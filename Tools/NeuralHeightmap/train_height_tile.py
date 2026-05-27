import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image
import torch
from torch import nn


def fourier_encode(uv: torch.Tensor, frequency_count: int) -> torch.Tensor:
    parts = [uv]
    u = uv[:, 0:1]
    v = uv[:, 1:2]
    for frequency in range(1, frequency_count + 1):
        angle_u = 2.0 * math.pi * frequency * u
        angle_v = 2.0 * math.pi * frequency * v
        parts.extend([torch.sin(angle_u), torch.cos(angle_u), torch.sin(angle_v), torch.cos(angle_v)])
    return torch.cat(parts, dim=1)


class FourierMlp(nn.Module):
    def __init__(self, frequency_count: int, hidden_width: int, hidden_layers: int):
        super().__init__()
        input_size = 2 + 4 * frequency_count
        layers = []
        current_size = input_size
        for _ in range(hidden_layers):
            layers.append(nn.Linear(current_size, hidden_width))
            layers.append(nn.ReLU())
            current_size = hidden_width
        layers.append(nn.Linear(current_size, 1))
        self.frequency_count = frequency_count
        self.network = nn.Sequential(*layers)

    def forward(self, uv: torch.Tensor) -> torch.Tensor:
        encoded = fourier_encode(uv, self.frequency_count)
        return self.network(encoded)


def load_height_tile(path: Path) -> tuple[np.ndarray, int]:
    image = Image.open(path).convert("L")
    array = np.asarray(image, dtype=np.float32) / 255.0
    return array, path.stat().st_size


def build_uv_grid(width: int, height: int) -> np.ndarray:
    xs = np.linspace(0.0, 1.0, width, dtype=np.float32)
    ys = np.linspace(1.0, 0.0, height, dtype=np.float32)
    grid_x, grid_y = np.meshgrid(xs, ys)
    return np.stack([grid_x.reshape(-1), grid_y.reshape(-1)], axis=1)


def collect_linear_layers(model: FourierMlp) -> list[dict]:
    layers = []
    for module in model.network:
        if isinstance(module, nn.Linear):
            weight = module.weight.detach().cpu().numpy().astype(np.float32)
            bias = module.bias.detach().cpu().numpy().astype(np.float32)
            layers.append(
                {
                    "inputSize": int(weight.shape[1]),
                    "outputSize": int(weight.shape[0]),
                    "weights": weight.reshape(-1).tolist(),
                    "bias": bias.tolist(),
                }
            )
    return layers


@torch.no_grad()
def evaluate_model(model: FourierMlp, uv: torch.Tensor, heights: torch.Tensor, batch_size: int) -> tuple[np.ndarray, dict]:
    model.eval()
    predictions = []
    for start in range(0, uv.shape[0], batch_size):
        batch = uv[start : start + batch_size]
        prediction = model(batch).squeeze(1).clamp(0.0, 1.0)
        predictions.append(prediction.cpu())
    predicted = torch.cat(predictions, dim=0)
    expected = heights.cpu()
    error = (predicted - expected).abs()
    mse = torch.mean((predicted - expected) ** 2).item()
    mae = torch.mean(error).item()
    max_error = torch.max(error).item()
    return predicted.numpy(), {"mse": mse, "mae": mae, "maxError": max_error}


def write_preview(predicted: np.ndarray, width: int, height: int, output_path: Path) -> None:
    image = (predicted.reshape(height, width).clip(0.0, 1.0) * 255.0).round().astype(np.uint8)
    Image.fromarray(image, mode="L").save(output_path)


def write_model_json(
    output_path: Path,
    model: FourierMlp,
    width: int,
    height: int,
    frequency_count: int,
    hidden_width: int,
    hidden_layers: int,
    metrics: dict,
) -> None:
    payload = {
        "version": 1,
        "tileWidth": width,
        "tileHeight": height,
        "frequencyCount": frequency_count,
        "hiddenWidth": hidden_width,
        "hiddenLayers": hidden_layers,
        "activation": "relu",
        "heightMin": 0.0,
        "heightMax": 1.0,
        "layers": collect_linear_layers(model),
        "metrics": metrics,
    }
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def write_model_json_with_final_size(
    output_path: Path,
    model: FourierMlp,
    width: int,
    height: int,
    frequency_count: int,
    hidden_width: int,
    hidden_layers: int,
    metrics: dict,
) -> None:
    previous_size = -1
    for _ in range(4):
        write_model_json(
            output_path,
            model,
            width,
            height,
            frequency_count,
            hidden_width,
            hidden_layers,
            metrics,
        )
        model_bytes = output_path.stat().st_size
        metrics["modelBytes"] = int(model_bytes)
        metrics["compressionRatio"] = float(metrics["sourceBytes"] / model_bytes) if model_bytes > 0 else 0.0
        if model_bytes == previous_size:
            return
        previous_size = model_bytes

    write_model_json(
        output_path,
        model,
        width,
        height,
        frequency_count,
        hidden_width,
        hidden_layers,
        metrics,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train one neural heightmap tile model.")
    parser.add_argument("--input", required=True, help="Input grayscale PNG tile path.")
    parser.add_argument("--output", required=True, help="Output model JSON path.")
    parser.add_argument("--preview", default="", help="Optional reconstructed preview PNG path.")
    parser.add_argument("--frequency-count", type=int, default=8)
    parser.add_argument("--hidden-width", type=int, default=64)
    parser.add_argument("--hidden-layers", type=int, default=3)
    parser.add_argument("--steps", type=int, default=3000)
    parser.add_argument("--batch-size", type=int, default=8192)
    parser.add_argument("--learning-rate", type=float, default=1e-3)
    parser.add_argument("--seed", type=int, default=1234)
    parser.add_argument("--device", default="cuda" if torch.cuda.is_available() else "cpu")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)

    input_path = Path(args.input)
    output_path = Path(args.output)
    preview_path = Path(args.preview) if args.preview else None
    output_path.parent.mkdir(parents=True, exist_ok=True)
    if preview_path is not None:
        preview_path.parent.mkdir(parents=True, exist_ok=True)

    height_array, source_bytes = load_height_tile(input_path)
    height, width = height_array.shape
    uv_np = build_uv_grid(width, height)
    heights_np = height_array.reshape(-1)

    device = torch.device(args.device)
    uv = torch.from_numpy(uv_np).to(device)
    heights = torch.from_numpy(heights_np).to(device)
    model = FourierMlp(args.frequency_count, args.hidden_width, args.hidden_layers).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=args.learning_rate)
    loss_fn = nn.MSELoss()

    model.train()
    total_samples = uv.shape[0]
    for step in range(1, args.steps + 1):
        indices = torch.randint(0, total_samples, (args.batch_size,), device=device)
        batch_uv = uv[indices]
        batch_height = heights[indices].unsqueeze(1)
        prediction = model(batch_uv)
        loss = loss_fn(prediction, batch_height)
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()
        if step == 1 or step % 250 == 0 or step == args.steps:
            print(f"step={step} loss={loss.item():.8f}")

    predicted, metric_values = evaluate_model(model, uv, heights, args.batch_size)
    if preview_path is not None:
        write_preview(predicted, width, height, preview_path)

    metrics = {
        "mse": metric_values["mse"],
        "mae": metric_values["mae"],
        "maxError": metric_values["maxError"],
        "sourceBytes": int(source_bytes),
        "modelBytes": 0,
        "compressionRatio": 0.0,
    }
    write_model_json_with_final_size(
        output_path,
        model,
        width,
        height,
        args.frequency_count,
        args.hidden_width,
        args.hidden_layers,
        metrics,
    )

    print(f"mse={metrics['mse']:.8f}")
    print(f"mae={metrics['mae']:.8f}")
    print(f"max_error={metrics['maxError']:.8f}")
    print(f"source_bytes={metrics['sourceBytes']}")
    print(f"model_bytes={metrics['modelBytes']}")
    print(f"compression_ratio={metrics['compressionRatio']:.4f}")
    print(f"wrote={output_path}")
    if preview_path is not None:
        print(f"preview={preview_path}")


if __name__ == "__main__":
    main()
