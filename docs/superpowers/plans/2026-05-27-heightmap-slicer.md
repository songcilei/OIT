# Heightmap Slicer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Unity Editor tool that slices `TerrainData` or readable grayscale `Texture2D` heightmaps into fixed-size, non-overlapping PNG tiles for machine learning training data.

**Architecture:** Add one editor-only C# file under `Assets/高度图AI/Editor` containing a thin `EditorWindow`, source adapters for `TerrainData` and `Texture2D`, and a reusable processor. The UI validates user input and delegates all slicing/file writing to the processor.

**Tech Stack:** Unity Editor C#, `UnityEditor.EditorWindow`, `UnityEngine.TerrainData`, `UnityEngine.Texture2D`, `Texture2D.EncodeToPNG`, `System.IO`.

---

## File Structure

- Create: `Assets/高度图AI/Editor/HeightmapSliceWindow.cs`
  - Contains `HeightmapSliceWindow`, `IHeightmapSource`, `TerrainDataHeightmapSource`, `TextureHeightmapSource`, `HeightmapSliceProcessor`, `HeightmapSliceResult`, and `HeightmapSliceException`.
  - Kept in `Editor` so it is excluded from runtime builds.
- Create by Unity or agent if needed: `Assets/高度图AI/Editor.meta`
  - Folder meta for Unity asset tracking.
- Verify existing untracked: `Assets/高度图AI.meta`
  - This already exists in the working tree. Include it only if needed to keep the new folder valid in Unity.

---

### Task 1: Create Editor Folder And Script Skeleton

**Files:**
- Create: `Assets/高度图AI/Editor/HeightmapSliceWindow.cs`
- Create if absent: `Assets/高度图AI/Editor.meta`

- [ ] **Step 1: Create the editor folder**

Run:

```cmd
mkdir "Assets\高度图AI\Editor"
```

Expected: The folder exists. If it already exists, `cmd` may print that the subdirectory already exists; continue.

- [ ] **Step 2: Add the script skeleton**

Create `Assets/高度图AI/Editor/HeightmapSliceWindow.cs` with this initial content:

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeightmapAI.Editor
{
    public sealed class HeightmapSliceWindow : EditorWindow
    {
        private enum InputKind
        {
            TerrainData,
            Texture2D
        }

        private InputKind inputKind;
        private TerrainData terrainData;
        private Texture2D texture;
        private int tileWidth = 256;
        private int tileHeight = 256;
        private string outputDirectory = "";
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Heightmap AI/Slice Heightmap")]
        private static void Open()
        {
            GetWindow<HeightmapSliceWindow>("Heightmap Slicer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            inputKind = (InputKind)EditorGUILayout.EnumPopup("Source Type", inputKind);

            if (inputKind == InputKind.TerrainData)
            {
                terrainData = (TerrainData)EditorGUILayout.ObjectField("Terrain Data", terrainData, typeof(TerrainData), false);
            }
            else
            {
                texture = (Texture2D)EditorGUILayout.ObjectField("Texture", texture, typeof(Texture2D), false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tiles", EditorStyles.boldLabel);
            tileWidth = EditorGUILayout.IntField("Tile Width", tileWidth);
            tileHeight = EditorGUILayout.IntField("Tile Height", tileHeight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Folder", outputDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Output Folder", outputDirectory, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    outputDirectory = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Export PNG Tiles", GUILayout.Height(32)))
            {
                Export();
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void Export()
        {
        }
    }

    internal interface IHeightmapSource
    {
        string Name { get; }
        int Width { get; }
        int Height { get; }
        float Sample(int x, int y);
    }

    internal sealed class HeightmapSliceException : Exception
    {
        public HeightmapSliceException(string message) : base(message)
        {
        }
    }

    internal readonly struct HeightmapSliceResult
    {
        public HeightmapSliceResult(int tileCount, int columns, int rows, string outputDirectory)
        {
            TileCount = tileCount;
            Columns = columns;
            Rows = rows;
            OutputDirectory = outputDirectory;
        }

        public int TileCount { get; }
        public int Columns { get; }
        public int Rows { get; }
        public string OutputDirectory { get; }
    }
}
```

- [ ] **Step 3: Check compile status in Unity**

Open or focus Unity and wait for compilation.

Expected: The script compiles. The menu item `Tools/Heightmap AI/Slice Heightmap` appears and opens an empty functional window.

- [ ] **Step 4: Commit the skeleton**

Run:

```cmd
git status --short
git add "Assets/高度图AI/Editor/HeightmapSliceWindow.cs" "Assets/高度图AI.meta"
git add "Assets/高度图AI/Editor.meta"
git commit -m heightmap-slicer-editor-skeleton
```

Expected: Commit succeeds. If `Assets/高度图AI/Editor.meta` does not exist yet, omit that path from `git add`.

---

### Task 2: Implement Heightmap Sources

**Files:**
- Modify: `Assets/高度图AI/Editor/HeightmapSliceWindow.cs`

- [ ] **Step 1: Add `TerrainDataHeightmapSource` and `TextureHeightmapSource`**

Insert these classes after `HeightmapSliceResult`:

```csharp
    internal sealed class TerrainDataHeightmapSource : IHeightmapSource
    {
        private readonly TerrainData terrainData;
        private readonly float[,] heights;

        public TerrainDataHeightmapSource(TerrainData terrainData)
        {
            this.terrainData = terrainData != null
                ? terrainData
                : throw new HeightmapSliceException("TerrainData is missing.");

            Width = terrainData.heightmapResolution;
            Height = terrainData.heightmapResolution;
            heights = terrainData.GetHeights(0, 0, Width, Height);
        }

        public string Name => string.IsNullOrEmpty(terrainData.name) ? "TerrainData" : terrainData.name;
        public int Width { get; }
        public int Height { get; }

        public float Sample(int x, int y)
        {
            return Mathf.Clamp01(heights[y, x]);
        }
    }

    internal sealed class TextureHeightmapSource : IHeightmapSource
    {
        private readonly Texture2D texture;

        public TextureHeightmapSource(Texture2D texture)
        {
            this.texture = texture != null
                ? texture
                : throw new HeightmapSliceException("Texture2D is missing.");

            Width = texture.width;
            Height = texture.height;

            try
            {
                texture.GetPixel(0, 0);
            }
            catch (UnityException exception)
            {
                throw new HeightmapSliceException("Texture2D is not readable. Enable Read/Write in the texture import settings. " + exception.Message);
            }
        }

        public string Name => string.IsNullOrEmpty(texture.name) ? "Texture2D" : texture.name;
        public int Width { get; }
        public int Height { get; }

        public float Sample(int x, int y)
        {
            return Mathf.Clamp01(texture.GetPixel(x, y).grayscale);
        }
    }
```

- [ ] **Step 2: Add source creation in the window**

Add this method inside `HeightmapSliceWindow`, below `Export()`:

```csharp
        private IHeightmapSource CreateSource()
        {
            if (inputKind == InputKind.TerrainData)
            {
                return new TerrainDataHeightmapSource(terrainData);
            }

            return new TextureHeightmapSource(texture);
        }
```

- [ ] **Step 3: Check compile status in Unity**

Expected: Unity compiles with no C# errors.

- [ ] **Step 4: Commit source adapters**

Run:

```cmd
git status --short
git add "Assets/高度图AI/Editor/HeightmapSliceWindow.cs"
git commit -m heightmap-slicer-source-adapters
```

Expected: Commit succeeds.

---

### Task 3: Implement Processor And PNG Export

**Files:**
- Modify: `Assets/高度图AI/Editor/HeightmapSliceWindow.cs`

- [ ] **Step 1: Add the processor class**

Insert this class after the source classes:

```csharp
    internal sealed class HeightmapSliceProcessor
    {
        public HeightmapSliceResult ExportPngTiles(
            IHeightmapSource source,
            int tileWidth,
            int tileHeight,
            string outputDirectory,
            Action<float, string> progress)
        {
            if (source == null)
            {
                throw new HeightmapSliceException("Input source is missing.");
            }

            if (tileWidth <= 0 || tileHeight <= 0)
            {
                throw new HeightmapSliceException("Tile width and height must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new HeightmapSliceException("Output folder is missing.");
            }

            int columns = source.Width / tileWidth;
            int rows = source.Height / tileHeight;
            if (columns <= 0 || rows <= 0)
            {
                throw new HeightmapSliceException($"Input size {source.Width}x{source.Height} is smaller than tile size {tileWidth}x{tileHeight}.");
            }

            Directory.CreateDirectory(outputDirectory);

            string safeSourceName = MakeSafeFileName(source.Name);
            int total = columns * rows;
            int written = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    progress?.Invoke((float)written / total, $"Writing tile {written + 1} / {total}");
                    WriteTile(source, tileWidth, tileHeight, row, column, safeSourceName, outputDirectory);
                    written++;
                }
            }

            progress?.Invoke(1f, $"Wrote {written} PNG tiles.");
            return new HeightmapSliceResult(written, columns, rows, outputDirectory);
        }

        private static void WriteTile(
            IHeightmapSource source,
            int tileWidth,
            int tileHeight,
            int row,
            int column,
            string safeSourceName,
            string outputDirectory)
        {
            Texture2D tile = new Texture2D(tileWidth, tileHeight, TextureFormat.RGBA32, false, true);

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int sourceX = column * tileWidth + x;
                    int sourceY = row * tileHeight + y;
                    float height = Mathf.Clamp01(source.Sample(sourceX, sourceY));
                    tile.SetPixel(x, y, new Color(height, height, height, 1f));
                }
            }

            tile.Apply(false, false);
            byte[] png = tile.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tile);

            string fileName = $"{safeSourceName}_y{row:000}_x{column:000}.png";
            string filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllBytes(filePath, png);
        }

        private static string MakeSafeFileName(string name)
        {
            string safe = string.IsNullOrWhiteSpace(name) ? "heightmap" : name.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return safe;
        }
    }
```

- [ ] **Step 2: Check compile status in Unity**

Expected: Unity compiles with no C# errors.

- [ ] **Step 3: Commit processor**

Run:

```cmd
git status --short
git add "Assets/高度图AI/Editor/HeightmapSliceWindow.cs"
git commit -m heightmap-slicer-png-processor
```

Expected: Commit succeeds.

---

### Task 4: Wire UI Export, Progress, And Errors

**Files:**
- Modify: `Assets/高度图AI/Editor/HeightmapSliceWindow.cs`

- [ ] **Step 1: Replace empty `Export()` implementation**

Replace the empty `Export()` method inside `HeightmapSliceWindow` with:

```csharp
        private void Export()
        {
            try
            {
                IHeightmapSource source = CreateSource();
                var processor = new HeightmapSliceProcessor();
                HeightmapSliceResult result = processor.ExportPngTiles(
                    source,
                    tileWidth,
                    tileHeight,
                    outputDirectory,
                    (progress, message) => EditorUtility.DisplayProgressBar("Heightmap Slicer", message, progress));

                statusType = MessageType.Info;
                statusMessage = $"Exported {result.TileCount} PNG tiles ({result.Columns} x {result.Rows}) to {result.OutputDirectory}.";
                AssetDatabase.Refresh();
            }
            catch (HeightmapSliceException exception)
            {
                statusType = MessageType.Error;
                statusMessage = exception.Message;
            }
            catch (Exception exception)
            {
                statusType = MessageType.Error;
                statusMessage = "Export failed: " + exception.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }
```

- [ ] **Step 2: Improve output folder text field editing**

Replace this line:

```csharp
            EditorGUILayout.TextField("Folder", outputDirectory);
```

with:

```csharp
            outputDirectory = EditorGUILayout.TextField("Folder", outputDirectory);
```

- [ ] **Step 3: Add proactive tile size clamping in UI**

After the two tile `IntField` calls, add:

```csharp
            tileWidth = Mathf.Max(1, tileWidth);
            tileHeight = Mathf.Max(1, tileHeight);
```

This keeps the UI friendly while the processor still protects against invalid values if called directly.

- [ ] **Step 4: Check compile status in Unity**

Expected: Unity compiles with no C# errors. Export button now reports errors or success in the window.

- [ ] **Step 5: Commit UI wiring**

Run:

```cmd
git status --short
git add "Assets/高度图AI/Editor/HeightmapSliceWindow.cs"
git commit -m heightmap-slicer-ui-export
```

Expected: Commit succeeds.

---

### Task 5: Manual Verification In Unity

**Files:**
- No code changes expected unless verification finds a defect.

- [ ] **Step 1: Verify texture export**

In Unity:

1. Select `Tools/Heightmap AI/Slice Heightmap`.
2. Set `Source Type` to `Texture2D`.
3. Assign a readable texture, such as `Assets/shadowMap.png` if its import settings allow Read/Write.
4. Set tile size to `256 x 256`.
5. Choose an output folder outside source-controlled assets, such as `F:\UnityProject\OIT\TempHeightmapTiles`.
6. Click `Export PNG Tiles`.

Expected:

- The window reports success.
- PNG files are written with names like `<source>_y000_x000.png`.
- Every PNG has dimensions `256 x 256`.

- [ ] **Step 2: Verify unreadable texture handling**

In Unity:

1. Use a texture with Read/Write disabled.
2. Click `Export PNG Tiles`.

Expected: The window shows an error that says the texture is not readable and tells the user to enable Read/Write.

- [ ] **Step 3: Verify invalid input handling**

In Unity:

1. Clear the input object.
2. Click `Export PNG Tiles`.

Expected: The window shows `TerrainData is missing.` or `Texture2D is missing.` depending on selected input type.

- [ ] **Step 4: Verify source smaller than tile**

In Unity:

1. Assign a source smaller than the tile size, or set tile size larger than the source.
2. Click `Export PNG Tiles`.

Expected: The window shows an error containing the source size and tile size.

- [ ] **Step 5: Verify TerrainData export**

In Unity:

1. Set `Source Type` to `TerrainData`.
2. Assign a `TerrainData` asset or a terrain's data.
3. Set tile size to a divisor or partial divisor of the heightmap resolution.
4. Choose an output folder.
5. Click `Export PNG Tiles`.

Expected:

- Tile count equals `(heightmapResolution / tileWidth) * (heightmapResolution / tileHeight)` using integer division.
- PNG files are written with stable row and column names.

- [ ] **Step 6: Commit verification fixes if needed**

If any code changes were needed, run:

```cmd
git status --short
git add "Assets/高度图AI/Editor/HeightmapSliceWindow.cs"
git commit -m heightmap-slicer-verification-fixes
```

Expected: Commit succeeds only if fixes were made.

---

## Self-Review Notes

- Spec coverage: TerrainData input, Texture2D input, fixed tile size, non-overlap, edge discard, PNG output, stable naming, local folder output, progress, refresh, and clear errors are covered by Tasks 1-5.
- Out-of-scope features remain out of implementation steps: no stride, padding, RAW, NumPy, metadata, dataset split, or batch processing.
- Type consistency: `IHeightmapSource`, `HeightmapSliceResult`, `HeightmapSliceException`, and `HeightmapSliceProcessor.ExportPngTiles` are defined before use in later tasks.
