# Neural Heightmap Tile Compression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimum viable single-tile neural heightmap compression loop: PNG tile -> Python/PyTorch training -> JSON model -> Unity C# `EvaluateHeight(Vector2 uv)`.

**Architecture:** Keep Python training tools outside Unity assets under `Tools/NeuralHeightmap`, and keep Unity runtime/editor code under `Assets/高度图AI`. The JSON file is the contract between Python and Unity, using deterministic Fourier encoding and row-major layer weights.

**Tech Stack:** Python 3, PyTorch, Pillow, NumPy, JSON, Unity C#, `JsonUtility`, `Vector2`, `Texture2D`, `EditorWindow`.

---

## File Structure

- Create: `Tools/NeuralHeightmap/train_height_tile.py`
  - CLI tool for loading a grayscale PNG, training a Fourier MLP, exporting JSON, printing metrics, and optionally writing a reconstructed preview PNG.
- Create: `Tools/NeuralHeightmap/README.md`
  - Short usage instructions and example commands.
- Create: `Assets/高度图AI/Runtime/NeuralHeightmapModel.cs`
  - Runtime JSON model loader and pure C# evaluator.
- Create if Unity generates it: `Assets/高度图AI/Runtime.meta`
  - Unity folder metadata.
- Create: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs`
  - Editor utility for loading a model JSON, querying UV, and writing a reconstructed preview texture asset.
- Create if Unity generates it: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs.meta`
  - Unity script metadata.

Do not modify the existing `Assets/高度图AI/Editor/HeightmapSliceWindow.cs` unless Unity compile errors require a namespace or path adjustment.

Known unrelated working tree changes may exist in `Packages/manifest.json`, `Packages/packages-lock.json`, `Assets/高度图AI/ExrportMap/`, and `Assets/高度图AI/New Terrain.asset`. Do not revert or include them unless the user explicitly asks.

---

### Task 1: Python Training Script

**Files:**
- Create: `Tools/NeuralHeightmap/train_height_tile.py`
- Create: `Tools/NeuralHeightmap/README.md`

- [ ] **Step 1: Create the tool folder**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'Tools/NeuralHeightmap'
```

Expected: `Tools/NeuralHeightmap` exists.

- [ ] **Step 2: Add the training script**

Create `Tools/NeuralHeightmap/train_height_tile.py` with this content:

```python
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
    ys = np.linspace(0.0, 1.0, height, dtype=np.float32)
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
    error = (predicted - heights.cpu()).abs()
    mse = torch.mean((predicted - heights.cpu()) ** 2).item()
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
    write_preview(predicted, width, height, preview_path) if preview_path is not None else None

    metrics = {
        "mse": metric_values["mse"],
        "mae": metric_values["mae"],
        "maxError": metric_values["maxError"],
        "sourceBytes": int(source_bytes),
        "modelBytes": 0,
        "compressionRatio": 0.0,
    }
    write_model_json(
        output_path,
        model,
        width,
        height,
        args.frequency_count,
        args.hidden_width,
        args.hidden_layers,
        metrics,
    )

    model_bytes = output_path.stat().st_size
    metrics["modelBytes"] = int(model_bytes)
    metrics["compressionRatio"] = float(source_bytes / model_bytes) if model_bytes > 0 else 0.0
    write_model_json(
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
```

- [ ] **Step 3: Add usage README**

Create `Tools/NeuralHeightmap/README.md`:

```markdown
# Neural Heightmap Tile Training

Train one grayscale height tile into a JSON neural model:

```powershell
python Tools/NeuralHeightmap/train_height_tile.py `
  --input "Assets/高度图AI/ExrportMap/New Terrain_y000_x000.png" `
  --output "Assets/高度图AI/Models/New Terrain_y000_x000.model.json" `
  --preview "Assets/高度图AI/Models/New Terrain_y000_x000.preview.png" `
  --steps 3000
```

Install dependencies if needed:

```powershell
python -m pip install torch pillow numpy
```

The JSON model is meant to be loaded by Unity `NeuralHeightmapModel`.
```

- [ ] **Step 4: Run a syntax check**

Run:

```powershell
python -m py_compile Tools/NeuralHeightmap/train_height_tile.py
```

Expected: exit code `0` and no output.

- [ ] **Step 5: Commit Python training tools**

Run:

```powershell
git add Tools/NeuralHeightmap/train_height_tile.py Tools/NeuralHeightmap/README.md
git commit -m neural-heightmap-python-trainer
```

Expected: commit succeeds.

---

### Task 2: Python Smoke Training

**Files:**
- May create generated artifacts outside git tracking:
  - `Temp/NeuralHeightmapSmoke/smoke.model.json`
  - `Temp/NeuralHeightmapSmoke/smoke.preview.png`

- [ ] **Step 1: Confirm dependencies**

Run:

```powershell
python -c "import torch, PIL, numpy; print('torch', torch.__version__); print('pillow', PIL.__version__); print('numpy', numpy.__version__)"
```

Expected: versions print. If imports fail, run:

```powershell
python -m pip install torch pillow numpy
```

- [ ] **Step 2: Train a quick smoke model**

Use an existing exported tile if present:

```powershell
New-Item -ItemType Directory -Force -Path 'Temp/NeuralHeightmapSmoke'
python Tools/NeuralHeightmap/train_height_tile.py `
  --input "Assets/高度图AI/ExrportMap/New Terrain_y000_x000.png" `
  --output "Temp/NeuralHeightmapSmoke/smoke.model.json" `
  --preview "Temp/NeuralHeightmapSmoke/smoke.preview.png" `
  --steps 25 `
  --batch-size 1024 `
  --hidden-width 16 `
  --hidden-layers 2 `
  --frequency-count 4
```

Expected:

- The command exits `0`.
- It prints `mse=`, `mae=`, `max_error=`, `source_bytes=`, `model_bytes=`, and `compression_ratio=`.
- `Temp/NeuralHeightmapSmoke/smoke.model.json` exists.
- `Temp/NeuralHeightmapSmoke/smoke.preview.png` exists.

If the tile path does not exist, use any one `256x256` exported PNG tile from `Assets/高度图AI/ExrportMap`.

- [ ] **Step 3: Inspect JSON shape**

Run:

```powershell
python -c "import json; from pathlib import Path; data=json.loads(Path('Temp/NeuralHeightmapSmoke/smoke.model.json').read_text(encoding='utf-8')); assert data['version']==1; assert data['frequencyCount']==4; assert len(data['layers'])==3; assert data['layers'][0]['inputSize']==18; assert data['layers'][-1]['outputSize']==1; print('json ok')"
```

Expected: prints `json ok`.

- [ ] **Step 4: Do not commit smoke artifacts**

Run:

```powershell
git status --short
```

Expected: smoke outputs under `Temp/` are ignored or at least not staged. Do not commit generated model/preview artifacts unless the user asks.

---

### Task 3: Unity Runtime Evaluator

**Files:**
- Create: `Assets/高度图AI/Runtime/NeuralHeightmapModel.cs`
- Create if Unity generates it: `Assets/高度图AI/Runtime.meta`

- [ ] **Step 1: Create runtime folder**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'Assets/高度图AI/Runtime'
```

Expected: folder exists.

- [ ] **Step 2: Add runtime model evaluator**

Create `Assets/高度图AI/Runtime/NeuralHeightmapModel.cs`:

```csharp
using System;
using UnityEngine;

namespace HeightmapAI
{
    [Serializable]
    internal sealed class NeuralHeightmapModelData
    {
        public int version;
        public int tileWidth;
        public int tileHeight;
        public int frequencyCount;
        public int hiddenWidth;
        public int hiddenLayers;
        public string activation;
        public float heightMin;
        public float heightMax;
        public NeuralHeightmapLayerData[] layers;
        public NeuralHeightmapMetricsData metrics;
    }

    [Serializable]
    internal sealed class NeuralHeightmapLayerData
    {
        public int inputSize;
        public int outputSize;
        public float[] weights;
        public float[] bias;
    }

    [Serializable]
    internal sealed class NeuralHeightmapMetricsData
    {
        public float mse;
        public float mae;
        public float maxError;
        public int sourceBytes;
        public int modelBytes;
        public float compressionRatio;
    }

    public sealed class NeuralHeightmapModel
    {
        private readonly NeuralHeightmapModelData data;
        private readonly float[] encodingBuffer;
        private readonly float[][] layerBuffers;

        private NeuralHeightmapModel(NeuralHeightmapModelData data)
        {
            this.data = data;
            Validate(data);
            encodingBuffer = new float[2 + 4 * data.frequencyCount];
            layerBuffers = new float[data.layers.Length][];
            for (int i = 0; i < data.layers.Length; i++)
            {
                layerBuffers[i] = new float[data.layers[i].outputSize];
            }
        }

        public int TileWidth => data.tileWidth;
        public int TileHeight => data.tileHeight;
        public float Mse => data.metrics != null ? data.metrics.mse : 0f;
        public float Mae => data.metrics != null ? data.metrics.mae : 0f;
        public float MaxError => data.metrics != null ? data.metrics.maxError : 0f;
        public float CompressionRatio => data.metrics != null ? data.metrics.compressionRatio : 0f;

        public static NeuralHeightmapModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Model JSON is empty.", nameof(json));
            }

            NeuralHeightmapModelData parsed = JsonUtility.FromJson<NeuralHeightmapModelData>(json);
            return new NeuralHeightmapModel(parsed);
        }

        public float EvaluateHeight(Vector2 uv)
        {
            Encode(new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y)), encodingBuffer, data.frequencyCount);

            float[] current = encodingBuffer;
            for (int layerIndex = 0; layerIndex < data.layers.Length; layerIndex++)
            {
                NeuralHeightmapLayerData layer = data.layers[layerIndex];
                float[] output = layerBuffers[layerIndex];
                for (int outputIndex = 0; outputIndex < layer.outputSize; outputIndex++)
                {
                    float value = layer.bias[outputIndex];
                    int weightOffset = outputIndex * layer.inputSize;
                    for (int inputIndex = 0; inputIndex < layer.inputSize; inputIndex++)
                    {
                        value += layer.weights[weightOffset + inputIndex] * current[inputIndex];
                    }

                    bool isHiddenLayer = layerIndex < data.layers.Length - 1;
                    output[outputIndex] = isHiddenLayer ? Mathf.Max(0f, value) : value;
                }

                current = output;
            }

            return Mathf.Clamp01(current[0]);
        }

        public Texture2D ReconstructTexture()
        {
            Texture2D texture = new Texture2D(data.tileWidth, data.tileHeight, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < data.tileHeight; y++)
            {
                float v = data.tileHeight <= 1 ? 0f : (float)y / (data.tileHeight - 1);
                for (int x = 0; x < data.tileWidth; x++)
                {
                    float u = data.tileWidth <= 1 ? 0f : (float)x / (data.tileWidth - 1);
                    float height = EvaluateHeight(new Vector2(u, v));
                    texture.SetPixel(x, y, new Color(height, height, height, 1f));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void Encode(Vector2 uv, float[] output, int frequencyCount)
        {
            output[0] = uv.x;
            output[1] = uv.y;
            int index = 2;
            for (int frequency = 1; frequency <= frequencyCount; frequency++)
            {
                float angleU = 2f * Mathf.PI * frequency * uv.x;
                float angleV = 2f * Mathf.PI * frequency * uv.y;
                output[index++] = Mathf.Sin(angleU);
                output[index++] = Mathf.Cos(angleU);
                output[index++] = Mathf.Sin(angleV);
                output[index++] = Mathf.Cos(angleV);
            }
        }

        private static void Validate(NeuralHeightmapModelData data)
        {
            if (data == null)
            {
                throw new ArgumentException("Model JSON could not be parsed.");
            }

            if (data.version != 1)
            {
                throw new ArgumentException($"Unsupported model version {data.version}.");
            }

            if (!string.Equals(data.activation, "relu", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unsupported activation '{data.activation}'.");
            }

            if (data.tileWidth <= 0 || data.tileHeight <= 0)
            {
                throw new ArgumentException("Model tile size must be greater than zero.");
            }

            if (data.frequencyCount <= 0)
            {
                throw new ArgumentException("Model frequency count must be greater than zero.");
            }

            if (data.layers == null || data.layers.Length == 0)
            {
                throw new ArgumentException("Model must contain at least one layer.");
            }

            int expectedInputSize = 2 + 4 * data.frequencyCount;
            for (int i = 0; i < data.layers.Length; i++)
            {
                NeuralHeightmapLayerData layer = data.layers[i];
                if (layer == null)
                {
                    throw new ArgumentException($"Layer {i} is missing.");
                }

                if (layer.inputSize != expectedInputSize)
                {
                    throw new ArgumentException($"Layer {i} input size is {layer.inputSize}, expected {expectedInputSize}.");
                }

                if (layer.outputSize <= 0)
                {
                    throw new ArgumentException($"Layer {i} output size must be greater than zero.");
                }

                if (layer.weights == null || layer.weights.Length != layer.inputSize * layer.outputSize)
                {
                    throw new ArgumentException($"Layer {i} has an invalid weight array length.");
                }

                if (layer.bias == null || layer.bias.Length != layer.outputSize)
                {
                    throw new ArgumentException($"Layer {i} has an invalid bias array length.");
                }

                expectedInputSize = layer.outputSize;
            }

            if (data.layers[data.layers.Length - 1].outputSize != 1)
            {
                throw new ArgumentException("Final layer output size must be 1.");
            }
        }
    }
}
```

- [ ] **Step 3: Check likely compile issues before opening Unity**

Run:

```powershell
Select-String -Path 'Assets/高度图AI/Runtime/NeuralHeightmapModel.cs' -Pattern 'TODO','TBD'
```

Expected: no output.

- [ ] **Step 4: Commit runtime evaluator**

Run:

```powershell
git add 'Assets/高度图AI/Runtime/NeuralHeightmapModel.cs'
if (Test-Path 'Assets/高度图AI/Runtime.meta') { git add 'Assets/高度图AI/Runtime.meta' }
git commit -m neural-heightmap-unity-runtime
```

Expected: commit succeeds.

---

### Task 4: Unity Editor Preview Window

**Files:**
- Create: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs`
- Create if Unity generates it: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs.meta`

- [ ] **Step 1: Add editor preview window**

Create `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs`:

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeightmapAI.Editor
{
    public sealed class NeuralHeightmapPreviewWindow : EditorWindow
    {
        private TextAsset modelJson;
        private NeuralHeightmapModel model;
        private Vector2 uv = new Vector2(0.5f, 0.5f);
        private float height;
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Heightmap AI/Neural Heightmap Preview")]
        private static void Open()
        {
            GetWindow<NeuralHeightmapPreviewWindow>("Neural Heightmap");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            TextAsset selected = (TextAsset)EditorGUILayout.ObjectField("JSON", modelJson, typeof(TextAsset), false);
            if (selected != modelJson)
            {
                modelJson = selected;
                model = null;
                statusMessage = "";
            }

            if (GUILayout.Button("Load Model"))
            {
                LoadModel();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evaluate", EditorStyles.boldLabel);
            uv = EditorGUILayout.Vector2Field("UV", uv);
            if (GUILayout.Button("Evaluate Height"))
            {
                Evaluate();
            }

            EditorGUILayout.FloatField("Height", height);

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Reconstructed Preview PNG"))
            {
                SavePreviewPng();
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void LoadModel()
        {
            try
            {
                if (modelJson == null)
                {
                    throw new InvalidOperationException("Select a model JSON TextAsset.");
                }

                model = NeuralHeightmapModel.FromJson(modelJson.text);
                statusType = MessageType.Info;
                statusMessage = $"Loaded {model.TileWidth}x{model.TileHeight}. MSE={model.Mse:0.000000}, MAE={model.Mae:0.000000}, Ratio={model.CompressionRatio:0.00}.";
            }
            catch (Exception exception)
            {
                model = null;
                statusType = MessageType.Error;
                statusMessage = exception.Message;
            }
        }

        private void Evaluate()
        {
            try
            {
                EnsureModelLoaded();
                height = model.EvaluateHeight(uv);
                statusType = MessageType.Info;
                statusMessage = $"height={height:0.000000}";
            }
            catch (Exception exception)
            {
                statusType = MessageType.Error;
                statusMessage = exception.Message;
            }
        }

        private void SavePreviewPng()
        {
            Texture2D texture = null;
            try
            {
                EnsureModelLoaded();
                string path = EditorUtility.SaveFilePanel("Save Preview PNG", Application.dataPath, "neural_heightmap_preview", "png");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                texture = model.ReconstructTexture();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.Refresh();
                statusType = MessageType.Info;
                statusMessage = "Saved preview PNG: " + path;
            }
            catch (Exception exception)
            {
                statusType = MessageType.Error;
                statusMessage = exception.Message;
            }
            finally
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
        }

        private void EnsureModelLoaded()
        {
            if (model == null)
            {
                LoadModel();
            }

            if (model == null)
            {
                throw new InvalidOperationException("Model is not loaded.");
            }
        }
    }
}
```

- [ ] **Step 2: Commit editor preview**

Run:

```powershell
git add 'Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs'
if (Test-Path 'Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs.meta') { git add 'Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs.meta' }
git commit -m neural-heightmap-unity-preview
```

Expected: commit succeeds.

---

### Task 5: End-To-End Verification

**Files:**
- Generated but not committed unless requested:
  - `Temp/NeuralHeightmapSmoke/smoke.model.json`
  - `Temp/NeuralHeightmapSmoke/smoke.preview.png`

- [ ] **Step 1: Run Python syntax check**

Run:

```powershell
python -m py_compile Tools/NeuralHeightmap/train_height_tile.py
```

Expected: exit code `0`.

- [ ] **Step 2: Run Python smoke training**

Run the smoke command from Task 2.

Expected: metrics print and output files exist.

- [ ] **Step 3: Try Unity compile check**

Only run this if no Unity editor currently has `F:\UnityProject\OIT` open:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' -batchmode -quit -projectPath 'F:\UnityProject\OIT' -logFile 'F:\UnityProject\OIT\Temp\neural-heightmap-compile.log'
```

Expected if Unity is available and project is not already open:

- Exit code `0`.
- Log does not contain `error CS`.

If Unity reports `Multiple Unity instances cannot open the same project`, record that compile verification was blocked by the open editor instead of claiming compile success.

- [ ] **Step 4: Manual Unity verification**

In Unity:

1. Put `smoke.model.json` under `Assets/高度图AI/Models/` or select any imported JSON `TextAsset`.
2. Open `Tools/Heightmap AI/Neural Heightmap Preview`.
3. Select the JSON.
4. Click `Load Model`.
5. Set UV to `(0.5, 0.5)`.
6. Click `Evaluate Height`.
7. Click `Save Reconstructed Preview PNG`.

Expected:

- The model loads and shows metrics.
- Height is between `0` and `1`.
- Preview PNG is written.

- [ ] **Step 5: Final review**

Review:

```powershell
git status --short
git log --oneline -8
```

Expected:

- Only unrelated user/generated changes remain unstaged unless Unity generated `.meta` files for new scripts.
- New implementation commits are present.
- If `.meta` files for new C# scripts were generated, add and commit them.

---

## Self-Review Notes

- Spec coverage: The plan includes Python/PyTorch training, Fourier encoding, JSON export, metrics, Unity JSON loading, `EvaluateHeight(Vector2 uv)`, and preview reconstruction.
- Out-of-scope features remain out of the plan: no full-map tile manifest, no multi-tile batch training, no shared encoder/decoder, no Sentis/ONNX runtime, no Compute Shader, and no binary model format.
- Type consistency: Python JSON keys match Unity `NeuralHeightmapModelData` fields. Python row-major weights match Unity indexing `outputIndex * inputSize + inputIndex`.
- Known risk: Unity `JsonUtility` requires JSON root fields to match serializable field names. The plan uses fields, not properties, for parsed data.
