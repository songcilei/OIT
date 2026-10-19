#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCCMechanicalArm.Editor
{
    public static class PCCArmDemoMenu
    {
        [MenuItem("Tools/PCC Mechanical Arm/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        // Also used by the headless validation harness. No runtime/editor package dependencies.
        public static PCCArm BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string directory = "Assets/PCCMechanicalArmDemo";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets", "PCCMechanicalArmDemo");
            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline == null
                ? "Standard" : "Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("This demo supports Built-in or URP. Assign compatible materials for your pipeline.");
            Material shell = MakeMaterial(directory, "Shell", shader, new Color(0.42f, 0.53f, 0.62f), 0.85f, 0.65f);
            Material core = MakeMaterial(directory, "Core", shader, new Color(0.075f, 0.105f, 0.13f), 0.8f, 0.48f);
            Material targetMaterial = MakeMaterial(directory, "Target", shader, new Color(0.1f, 0.95f, 0.65f), 0.2f, 0.4f);
            Material baseMaterial = MakeMaterial(directory, "Base", shader, new Color(0.10f, 0.15f, 0.21f), 0.5f, 0.4f);
            Material floorMaterial = MakeMaterial(directory, "Floor", shader, new Color(0.055f, 0.075f, 0.10f), 0.05f, 0.25f);

            var armObject = new GameObject("PCC Arm - Local +Z forward");
            armObject.transform.position = new Vector3(0f, 1.15f, 0f);
            PCCArm arm = armObject.AddComponent<PCCArm>();
            arm.shellMaterial = shell; arm.coreMaterial = core;

            GameObject target = Primitive("IK Target - move me", PrimitiveType.Sphere,
                armObject.transform.TransformPoint(new Vector3(0.9f, 0.6f, 2.75f)),
                Vector3.one * 0.14f, targetMaterial);
            var motion = target.AddComponent<PCCDemoTarget>();
            motion.armBase = arm.transform;
            arm.target = target.transform;

            GameObject tip = new GameObject("Tip Attachment (local +Z is tool forward)");
            tip.transform.SetParent(arm.transform, false);
            arm.tipMarker = tip.transform;
            // A small two-finger tool for orientation/attachment demonstration (no grasp physics).
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject finger = Primitive("Gripper Finger", PrimitiveType.Cube,
                    Vector3.zero, new Vector3(0.035f, 0.06f, 0.18f), shell);
                finger.transform.SetParent(tip.transform, false);
                finger.transform.localPosition = new Vector3(i * 0.075f, 0f, 0.08f);
                finger.transform.localRotation = Quaternion.Euler(0f, -i * 16f, 0f);
            }
            GameObject mount = Primitive("Arm Mount", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, -0.08f), new Vector3(0.44f, 0.11f, 0.44f), baseMaterial);
            mount.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive("Pedestal", PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.12f),
                new Vector3(0.32f, 1.1f, 0.32f), baseMaterial);
            Primitive("Floor", PrimitiveType.Plane, Vector3.zero,
                new Vector3(1.2f, 1f, 1.2f), floorMaterial);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(5.8f, 4.3f, 6.4f);
            camera.transform.LookAt(new Vector3(0f, 1.45f, 1.6f));
            camera.fieldOfView = 43f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.nearClipPlane = 0.05f; camera.farClipPlane = 50f;

            Light light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.45f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.65f;
            fill.color = new Color(0.55f, 0.73f, 1f);
            fill.transform.rotation = Quaternion.Euler(30f, 140f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.48f);

            // Place the static attachment at the initial tip before the first Play update.
            PCCSolver solver = new PCCSolver(arm.sections);
            Vector3 p; Quaternion r;
            solver.EvaluatePose(solver.SectionCount - 1, 1f, out p, out r);
            tip.transform.SetPositionAndRotation(arm.transform.TransformPoint(p), arm.transform.rotation * r);
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/PCCArmDemo.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = armObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 1.45f, 1.5f), Quaternion.Euler(22f, -140f, 0f), 5f);
            AssetDatabase.SaveAssets();
            Debug.Log("PCC demo saved: " + path + ". Press Play. Disable the target's Animate toggle to drag it manually.");
            return arm;
        }

        private static Material MakeMaterial(string directory, string name, Shader shader, Color color,
            float metal, float smooth)
        {
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smooth);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(directory + "/" + name + ".mat"));
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }
    }
}
#endif
