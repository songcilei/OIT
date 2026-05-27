# Neural Heightmap Tile Training

Train one grayscale height tile into a JSON neural model:

```powershell
py Tools/NeuralHeightmap/train_height_tile.py `
  --input "path/to/height_tile.png" `
  --output "Temp/NeuralHeightmapSmoke/height_tile.model.json" `
  --preview "Temp/NeuralHeightmapSmoke/height_tile.preview.png" `
  --steps 3000
```

Export JSON and binary:

```powershell
py Tools/NeuralHeightmap/train_height_tile.py `
  --input "path/to/height_tile.png" `
  --output "Temp/NeuralHeightmapSmoke/height_tile.model.json" `
  --binary-output "Temp/NeuralHeightmapSmoke/height_tile.model.bytes" `
  --preview "Temp/NeuralHeightmapSmoke/height_tile.preview.png" `
  --steps 3000
```

Install dependencies if needed:

```powershell
py -m pip install torch pillow numpy
```

The JSON model is meant to be loaded by Unity `NeuralHeightmapModel`.
