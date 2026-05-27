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

        private IHeightmapSource CreateSource()
        {
            if (inputKind == InputKind.TerrainData)
            {
                return new TerrainDataHeightmapSource(terrainData);
            }

            return new TextureHeightmapSource(texture);
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
}
