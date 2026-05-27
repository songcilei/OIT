# Neural Heightmap Tile Compression Design

## Goal

Build a minimum viable experiment for compressing one grayscale heightmap tile into a small neural representation that Unity can query with UV coordinates.

The first version proves this loop:

```text
256x256 height tile PNG -> Python training -> model JSON -> Unity EvaluateHeight(uv)
```

The model represents a height tile as a function:

```text
height = f(u, v)
```

where `u` and `v` are normalized coordinates in the `0..1` range and `height` is normalized to `0..1`.

## Scope

Version 1 supports:

- A single readable grayscale PNG tile, expected to be `256x256`.
- Python/PyTorch training for one tile.
- Fourier positional encoding plus a small ReLU MLP.
- JSON model export for easy inspection and Unity loading.
- Unity C# decoding with `EvaluateHeight(Vector2 uv)`.
- Basic metrics: MSE, MAE, max error, original PNG size, JSON size, compression ratio.

Version 1 does not include:

- Full 4096 or 2048 heightmap tile indexing.
- Multi-tile batch training.
- A general encoder/decoder trained across many maps.
- Sentis or ONNX runtime.
- Compute Shader decoding.
- Binary weight files.
- Automatic train/validation/test splitting.

These features can follow after the single-tile experiment proves quality and runtime feasibility.

## Model

Use a Fourier Features + ReLU MLP model.

Default configuration:

- Tile width: `256`
- Tile height: `256`
- Fourier frequency count: `8`
- Hidden width: `64`
- Hidden layers: `3`
- Output size: `1`
- Activation: `ReLU`
- Loss: mean squared error

The input encoding is deterministic and shared by Python and Unity:

```text
[u, v,
 sin(2*pi*1*u), cos(2*pi*1*u), sin(2*pi*1*v), cos(2*pi*1*v),
 sin(2*pi*2*u), cos(2*pi*2*u), sin(2*pi*2*v), cos(2*pi*2*v),
 ...
 sin(2*pi*N*u), cos(2*pi*N*u), sin(2*pi*N*v), cos(2*pi*N*v)]
```

With `N = 8`, the encoded input size is:

```text
2 + 4 * 8 = 34
```

The MLP layout is:

```text
34 -> 64 -> 64 -> 64 -> 1
```

The final output is clamped to `0..1` when used in Unity.

## Training Data

The training script loads a grayscale PNG tile and converts pixels to normalized heights:

- Black maps to `0`.
- White maps to `1`.
- Intermediate values map linearly through grayscale.

Each pixel corresponds to a UV coordinate:

```text
u = x / (width - 1)
v = y / (height - 1)
```

Training samples can be drawn randomly from all pixels each step. The script should also support evaluating the full tile grid for metrics.

## Export Format

Export a JSON file with all information Unity needs to reconstruct the network.

Example shape:

```json
{
  "version": 1,
  "tileWidth": 256,
  "tileHeight": 256,
  "frequencyCount": 8,
  "hiddenWidth": 64,
  "hiddenLayers": 3,
  "activation": "relu",
  "heightMin": 0.0,
  "heightMax": 1.0,
  "layers": [
    {
      "inputSize": 34,
      "outputSize": 64,
      "weights": [],
      "bias": []
    }
  ],
  "metrics": {
    "mse": 0.0,
    "mae": 0.0,
    "maxError": 0.0,
    "sourceBytes": 0,
    "modelBytes": 0,
    "compressionRatio": 0.0
  }
}
```

Weights are stored row-major per output neuron:

```text
weights[outputIndex * inputSize + inputIndex]
```

This makes the Unity evaluator straightforward and avoids ambiguity.

## Python Components

Create a training script that can be run from the project root.

Responsibilities:

- Load a PNG tile path supplied by the user.
- Build the Fourier MLP with configurable frequency count, hidden width, hidden layers, learning rate, steps, and batch size.
- Train on random UV-height batches.
- Evaluate full-grid MSE, MAE, and max error after training.
- Export model JSON.
- Optionally export a reconstructed preview PNG next to the JSON.

The first implementation can live under:

```text
Tools/NeuralHeightmap/train_height_tile.py
```

## Unity Components

Create Unity-side code under:

```text
Assets/高度图AI/Runtime
Assets/高度图AI/Editor
```

Runtime component:

```csharp
public sealed class NeuralHeightmapModel
{
    public static NeuralHeightmapModel FromJson(string json);
    public float EvaluateHeight(Vector2 uv);
}
```

The runtime evaluator:

- Parses the JSON.
- Rebuilds the Fourier encoding.
- Runs each linear layer.
- Applies ReLU after hidden layers.
- Returns a clamped height in `0..1`.

Editor helper:

- Allows selecting a model JSON file.
- Allows entering a UV coordinate.
- Displays the decoded height.
- Optionally rebuilds a `256x256` grayscale preview texture from the model.

## Validation

Training validation:

1. Train on one exported height tile.
2. Confirm the script prints MSE, MAE, max error, PNG size, JSON size, and compression ratio.
3. Confirm the optional reconstructed preview PNG is visually close to the source tile.

Unity validation:

1. Load the exported model JSON.
2. Query several UV coordinates and confirm heights are in `0..1`.
3. Reconstruct a preview texture from the Unity evaluator.
4. Compare Python preview and Unity preview visually. They should match closely.

## Follow-Up Features

After version 1 works:

- Train one model per tile for a full 2048 or 4096 heightmap.
- Add a tile manifest mapping global UV to tile JSON files.
- Convert JSON to compact binary `.bytes`.
- Add Compute Shader batched decoding.
- Add Sentis or ONNX path for comparison.
- Explore a shared decoder plus per-tile latent vectors for better compression.
