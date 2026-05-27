import argparse
import csv
from pathlib import Path
from types import SimpleNamespace

import torch

from train_height_tile import train_height_tile


def parse_int_list(value: str) -> list[int]:
    return [int(part.strip()) for part in value.split(",") if part.strip()]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Sweep neural heightmap tile model sizes.")
    parser.add_argument("--input", required=True, help="Input grayscale PNG tile path.")
    parser.add_argument("--output-dir", required=True, help="Directory for sweep outputs.")
    parser.add_argument("--steps", type=int, default=1000)
    parser.add_argument("--batch-size", type=int, default=8192)
    parser.add_argument("--learning-rate", type=float, default=1e-3)
    parser.add_argument("--frequency-counts", default="4,6,8")
    parser.add_argument("--hidden-widths", default="8,16,32")
    parser.add_argument("--hidden-layers", default="2,3")
    parser.add_argument("--preview", action="store_true")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--seed", type=int, default=1234)
    parser.add_argument("--device", default="")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    device = args.device or ("cuda" if torch.cuda.is_available() else "cpu")

    rows = []
    frequencies = parse_int_list(args.frequency_counts)
    widths = parse_int_list(args.hidden_widths)
    layers = parse_int_list(args.hidden_layers)

    for frequency_count in frequencies:
        for hidden_width in widths:
            for hidden_layers in layers:
                stem = f"f{frequency_count}_w{hidden_width}_l{hidden_layers}"
                binary_path = output_dir / f"{stem}.bytes"
                json_path = output_dir / f"{stem}.json" if args.json else Path("")
                preview_path = output_dir / f"{stem}.preview.png" if args.preview else Path("")
                train_args = SimpleNamespace(
                    input=args.input,
                    output=str(json_path) if args.json else "",
                    binary_output=str(binary_path),
                    preview=str(preview_path) if args.preview else "",
                    frequency_count=frequency_count,
                    hidden_width=hidden_width,
                    hidden_layers=hidden_layers,
                    steps=args.steps,
                    batch_size=args.batch_size,
                    learning_rate=args.learning_rate,
                    seed=args.seed,
                    device=device,
                )
                print(f"training {stem}")
                result = train_height_tile(train_args)
                metrics = result["metrics"]
                rows.append(
                    {
                        "frequency_count": frequency_count,
                        "hidden_width": hidden_width,
                        "hidden_layers": hidden_layers,
                        "mse": metrics["mse"],
                        "mae": metrics["mae"],
                        "max_error": metrics["maxError"],
                        "source_bytes": metrics["sourceBytes"],
                        "model_bytes": metrics["modelBytes"],
                        "compression_ratio": metrics["compressionRatio"],
                        "model_path": str(binary_path),
                        "preview_path": str(preview_path) if args.preview else "",
                    }
                )

    rows.sort(key=lambda row: (float(row["mae"]), int(row["model_bytes"])))
    csv_path = output_dir / "sweep_results.csv"
    fieldnames = [
        "frequency_count",
        "hidden_width",
        "hidden_layers",
        "mse",
        "mae",
        "max_error",
        "source_bytes",
        "model_bytes",
        "compression_ratio",
        "model_path",
        "preview_path",
    ]
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"wrote={csv_path}")
    if rows:
        best = rows[0]
        print(
            "best="
            f"f{best['frequency_count']}_w{best['hidden_width']}_l{best['hidden_layers']} "
            f"mae={best['mae']:.8f} bytes={best['model_bytes']}"
        )


if __name__ == "__main__":
    main()
