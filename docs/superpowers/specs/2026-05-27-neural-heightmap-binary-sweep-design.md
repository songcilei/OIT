# Neural Heightmap Binary And Sweep Design

## Goal

Improve the single-tile neural heightmap experiment by adding:

- A compact binary `.bytes` model format.
- A Python parameter sweep tool that trains multiple small models and ranks their quality/size trade-offs.
- Unity loading for both existing JSON models and new binary `.bytes` models.

This stage keeps the same model family:

```text
Fourier encoded uv -> ReLU MLP -> height
```

The goal is to find a practical model size before moving to full 2048/4096 tile manifests or GPU decoding.

## Scope

Version 2 supports:

- Exporting `.bytes` models from Python.
- Loading `.bytes` models in Unity through `NeuralHeightmapModel.FromBytes(byte[] bytes)`.
- Keeping existing JSON loading through `NeuralHeightmapModel.FromJson(string json)`.
- Automatically training multiple model configurations for one tile.
- Writing a `sweep_results.csv` file with metrics and output paths.
- Ranking results by `mae` and then by `model_bytes`.

Version 2 does not include:

- Float16 or int8 quantization.
- Compute Shader decoding.
- Sentis or ONNX runtime.
- Full-map tile manifest.
- Multi-tile batch training.
- Shared decoder plus per-tile latent vectors.

## Binary Format

Use fixed little-endian encoding.

The first version of the binary format is `NHM1`.

Layout:

```text
magic: 4 bytes = "NHM1"
version: int32 = 1
tileWidth: int32
tileHeight: int32
frequencyCount: int32
hiddenWidth: int32
hiddenLayers: int32
layerCount: int32

for each layer:
  inputSize: int32
  outputSize: int32
  weightsCount: int32
  biasCount: int32
  weights: float32[weightsCount]
  bias: float32[biasCount]

metrics:
  mse: float32
  mae: float32
  maxError: float32
  sourceBytes: int32
  modelBytes: int32
  compressionRatio: float32
```

Weights keep the same row-major order used by the JSON exporter:

```text
weights[outputIndex * inputSize + inputIndex]
```

The binary format intentionally stores `float32` first. This makes correctness easy to verify against JSON. Later versions can add quantization with a new magic/version, such as `NHM2`.

## Python Export Changes

Extend the existing training code so it can export both JSON and binary models.

The binary writer should:

- Write all integer fields as little-endian signed `int32`.
- Write all float fields as little-endian `float32`.
- Write `modelBytes` after the final binary size is known.
- Re-write the binary file if needed so the stored `modelBytes` equals the actual file size.

The JSON exporter remains available for debugging and compatibility.

## Parameter Sweep Tool

Add:

```text
Tools/NeuralHeightmap/sweep_height_tile.py
```

Inputs:

- `--input`: source grayscale PNG tile.
- `--output-dir`: output directory for models, previews, and CSV.
- `--steps`: training steps per configuration.
- `--batch-size`: training batch size.
- `--learning-rate`: learning rate.
- `--frequency-counts`: comma-separated list, default `4,6,8`.
- `--hidden-widths`: comma-separated list, default `8,16,32`.
- `--hidden-layers`: comma-separated list, default `2,3`.
- `--preview`: optional flag to write preview PNGs.
- `--json`: optional flag to also write JSON files.

For each configuration, the sweep should train a model and write:

```text
f{frequency}_w{width}_l{layers}.bytes
f{frequency}_w{width}_l{layers}.preview.png  optional
f{frequency}_w{width}_l{layers}.json         optional
```

It writes:

```text
sweep_results.csv
```

CSV columns:

```text
frequency_count,
hidden_width,
hidden_layers,
mse,
mae,
max_error,
source_bytes,
model_bytes,
compression_ratio,
model_path,
preview_path
```

Sort rows by:

1. `mae` ascending.
2. `model_bytes` ascending.

The sweep is allowed to reuse training functions from `train_height_tile.py` instead of duplicating model code.

## Unity Loading Changes

Extend:

```text
Assets/高度图AI/Runtime/NeuralHeightmapModel.cs
```

Add:

```csharp
public static NeuralHeightmapModel FromBytes(byte[] bytes);
public static bool LooksLikeBinary(byte[] bytes);
```

`FromBytes` should:

- Validate the magic is `NHM1`.
- Validate version is `1`.
- Read layer metadata, weights, biases, and metrics.
- Reuse the same validation path as JSON.

The runtime evaluator remains unchanged after loading. JSON and binary data both produce the same internal model data structure.

## Unity Preview Changes

Update:

```text
Assets/高度图AI/Editor/NeuralHeightmapPreviewWindow.cs
```

The preview window should accept a `TextAsset` and decide loading mode:

- If `NeuralHeightmapModel.LooksLikeBinary(modelAsset.bytes)` is true, load with `FromBytes(modelAsset.bytes)`.
- Otherwise load with `FromJson(modelAsset.text)`.

The existing UV evaluation and preview PNG reconstruction stay the same.

## Validation

Python validation:

1. Train one `.bytes` model from a known tile.
2. Confirm file size is smaller than the equivalent JSON model.
3. Confirm binary `modelBytes` equals the actual file size.
4. Run a small sweep with at least two configurations.
5. Confirm `sweep_results.csv` is sorted by `mae`, then `model_bytes`.

Unity validation:

1. Load an existing JSON model in the preview window.
2. Load a new `.bytes` model in the same preview window.
3. Query the same UV on both JSON and `.bytes` models exported from the same weights.
4. Confirm heights match within a small tolerance, such as `0.0001`.
5. Save reconstructed preview PNG from `.bytes`.

## Follow-Up Features

After this stage:

- Add float16 or int8 quantized weights.
- Add a model selection command that chooses the smallest model below an MAE threshold.
- Add full-map tile manifest support.
- Add GPU batch decoding through Compute Shader.
