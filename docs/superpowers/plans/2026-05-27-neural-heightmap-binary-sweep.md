# Neural Heightmap Binary And Sweep Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add compact `.bytes` export/loading and a parameter sweep tool for the neural heightmap tile model.

**Architecture:** Extend the existing Python trainer with reusable binary export helpers, add a separate sweep CLI that reuses trainer functions, and extend the Unity runtime/editor loader so JSON and binary models share the same internal evaluator. The existing `EvaluateHeight(Vector2 uv)` path remains unchanged after model data is loaded.

**Tech Stack:** Python 3, PyTorch, Pillow, NumPy, CSV, binary `struct`, Unity C#, `BinaryReader`, `TextAsset`, `EditorWindow`.

---

## File Structure

- Modify: `Tools/NeuralHeightmap/train_height_tile.py`
  - Add reusable training function and `.bytes` binary writer.
  - Add optional `--binary-output` argument.
- Create: `Tools/NeuralHeightmap/sweep_height_tile.py`
  - Runs multiple configurations, writes `.bytes` models, optional previews/JSON, and `sweep_results.csv`.
- Modify: `Tools/NeuralHeightmap/README.md`
  - Document `.bytes` export and sweep usage.
- Modify: `Assets/高度图AI/Runtime/NeuralHeightmapModel.cs`
  - Add `FromBytes(byte[] bytes)` and `LooksLikeBinary(byte[] bytes)`.
- Modify: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs`
  - Auto-load binary `TextAsset.bytes` when magic is `NHM1`, otherwise fall back to JSON.

Known unrelated working tree changes may exist in `Packages/manifest.json`, `Packages/packages-lock.json`, generated Unity `.meta` files, `Assets/高度图AI/ExrportMap/`, `Assets/高度图AI/New Terrain.asset`, and local test model/image files. Do not revert them. Only stage files that are part of this plan.

---

### Task 1: Python Binary Export Helpers

**Files:**
- Modify: `Tools/NeuralHeightmap/train_height_tile.py`
- Modify: `Tools/NeuralHeightmap/README.md`

- [ ] **Step 1: Add imports**

Add `struct` to the imports near the top:

```python
import struct
```

- [ ] **Step 2: Add binary writer helpers**

Add these functions after `write_model_json_with_final_size`:

```python
def write_int32(buffer, value: int) -> None:
    buffer.write(struct.pack("<i", int(value)))


def write_float32(buffer, value: float) -> None:
    buffer.write(struct.pack("<f", float(value)))


def write_model_binary(
    output_path: Path,
    model: FourierMlp,
    width: int,
    height: int,
    frequency_count: int,
    hidden_width: int,
    hidden_layers: int,
    metrics: dict,
) -> None:
    layers = collect_linear_layers(model)
    with output_path.open("wb") as buffer:
        buffer.write(b"NHM1")
        write_int32(buffer, 1)
        write_int32(buffer, width)
        write_int32(buffer, height)
        write_int32(buffer, frequency_count)
        write_int32(buffer, hidden_width)
        write_int32(buffer, hidden_layers)
        write_int32(buffer, len(layers))

        for layer in layers:
            write_int32(buffer, layer["inputSize"])
            write_int32(buffer, layer["outputSize"])
            write_int32(buffer, len(layer["weights"]))
            write_int32(buffer, len(layer["bias"]))
            for weight in layer["weights"]:
                write_float32(buffer, weight)
            for bias in layer["bias"]:
                write_float32(buffer, bias)

        write_float32(buffer, metrics["mse"])
        write_float32(buffer, metrics["mae"])
        write_float32(buffer, metrics["maxError"])
        write_int32(buffer, metrics["sourceBytes"])
        write_int32(buffer, metrics["modelBytes"])
        write_float32(buffer, metrics["compressionRatio"])


def write_model_binary_with_final_size(
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
        write_model_binary(
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

    write_model_binary(
        output_path,
        model,
        width,
        height,
        frequency_count,
        hidden_width,
        hidden_layers,
        metrics,
    )
```

- [ ] **Step 3: Extract reusable training function**

Move the body of `main()` into a function named `train_height_tile(args: argparse.Namespace) -> dict`. It should return:

```python
{
    "metrics": metrics,
    "width": width,
    "height": height,
    "model": model,
    "predicted": predicted,
}
```

The function must still:

- Load the tile.
- Build UVs using the fixed Unity-compatible Y direction.
- Train the model.
- Evaluate full-grid metrics.
- Write preview if `args.preview` is set.
- Write JSON if `args.output` is set.
- Write binary if `args.binary_output` is set.
- Print the same metric lines.

- [ ] **Step 4: Add CLI argument for binary output**

In `parse_args()`, add:

```python
parser.add_argument("--binary-output", default="", help="Optional output binary .bytes path.")
```

- [ ] **Step 5: Update `main()`**

`main()` should become:

```python
def main() -> None:
    args = parse_args()
    train_height_tile(args)
```

- [ ] **Step 6: Update README**

Add an example:

```markdown
Export JSON and binary:

```powershell
py Tools/NeuralHeightmap/train_height_tile.py `
  --input "path/to/height_tile.png" `
  --output "Temp/NeuralHeightmapSmoke/height_tile.model.json" `
  --binary-output "Temp/NeuralHeightmapSmoke/height_tile.model.bytes" `
  --preview "Temp/NeuralHeightmapSmoke/height_tile.preview.png" `
  --steps 3000
```
```

- [ ] **Step 7: Verify Python compile**

Run:

```powershell
py -m py_compile Tools/NeuralHeightmap/train_height_tile.py
```

Expected: exit code `0`.

- [ ] **Step 8: Verify binary export smoke**

Run with an ASCII input path to avoid Windows shell encoding issues:

```powershell
New-Item -ItemType Directory -Force -Path 'Temp/NeuralHeightmapSmoke' | Out-Null
py -c "from PIL import Image; import numpy as np; arr=np.tile(np.linspace(255,0,16,dtype=np.uint8)[:,None],(1,16)); Image.fromarray(arr,'L').save('Temp/NeuralHeightmapSmoke/source.png')"
py Tools/NeuralHeightmap/train_height_tile.py `
  --input "Temp/NeuralHeightmapSmoke/source.png" `
  --output "Temp/NeuralHeightmapSmoke/binary-smoke.json" `
  --binary-output "Temp/NeuralHeightmapSmoke/binary-smoke.bytes" `
  --preview "Temp/NeuralHeightmapSmoke/binary-smoke.preview.png" `
  --steps 5 `
  --batch-size 64 `
  --hidden-width 8 `
  --hidden-layers 1 `
  --frequency-count 2
```

Expected:

- Command exits `0`.
- `.json`, `.bytes`, and preview PNG exist.
- `.bytes` starts with `NHM1`.

Check magic:

```powershell
$bytes = [System.IO.File]::ReadAllBytes('Temp/NeuralHeightmapSmoke/binary-smoke.bytes')
if ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'NHM1') { throw 'bad magic' }
'binary ok'
```

- [ ] **Step 9: Commit Python binary export**

Run:

```powershell
git add Tools/NeuralHeightmap/train_height_tile.py Tools/NeuralHeightmap/README.md
git commit -m neural-heightmap-python-binary-export
```

Expected: commit succeeds.

---

### Task 2: Python Sweep Tool

**Files:**
- Create: `Tools/NeuralHeightmap/sweep_height_tile.py`
- Modify: `Tools/NeuralHeightmap/README.md`

- [ ] **Step 1: Add sweep script**

Create `Tools/NeuralHeightmap/sweep_height_tile.py`:

```python
import argparse
import csv
from pathlib import Path
from types import SimpleNamespace

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
                    device=args.device,
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
```

- [ ] **Step 2: Update README with sweep command**

Add:

```markdown
Run a small parameter sweep:

```powershell
py Tools/NeuralHeightmap/sweep_height_tile.py `
  --input "path/to/height_tile.png" `
  --output-dir "Temp/NeuralHeightmapSweep" `
  --steps 1000 `
  --frequency-counts "4,6,8" `
  --hidden-widths "8,16,32" `
  --hidden-layers "2,3" `
  --preview
```
```

- [ ] **Step 3: Verify Python compile**

Run:

```powershell
py -m py_compile Tools/NeuralHeightmap/train_height_tile.py Tools/NeuralHeightmap/sweep_height_tile.py
```

Expected: exit code `0`.

- [ ] **Step 4: Verify small sweep**

Run a tiny two-configuration sweep:

```powershell
New-Item -ItemType Directory -Force -Path 'Temp/NeuralHeightmapSweep' | Out-Null
py Tools/NeuralHeightmap/sweep_height_tile.py `
  --input "Temp/NeuralHeightmapSmoke/source.png" `
  --output-dir "Temp/NeuralHeightmapSweep" `
  --steps 3 `
  --batch-size 32 `
  --frequency-counts "2" `
  --hidden-widths "4,8" `
  --hidden-layers "1" `
  --preview
```

Expected:

- Two `.bytes` files exist.
- `sweep_results.csv` exists.
- CSV has two data rows.
- Rows are sorted by `mae`, then `model_bytes`.

Check rows:

```powershell
$rows = Import-Csv 'Temp/NeuralHeightmapSweep/sweep_results.csv'
if ($rows.Count -ne 2) { throw "expected 2 rows" }
'sweep ok'
```

- [ ] **Step 5: Commit sweep tool**

Run:

```powershell
git add Tools/NeuralHeightmap/sweep_height_tile.py Tools/NeuralHeightmap/README.md
git commit -m neural-heightmap-parameter-sweep
```

Expected: commit succeeds.

---

### Task 3: Unity Binary Runtime Loading

**Files:**
- Modify: `Assets/高度图AI/Runtime/NeuralHeightmapModel.cs`

- [ ] **Step 1: Add imports**

Add:

```csharp
using System.IO;
using System.Text;
```

- [ ] **Step 2: Add binary loading methods**

Inside `NeuralHeightmapModel`, after `FromJson`, add:

```csharp
public static bool LooksLikeBinary(byte[] bytes)
{
    return bytes != null
        && bytes.Length >= 4
        && bytes[0] == (byte)'N'
        && bytes[1] == (byte)'H'
        && bytes[2] == (byte)'M'
        && bytes[3] == (byte)'1';
}

public static NeuralHeightmapModel FromBytes(byte[] bytes)
{
    if (!LooksLikeBinary(bytes))
    {
        throw new ArgumentException("Model bytes do not start with NHM1.", nameof(bytes));
    }

    using (var stream = new MemoryStream(bytes))
    using (var reader = new BinaryReader(stream, Encoding.UTF8))
    {
        string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != "NHM1")
        {
            throw new ArgumentException("Unsupported binary model magic '" + magic + "'.");
        }

        var parsed = new NeuralHeightmapModelData
        {
            version = reader.ReadInt32(),
            tileWidth = reader.ReadInt32(),
            tileHeight = reader.ReadInt32(),
            frequencyCount = reader.ReadInt32(),
            hiddenWidth = reader.ReadInt32(),
            hiddenLayers = reader.ReadInt32()
        };

        int layerCount = reader.ReadInt32();
        parsed.activation = "relu";
        parsed.heightMin = 0f;
        parsed.heightMax = 1f;
        parsed.layers = new NeuralHeightmapLayerData[layerCount];

        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            int inputSize = reader.ReadInt32();
            int outputSize = reader.ReadInt32();
            int weightsCount = reader.ReadInt32();
            int biasCount = reader.ReadInt32();
            var layer = new NeuralHeightmapLayerData
            {
                inputSize = inputSize,
                outputSize = outputSize,
                weights = new float[weightsCount],
                bias = new float[biasCount]
            };

            for (int i = 0; i < weightsCount; i++)
            {
                layer.weights[i] = reader.ReadSingle();
            }

            for (int i = 0; i < biasCount; i++)
            {
                layer.bias[i] = reader.ReadSingle();
            }

            parsed.layers[layerIndex] = layer;
        }

        parsed.metrics = new NeuralHeightmapMetricsData
        {
            mse = reader.ReadSingle(),
            mae = reader.ReadSingle(),
            maxError = reader.ReadSingle(),
            sourceBytes = reader.ReadInt32(),
            modelBytes = reader.ReadInt32(),
            compressionRatio = reader.ReadSingle()
        };

        if (stream.Position != stream.Length)
        {
            throw new ArgumentException("Binary model contains trailing bytes.");
        }

        return new NeuralHeightmapModel(parsed);
    }
}
```

- [ ] **Step 3: Build check**

Run:

```powershell
dotnet build F:\UnityProject\OIT\Assembly-CSharp-Editor.csproj --no-restore
```

Expected: exit code `0`; no errors.

- [ ] **Step 4: Commit runtime binary loader**

Run:

```powershell
git add 'Assets/高度图AI/Runtime/NeuralHeightmapModel.cs'
git commit -m neural-heightmap-unity-binary-loader
```

Expected: commit succeeds.

---

### Task 4: Unity Preview Auto-Detect JSON Or Binary

**Files:**
- Modify: `Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs`

- [ ] **Step 1: Update `LoadModel()`**

Replace:

```csharp
model = NeuralHeightmapModel.FromJson(modelJson.text);
```

with:

```csharp
model = NeuralHeightmapModel.LooksLikeBinary(modelJson.bytes)
    ? NeuralHeightmapModel.FromBytes(modelJson.bytes)
    : NeuralHeightmapModel.FromJson(modelJson.text);
```

- [ ] **Step 2: Add load mode status**

Before setting the status message, compute:

```csharp
string mode = NeuralHeightmapModel.LooksLikeBinary(modelJson.bytes) ? "binary" : "json";
```

Then include the mode in the status:

```csharp
statusMessage = $"Loaded {mode} model {model.TileWidth}x{model.TileHeight}. MAE: {model.Mae:0.######}, Max Error: {model.MaxError:0.######}.";
```

- [ ] **Step 3: Build check**

Run:

```powershell
dotnet build F:\UnityProject\OIT\Assembly-CSharp-Editor.csproj --no-restore
```

Expected: exit code `0`; no errors.

- [ ] **Step 4: Commit preview binary support**

Run:

```powershell
git add 'Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs'
git commit -m neural-heightmap-preview-binary-support
```

Expected: commit succeeds.

---

### Task 5: End-To-End Validation

**Files:**
- Generated artifacts in `Temp/` only; do not commit.

- [ ] **Step 1: Run Python compile checks**

Run:

```powershell
py -m py_compile Tools/NeuralHeightmap/train_height_tile.py Tools/NeuralHeightmap/sweep_height_tile.py
```

Expected: exit code `0`.

- [ ] **Step 2: Export paired JSON and binary**

Run:

```powershell
py Tools/NeuralHeightmap/train_height_tile.py `
  --input "Temp/NeuralHeightmapSmoke/source.png" `
  --output "Temp/NeuralHeightmapSmoke/pair.json" `
  --binary-output "Temp/NeuralHeightmapSmoke/pair.bytes" `
  --steps 5 `
  --batch-size 64 `
  --hidden-width 8 `
  --hidden-layers 1 `
  --frequency-count 2
```

Expected:

- JSON and `.bytes` exist.
- `.bytes` is smaller than JSON.
- Binary magic is `NHM1`.
- Binary metrics `modelBytes` equals actual `.bytes` size.

- [ ] **Step 3: Run small sweep**

Run:

```powershell
py Tools/NeuralHeightmap/sweep_height_tile.py `
  --input "Temp/NeuralHeightmapSmoke/source.png" `
  --output-dir "Temp/NeuralHeightmapSweep" `
  --steps 3 `
  --batch-size 32 `
  --frequency-counts "2" `
  --hidden-widths "4,8" `
  --hidden-layers "1" `
  --preview
```

Expected:

- `sweep_results.csv` exists.
- Two `.bytes` models exist.
- CSV has two rows and is sorted by `mae`, then `model_bytes`.

- [ ] **Step 4: Run Unity build check**

Run:

```powershell
dotnet build F:\UnityProject\OIT\Assembly-CSharp-Editor.csproj --no-restore
```

Expected: exit code `0`; no errors.

- [ ] **Step 5: Manual Unity check**

In Unity:

1. Import or place a generated `.bytes` model under `Assets`.
2. Open `Tools/Heightmap AI/Neural Heightmap Preview`.
3. Select the `.bytes` `TextAsset`.
4. Click `Load Model`.
5. Evaluate a UV.
6. Save reconstructed preview PNG.

Expected:

- Status says it loaded a binary model.
- Height displays successfully.
- Preview PNG saves.

- [ ] **Step 6: Final status check**

Run:

```powershell
git status --short
git log --oneline -10
```

Expected:

- Implementation files are committed.
- Generated `Temp/` artifacts are not staged.
- Existing unrelated user/generated changes remain untouched.

---

## Self-Review Notes

- Spec coverage: Binary `.bytes` export, `NHM1` format, sweep CSV, Unity `FromBytes`, binary/JSON auto-detection, and validation steps are covered.
- Out-of-scope features remain out: no quantization, Compute Shader, Sentis/ONNX runtime, tile manifest, multi-tile batch training, or shared decoder.
- Type consistency: Python writes little-endian `int32`/`float32`; Unity reads with `BinaryReader` little-endian methods. JSON and binary both populate `NeuralHeightmapModelData`.
