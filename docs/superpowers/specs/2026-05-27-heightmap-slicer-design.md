# Heightmap Slicer Design

## Goal

Build a Unity Editor tool that slices terrain heightmaps into fixed-size grayscale PNG tiles for machine learning training data.

The first version should be simple, repeatable, and useful inside the Unity project:

- Accept either a Unity `TerrainData` asset/object or a readable grayscale `Texture2D`.
- Export non-overlapping PNG tiles with a fixed pixel width and height.
- Discard edge areas that are smaller than a full tile.
- Save tiles to a user-selected local output folder.

## Out Of Scope For Version 1

- Overlapping tiles or custom stride.
- Edge padding.
- RAW, NumPy, CSV, JSON, or 16-bit output.
- Dataset train/validation/test splitting.
- Batch processing multiple inputs at once.

These are likely follow-up features, so the first version should avoid designs that make them hard to add later.

## User Workflow

The tool appears in the Unity menu as `Tools/Heightmap AI/Slice Heightmap`.

The editor window lets the user:

1. Choose the input type: `TerrainData` or `Texture2D`.
2. Assign the selected input asset/object.
3. Set tile width and height in pixels.
4. Choose an output folder.
5. Click an export button.

When export succeeds, the tool writes PNG files named with a stable pattern:

```text
<sourceName>_y000_x000.png
<sourceName>_y000_x001.png
<sourceName>_y001_x000.png
```

The `y` and `x` indices refer to the tile row and column in the input heightmap.

## Architecture

Create the implementation under `Assets/高度图AI/Editor`.

### `HeightmapSliceWindow`

Unity `EditorWindow` responsible for:

- Drawing the UI.
- Holding editor-only state.
- Validating user parameters before export.
- Opening the output folder picker.
- Showing progress and final status.
- Calling the processor.
- Clearing the Unity progress bar in all exit paths.
- Refreshing `AssetDatabase` after export.

The window should keep the processing logic thin and delegate data conversion and file writing.

### `IHeightmapSource`

A small source abstraction used by the processor:

```csharp
public interface IHeightmapSource
{
    string Name { get; }
    int Width { get; }
    int Height { get; }
    float Sample(int x, int y);
}
```

`Sample` returns a normalized height value in the `0..1` range.

Two implementations are needed:

- `TerrainDataHeightmapSource`: wraps `TerrainData`, reads heights via `GetHeights`, and exposes the heightmap resolution.
- `TextureHeightmapSource`: wraps readable `Texture2D`, samples pixel grayscale values, and exposes texture width and height.

### `HeightmapSliceProcessor`

Pure processing class responsible for:

- Computing complete tile counts with integer division.
- Creating a `Texture2D` for each tile.
- Filling tile pixels from the source samples.
- Encoding tiles as PNG.
- Writing files to the output directory.
- Reporting progress through a callback supplied by the editor window.

The processor returns a summary result containing at least:

- Tile count written.
- Tile columns and rows.
- Output directory.

## Data Handling

All input heights are normalized to grayscale PNG values:

- Height `0` becomes black.
- Height `1` becomes white.
- Values are clamped to `0..1` before writing.

Version 1 uses Unity PNG encoding through `Texture2D.EncodeToPNG`. This favors usability and visual inspection over maximum height precision.

Terrain and texture sources should use the same tile slicing path so output naming and tile count behavior stay consistent.

## Error Handling

The tool should show clear editor errors and avoid partial confusing behavior for:

- Missing input.
- Tile width or height less than or equal to zero.
- Missing output folder.
- Output folder cannot be created or written.
- Input width or height smaller than the requested tile size.
- `Texture2D` is not readable.

If export fails midway, already written PNG files can remain on disk. The error message should report the failure instead of silently claiming success.

## Verification

Manual verification for version 1:

1. Use a readable `Texture2D` heightmap, export fixed-size PNG tiles, and confirm file count, names, and dimensions.
2. Use a `TerrainData` source and confirm tile count matches heightmap resolution divided by tile size.
3. Confirm invalid inputs show clear errors:
   - No input.
   - Illegal tile size.
   - Output path missing or invalid.
   - Source smaller than tile size.
   - Unreadable texture.

If Unity editor test infrastructure is available in the project, add focused tests around processor tile-count behavior and source sampling. If not, keep the processor isolated enough that tests can be added later without touching the editor UI.
