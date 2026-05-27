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
