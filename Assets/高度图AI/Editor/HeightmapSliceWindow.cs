using System;
using System.Collections.Generic;
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
        private Terrain terrain;
        private Texture2D texture;
        private int tileWidth = 256;
        private int tileHeight = 256;
        private string outputDirectory = "";
        private bool bakeGameObjects;
        private readonly List<GameObject> overlayObjects = new List<GameObject>();
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
                terrain = (Terrain)EditorGUILayout.ObjectField("Terrain Object", terrain, typeof(Terrain), true);
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
            DrawObjectOverlaySection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputDirectory = EditorGUILayout.TextField("Folder", outputDirectory);
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
            try
            {
                IHeightmapSource source = CreateSource();
                HeightmapObjectOverlaySettings overlaySettings = CreateObjectOverlaySettings(source);
                var processor = new HeightmapSliceProcessor();
                HeightmapSliceResult result = processor.ExportPngTiles(
                    source,
                    tileWidth,
                    tileHeight,
                    outputDirectory,
                    overlaySettings,
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

        private IHeightmapSource CreateSource()
        {
            if (inputKind == InputKind.TerrainData)
            {
                return new TerrainDataHeightmapSource(terrainData);
            }

            return new TextureHeightmapSource(texture);
        }

        private void DrawObjectOverlaySection()
        {
            EditorGUILayout.LabelField("Object Overlay", EditorStyles.boldLabel);
            bakeGameObjects = EditorGUILayout.Toggle("Bake GameObjects", bakeGameObjects);

            using (new EditorGUI.DisabledScope(!bakeGameObjects))
            {
                if (inputKind != InputKind.TerrainData)
                {
                    EditorGUILayout.HelpBox("GameObject overlay needs TerrainData plus a Terrain Object for world-space height conversion.", MessageType.Info);
                }

                for (int i = 0; i < overlayObjects.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    overlayObjects[i] = (GameObject)EditorGUILayout.ObjectField($"Object {i}", overlayObjects[i], typeof(GameObject), true);
                    if (GUILayout.Button("-", GUILayout.Width(28)))
                    {
                        overlayObjects.RemoveAt(i);
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Object"))
                {
                    overlayObjects.Add(null);
                }

                if (GUILayout.Button("Clear"))
                {
                    overlayObjects.Clear();
                }
                EditorGUILayout.EndHorizontal();

                Rect dropArea = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drag GameObjects Here");
                HandleObjectDrop(dropArea);
            }
        }

        private void HandleObjectDrop(Rect dropArea)
        {
            Event current = Event.current;
            if (!dropArea.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (UnityEngine.Object dragged in DragAndDrop.objectReferences)
                {
                    if (dragged is GameObject gameObject && !overlayObjects.Contains(gameObject))
                    {
                        overlayObjects.Add(gameObject);
                    }
                }
            }

            current.Use();
        }

        private HeightmapObjectOverlaySettings CreateObjectOverlaySettings(IHeightmapSource source)
        {
            if (!bakeGameObjects)
            {
                return HeightmapObjectOverlaySettings.Disabled;
            }

            if (inputKind != InputKind.TerrainData)
            {
                throw new HeightmapSliceException("GameObject overlay is only available when Source Type is TerrainData.");
            }

            Terrain resolvedTerrain = ResolveTerrain();
            if (resolvedTerrain == null)
            {
                throw new HeightmapSliceException("Terrain Object is missing. Assign the scene Terrain that uses this TerrainData.");
            }

            if (resolvedTerrain.terrainData != terrainData)
            {
                throw new HeightmapSliceException("Terrain Object must use the selected TerrainData.");
            }

            var validObjects = new List<GameObject>();
            for (int i = 0; i < overlayObjects.Count; i++)
            {
                if (overlayObjects[i] != null)
                {
                    validObjects.Add(overlayObjects[i]);
                }
            }

            if (validObjects.Count == 0)
            {
                throw new HeightmapSliceException("Bake GameObjects is enabled, but no GameObjects were assigned.");
            }

            return HeightmapObjectOverlaySettings.FromGameObjects(
                resolvedTerrain.transform.position,
                resolvedTerrain.terrainData.size,
                source.Width,
                source.Height,
                validObjects);
        }

        private Terrain ResolveTerrain()
        {
            if (terrain != null)
            {
                return terrain;
            }

            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (Terrain candidate in terrains)
            {
                if (candidate != null && candidate.terrainData == terrainData)
                {
                    terrain = candidate;
                    return candidate;
                }
            }

            return null;
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

    internal readonly struct OverlayHit
    {
        public OverlayHit(Vector2Int coordinate, float height)
        {
            Coordinate = coordinate;
            Height = Mathf.Clamp01(height);
        }

        public Vector2Int Coordinate { get; }
        public float Height { get; }
    }

    internal sealed class HeightmapObjectOverlaySettings
    {
        private readonly Dictionary<Vector2Int, float> precomputedHits;
        private readonly Collider[] colliders;
        private readonly List<MeshCollider> temporaryColliders;

        public HeightmapObjectOverlaySettings(
            bool enabled,
            Vector3 terrainPosition,
            Vector3 terrainSize,
            IEnumerable<OverlayHit> precomputedHits)
        {
            Enabled = enabled;
            TerrainPosition = terrainPosition;
            TerrainSize = terrainSize;
            this.precomputedHits = new Dictionary<Vector2Int, float>();
            colliders = Array.Empty<Collider>();
            temporaryColliders = new List<MeshCollider>();

            if (precomputedHits == null)
            {
                return;
            }

            foreach (OverlayHit hit in precomputedHits)
            {
                this.precomputedHits[hit.Coordinate] = hit.Height;
            }
        }

        private HeightmapObjectOverlaySettings(
            Vector3 terrainPosition,
            Vector3 terrainSize,
            Collider[] colliders,
            List<MeshCollider> temporaryColliders)
        {
            Enabled = true;
            TerrainPosition = terrainPosition;
            TerrainSize = terrainSize;
            precomputedHits = null;
            this.colliders = colliders;
            this.temporaryColliders = temporaryColliders;
        }

        public static HeightmapObjectOverlaySettings Disabled { get; } =
            new HeightmapObjectOverlaySettings(false, Vector3.zero, Vector3.one, Array.Empty<OverlayHit>());

        public bool Enabled { get; }
        public Vector3 TerrainPosition { get; }
        public Vector3 TerrainSize { get; }

        public static HeightmapObjectOverlaySettings FromGameObjects(
            Vector3 terrainPosition,
            Vector3 terrainSize,
            int width,
            int height,
            IReadOnlyList<GameObject> gameObjects)
        {
            var temporaryColliders = new List<MeshCollider>();
            var colliderList = new List<Collider>();

            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                colliderList.AddRange(gameObject.GetComponentsInChildren<Collider>());
                MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (meshFilter.GetComponent<Collider>() != null)
                    {
                        continue;
                    }

                    MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    temporaryColliders.Add(meshCollider);
                    colliderList.Add(meshCollider);
                }
            }

            if (colliderList.Count == 0)
            {
                DisposeTemporaryColliders(temporaryColliders);
                throw new HeightmapSliceException("Assigned GameObjects do not contain Collider or MeshFilter components.");
            }

            Physics.SyncTransforms();
            return new HeightmapObjectOverlaySettings(terrainPosition, terrainSize, colliderList.ToArray(), temporaryColliders);
        }

        public bool TrySampleObjectHeight(IHeightmapSource source, int x, int y, out float height)
        {
            height = 0f;
            if (!Enabled)
            {
                return false;
            }

            if (precomputedHits != null)
            {
                return precomputedHits.TryGetValue(new Vector2Int(x, y), out height);
            }

            if (colliders == null || colliders.Length == 0)
            {
                return false;
            }

            float u = source.Width <= 1 ? 0f : (float)x / (source.Width - 1);
            float v = source.Height <= 1 ? 0f : (float)y / (source.Height - 1);
            Vector3 origin = new Vector3(
                TerrainPosition.x + u * TerrainSize.x,
                TerrainPosition.y + TerrainSize.y + 1000f,
                TerrainPosition.z + v * TerrainSize.z);
            var ray = new Ray(origin, Vector3.down);
            float maxDistance = TerrainSize.y + 2000f;

            bool hasHit = false;
            float highestWorldY = float.NegativeInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (collider.Raycast(ray, out RaycastHit hit, maxDistance) && hit.point.y > highestWorldY)
                {
                    highestWorldY = hit.point.y;
                    hasHit = true;
                }
            }

            if (!hasHit)
            {
                return false;
            }

            height = Mathf.Clamp01((highestWorldY - TerrainPosition.y) / TerrainSize.y);
            return true;
        }

        public void Dispose()
        {
            DisposeTemporaryColliders(temporaryColliders);
        }

        private static void DisposeTemporaryColliders(List<MeshCollider> collidersToDispose)
        {
            if (collidersToDispose == null)
            {
                return;
            }

            for (int i = 0; i < collidersToDispose.Count; i++)
            {
                if (collidersToDispose[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(collidersToDispose[i]);
                }
            }

            collidersToDispose.Clear();
        }
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
            HeightmapObjectOverlaySettings overlaySettings,
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

            try
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        progress?.Invoke((float)written / total, $"Writing tile {written + 1} / {total}");
                        WriteTile(source, tileWidth, tileHeight, row, column, safeSourceName, outputDirectory, overlaySettings);
                        written++;
                    }
                }
            }
            finally
            {
                overlaySettings?.Dispose();
            }

            progress?.Invoke(1f, $"Wrote {written} PNG tiles.");
            return new HeightmapSliceResult(written, columns, rows, outputDirectory);
        }

        public static float SampleWithOverlay(IHeightmapSource source, int x, int y, HeightmapObjectOverlaySettings overlaySettings)
        {
            float terrainHeight = Mathf.Clamp01(source.Sample(x, y));
            if (overlaySettings == null || !overlaySettings.Enabled)
            {
                return terrainHeight;
            }

            return overlaySettings.TrySampleObjectHeight(source, x, y, out float objectHeight)
                ? Mathf.Max(terrainHeight, objectHeight)
                : terrainHeight;
        }

        private static void WriteTile(
            IHeightmapSource source,
            int tileWidth,
            int tileHeight,
            int row,
            int column,
            string safeSourceName,
            string outputDirectory,
            HeightmapObjectOverlaySettings overlaySettings)
        {
            Texture2D tile = null;
            try
            {
                tile = new Texture2D(tileWidth, tileHeight, TextureFormat.RGBA32, false, true);

                for (int y = 0; y < tileHeight; y++)
                {
                    for (int x = 0; x < tileWidth; x++)
                    {
                        int sourceX = column * tileWidth + x;
                        int sourceY = row * tileHeight + y;
                        float height = SampleWithOverlay(source, sourceX, sourceY, overlaySettings);
                        tile.SetPixel(x, y, new Color(height, height, height, 1f));
                    }
                }

                tile.Apply(false, false);
                byte[] png = tile.EncodeToPNG();

                string fileName = $"{safeSourceName}_y{row:000}_x{column:000}.png";
                string filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, png);
            }
            finally
            {
                if (tile != null)
                {
                    UnityEngine.Object.DestroyImmediate(tile);
                }
            }
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
