using System;
using UnityEditor;
using UnityEngine;

public sealed class CubemapSphericalHarmonicsWindow : EditorWindow
{
    private Cubemap cubemap;
    private Vector3[] coefficients;
    private string formattedResult;
    private string statusMessage = "Assign a Cubemap and click Calculate.";
    private MessageType statusType = MessageType.Info;
    private Vector2 scrollPosition;

    [MenuItem("Tools/渲染工具/Cubemap 球谐计算器")]
    private static void Open()
    {
        GetWindow<CubemapSphericalHarmonicsWindow>("Cubemap 球谐");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("三阶球谐漫反射光照", EditorStyles.boldLabel);
        cubemap = (Cubemap)EditorGUILayout.ObjectField("Cubemap", cubemap, typeof(Cubemap), false);

        using (new EditorGUI.DisabledScope(cubemap == null))
        {
            if (GUILayout.Button("计算并打印", GUILayout.Height(30f)))
            {
                Calculate();
            }
        }

        EditorGUILayout.HelpBox(statusMessage, statusType);
        if (coefficients != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cosine-convolved irradiance coefficients (linear RGB)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < coefficients.Length; i++)
                {
                    EditorGUILayout.Vector3Field("SH" + i, coefficients[i]);
                }
            }

            EditorGUILayout.LabelField("Console / shader format", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(formattedResult, EditorStyles.textArea, GUILayout.MinHeight(170f));

            if (GUILayout.Button("创建校验球", GUILayout.Height(30f)))
            {
                CreateValidationSphere();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Calculate()
    {
        if (cubemap == null)
        {
            SetStatus("Cubemap is missing.", MessageType.Error);
            return;
        }

        TextureImporter importer = null;
        string assetPath = AssetDatabase.GetAssetPath(cubemap);
        bool restoreReadable = false;
        bool originalReadable = false;
        Exception calculationException = null;

        try
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    originalReadable = importer.isReadable;
                    restoreReadable = true;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(assetPath);
                }
            }

            coefficients = CubemapSphericalHarmonics.Calculate(cubemap);
            formattedResult = CubemapSphericalHarmonics.Format(coefficients);
            Debug.Log("Cubemap diffuse SH coefficients (L0-L2):\n" + formattedResult, cubemap);
            SetStatus("Calculation complete. Coefficients were printed to the Console.", MessageType.Info);
        }
        catch (Exception exception)
        {
            calculationException = exception;
            coefficients = null;
            formattedResult = null;
            SetStatus(exception.Message, MessageType.Error);
            Debug.LogException(exception, cubemap);
        }
        finally
        {
            if (restoreReadable && importer != null)
            {
                try
                {
                    importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                    {
                        throw new InvalidOperationException("The Cubemap importer could not be reloaded.");
                    }

                    importer.isReadable = originalReadable;
                    importer.SaveAndReimport();
                }
                catch (Exception restoreException)
                {
                    string prefix = calculationException == null
                        ? "Calculation finished, but restoring Cubemap Read/Write failed: "
                        : calculationException.Message + " Restoring Cubemap Read/Write also failed: ";
                    SetStatus(prefix + restoreException.Message, calculationException == null ? MessageType.Warning : MessageType.Error);
                    Debug.LogException(restoreException, cubemap);
                }
            }
        }
    }

    private void CreateValidationSphere()
    {
        if (coefficients == null || coefficients.Length != 9)
        {
            SetStatus("Calculate the Cubemap SH coefficients first.", MessageType.Error);
            return;
        }

        Shader shader = Shader.Find("SHCalculation/Validation Sphere");
        if (shader == null)
        {
            SetStatus("Validation Shader was not found. Reimport SHValidationSphere.shader.", MessageType.Error);
            return;
        }

        Vector3 position = Selection.activeGameObject != null
            ? Selection.activeGameObject.transform.position
            : Vector3.zero;
        GameObject sphere = null;
        Material material = null;

        try
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SH Validation Sphere";
            sphere.transform.position = position;

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer == null)
            {
                throw new InvalidOperationException("The validation Sphere has no Renderer.");
            }

            material = new Material(shader)
            {
                name = "SH Validation Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            SHValidationMaterial.Apply(material, coefficients);
            sphereRenderer.sharedMaterial = material;

            Undo.RegisterCreatedObjectUndo(sphere, "Create SH Validation Sphere");
            Selection.activeGameObject = sphere;
            SetStatus("Validation Sphere created at " + position + ".", MessageType.Info);
        }
        catch (Exception exception)
        {
            if (sphere != null)
            {
                DestroyImmediate(sphere);
            }

            if (material != null)
            {
                DestroyImmediate(material);
            }

            SetStatus(exception.Message, MessageType.Error);
            Debug.LogException(exception);
        }
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        Repaint();
    }
}
