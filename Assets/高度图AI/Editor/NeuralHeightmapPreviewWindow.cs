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
        private Vector2 uv;
        private float evaluatedHeight;
        private bool hasEvaluation;
        private string statusMessage = "";
        private int maxHeight = 150;
        private int uSamples = 16;
        private int vSamples = 16;
        private MessageType statusType = MessageType.Info;
        private const string SampleRootName = "Neural Heightmap Samples";

        [MenuItem("Tools/Heightmap AI/Neural Heightmap Preview")]
        private static void Open()
        {
            GetWindow<NeuralHeightmapPreviewWindow>("Neural Heightmap Preview");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            TextAsset selectedModelJson = (TextAsset)EditorGUILayout.ObjectField("JSON", modelJson, typeof(TextAsset), false);
            if (selectedModelJson != modelJson)
            {
                modelJson = selectedModelJson;
                model = null;
                hasEvaluation = false;
                statusMessage = "";
            }

            if (GUILayout.Button("Load Model", GUILayout.Height(28)))
            {
                LoadModel();
            }

            using (new EditorGUI.DisabledScope(model == null))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Evaluate", EditorStyles.boldLabel);
                uv = EditorGUILayout.Vector2Field("UV", uv);
                if (GUILayout.Button("Evaluate Height", GUILayout.Height(28)))
                {
                    EvaluateHeight();
                }

                maxHeight = EditorGUILayout.IntField("MaxHeight", maxHeight);
                using (new EditorGUI.DisabledScope(!hasEvaluation))
                {
                    EditorGUILayout.FloatField("Height", evaluatedHeight*maxHeight);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                if (GUILayout.Button("Save Reconstructed Preview PNG", GUILayout.Height(32)))
                {
                    SaveReconstructedPreview();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scene Samples", EditorStyles.boldLabel);
                uSamples = EditorGUILayout.IntField("U Samples", uSamples);
                vSamples = EditorGUILayout.IntField("V Samples", vSamples);
                if (GUILayout.Button("Create Cube Samples", GUILayout.Height(32)))
                {
                    CreateCubeSamples();
                }
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void LoadModel()
        {
            try
            {
                if (modelJson == null)
                {
                    throw new InvalidOperationException("Model JSON asset is missing.");
                }

                bool isBinary = NeuralHeightmapModel.LooksLikeBinary(modelJson.bytes);
                model = isBinary
                    ? NeuralHeightmapModel.FromBytes(modelJson.bytes)
                    : NeuralHeightmapModel.FromJson(modelJson.text);
                hasEvaluation = false;
                statusType = MessageType.Info;
                statusMessage = $"Loaded {((isBinary) ? "binary" : "json")} model {model.TileWidth}x{model.TileHeight}. MAE: {model.Mae:0.######}, Max Error: {model.MaxError:0.######}.";
            }
            catch (Exception exception)
            {
                model = null;
                hasEvaluation = false;
                statusType = MessageType.Error;
                statusMessage = "Load failed: " + exception.Message;
            }
            finally
            {
                Repaint();
            }
        }

        private void EvaluateHeight()
        {
            try
            {
                if (model == null)
                {
                    throw new InvalidOperationException("Load a model before evaluating.");
                }

                evaluatedHeight = model.EvaluateHeight(uv);
                hasEvaluation = true;
                statusType = MessageType.Info;
                statusMessage = $"Evaluated height at UV ({uv.x:0.###}, {uv.y:0.###}).";
            }
            catch (Exception exception)
            {
                hasEvaluation = false;
                statusType = MessageType.Error;
                statusMessage = "Evaluate failed: " + exception.Message;
            }
            finally
            {
                Repaint();
            }
        }

        private void SaveReconstructedPreview()
        {
            Texture2D texture = null;

            try
            {
                if (model == null)
                {
                    throw new InvalidOperationException("Load a model before saving a preview.");
                }

                string defaultName = modelJson != null && !string.IsNullOrEmpty(modelJson.name)
                    ? modelJson.name + "_reconstructed.png"
                    : "neural_heightmap_reconstructed.png";
                string path = EditorUtility.SaveFilePanel("Save Reconstructed Preview PNG", "", defaultName, "png");
                if (string.IsNullOrEmpty(path))
                {
                    statusType = MessageType.Info;
                    statusMessage = "Save cancelled.";
                    return;
                }

                texture = model.ReconstructTexture();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.Refresh();

                statusType = MessageType.Info;
                statusMessage = "Saved reconstructed preview PNG to " + path;
            }
            catch (Exception exception)
            {
                statusType = MessageType.Error;
                statusMessage = "Save failed: " + exception.Message;
            }
            finally
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }

                Repaint();
            }
        }

        private void CreateCubeSamples()
        {
            try
            {
                if (model == null)
                {
                    throw new InvalidOperationException("Load a model before creating cube samples.");
                }

                if (uSamples <= 0 || vSamples <= 0)
                {
                    throw new InvalidOperationException("U Samples and V Samples must be greater than zero.");
                }

                GameObject existing = GameObject.Find(SampleRootName);
                if (existing != null)
                {
                    DestroyImmediate(existing);
                }

                GameObject root = new GameObject(SampleRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Neural Heightmap Samples");

                int created = 0;
                for (int vIndex = 0; vIndex < vSamples; vIndex++)
                {
                    float v = vSamples == 1 ? 0f : (float)vIndex / (vSamples - 1);
                    for (int uIndex = 0; uIndex < uSamples; uIndex++)
                    {
                        float u = uSamples == 1 ? 0f : (float)uIndex / (uSamples - 1);
                        float height = model.EvaluateHeight(new Vector2(u, v));
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = $"Sample_u{uIndex:000}_v{vIndex:000}";
                        cube.transform.SetParent(root.transform);
                        cube.transform.position = new Vector3(u * 1000f, height * maxHeight, v * 1000f);
                        Undo.RegisterCreatedObjectUndo(cube, "Create Neural Heightmap Sample Cube");
                        created++;
                    }
                }

                Selection.activeGameObject = root;
                statusType = MessageType.Info;
                statusMessage = $"Created {created} cube samples.";
            }
            catch (Exception exception)
            {
                statusType = MessageType.Error;
                statusMessage = "Create samples failed: " + exception.Message;
            }
            finally
            {
                Repaint();
            }
        }
    }
}
