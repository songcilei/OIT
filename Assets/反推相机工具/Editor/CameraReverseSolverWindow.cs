using UnityEditor;
using UnityEngine;

namespace CameraReverseTool.Editor
{
    public sealed class CameraReverseSolverWindow : EditorWindow
    {
        private readonly struct PreviewLayout
        {
            public PreviewLayout(Rect viewportRect, Rect imageRect)
            {
                ViewportRect = viewportRect;
                ImageRect = imageRect;
            }

            public Rect ViewportRect { get; }
            public Rect ImageRect { get; }
        }

        private enum SolveMode
        {
            Plane4Points,
            Cube8Points
        }

        private enum InitialGuessMode
        {
            SelectedCamera,
            SceneViewCamera,
            DefaultCamera
        }

        private const float HandleRadius = 7f;
        private const float MinPreviewZoom = 0.25f;
        private const float MaxPreviewZoom = 6f;
        private const float PreviewViewportHeight = 640f;
        private Texture2D referenceTexture;
        private SolveMode solveMode = SolveMode.Plane4Points;
        private InitialGuessMode initialGuessMode = InitialGuessMode.SelectedCamera;
        private float planeWidth = 1f;
        private float planeHeight = 1f;
        private float cubeWidth = 1f;
        private float cubeHeight = 1f;
        private float cubeDepth = 1f;
        private Vector2[] imagePoints = CreateDefaultImagePoints(4);
        private Vector2[] projectedPoints;
        private int draggingPoint = -1;
        private CameraReverseSolveResult lastResult;
        private bool hasResult;
        private string statusMessage = "Assign a reference image, place points, then solve.";
        private MessageType statusType = MessageType.Info;
        private Vector2 scrollPosition;
        private float previewZoom = 1f;
        private Vector2 previewPan;
        private bool draggingPreview;
        private Vector2 lastPreviewMousePosition;

        [MenuItem("Tools/Camera Reverse Solver")]
        private static void Open()
        {
            GetWindow<CameraReverseSolverWindow>("Camera Reverse Solver");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawInputSection();
            EditorGUILayout.Space();
            PreviewLayout previewLayout = DrawImagePreview();
            HandlePreviewInput(previewLayout);
            EditorGUILayout.Space();
            DrawSolveSection();
            EditorGUILayout.Space();
            DrawResultSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawInputSection()
        {
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            referenceTexture = (Texture2D)EditorGUILayout.ObjectField("Reference Image", referenceTexture, typeof(Texture2D), false);

            EditorGUI.BeginChangeCheck();
            solveMode = (SolveMode)EditorGUILayout.EnumPopup("Mode", solveMode);
            if (EditorGUI.EndChangeCheck())
            {
                ResetPointCount();
                hasResult = false;
                projectedPoints = null;
            }

            initialGuessMode = (InitialGuessMode)EditorGUILayout.EnumPopup("Initial Guess", initialGuessMode);

            if (solveMode == SolveMode.Plane4Points)
            {
                planeWidth = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Plane Width", planeWidth));
                planeHeight = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Plane Height", planeHeight));
            }
            else
            {
                cubeWidth = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Cube Width", cubeWidth));
                cubeHeight = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Cube Height", cubeHeight));
                cubeDepth = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Cube Depth", cubeDepth));
            }

            if (GUILayout.Button("Reset Points"))
            {
                ResetPointCount();
                hasResult = false;
                projectedPoints = null;
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private PreviewLayout DrawImagePreview()
        {
            EditorGUILayout.LabelField("Image Points", EditorStyles.boldLabel);
            if (referenceTexture == null)
            {
                Rect emptyRect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
                GUI.Box(emptyRect, "No Reference Image");
                return new PreviewLayout(emptyRect, emptyRect);
            }

            float availableWidth = Mathf.Max(220f, EditorGUIUtility.currentViewWidth - 40f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Zoom", GUILayout.Width(42f));
            previewZoom = EditorGUILayout.Slider(previewZoom, MinPreviewZoom, MaxPreviewZoom);
            if (GUILayout.Button("Fit", GUILayout.Width(52f)))
            {
                previewZoom = 1f;
                previewPan = Vector2.zero;
            }
            EditorGUILayout.EndHorizontal();

            Rect viewportRect = GUILayoutUtility.GetRect(availableWidth, PreviewViewportHeight, GUILayout.ExpandWidth(true));
            GUI.Box(viewportRect, GUIContent.none);
            Rect imageRect = CalculateImageRect(viewportRect);

            GUI.BeginGroup(viewportRect);
            Rect localImageRect = new Rect(
                imageRect.x - viewportRect.x,
                imageRect.y - viewportRect.y,
                imageRect.width,
                imageRect.height);
            GUI.DrawTexture(localImageRect, referenceTexture, ScaleMode.StretchToFill, false);
            GUI.EndGroup();

            DrawPointHandles(viewportRect, imageRect);
            return new PreviewLayout(viewportRect, imageRect);
        }

        private void DrawPointHandles(Rect viewportRect, Rect imageRect)
        {
            if (imagePoints == null)
            {
                return;
            }

            Handles.BeginGUI();
            for (int i = 0; i < imagePoints.Length; i++)
            {
                Vector2 screen = NormalizedToScreen(imageRect, imagePoints[i]);
                if (!viewportRect.Contains(screen))
                {
                    continue;
                }

                Handles.color = Color.yellow;
                Handles.DrawSolidDisc(screen, Vector3.forward, HandleRadius);
                Handles.color = Color.black;
                Handles.DrawWireDisc(screen, Vector3.forward, HandleRadius);
                GUI.Label(new Rect(screen.x + 8f, screen.y - 9f, 44f, 18f), i.ToString());
            }

            if (projectedPoints != null)
            {
                for (int i = 0; i < projectedPoints.Length; i++)
                {
                    Vector2 screen = NormalizedToScreen(imageRect, projectedPoints[i]);
                    if (!viewportRect.Contains(screen))
                    {
                        continue;
                    }

                    Handles.color = Color.cyan;
                    Handles.DrawWireDisc(screen, Vector3.forward, HandleRadius + 3f);
                    Handles.DrawLine(new Vector3(screen.x - 6f, screen.y, 0f), new Vector3(screen.x + 6f, screen.y, 0f));
                    Handles.DrawLine(new Vector3(screen.x, screen.y - 6f, 0f), new Vector3(screen.x, screen.y + 6f, 0f));
                }
            }

            Handles.EndGUI();
        }

        private Rect CalculateImageRect(Rect viewportRect)
        {
            float textureAspect = (float)referenceTexture.width / Mathf.Max(1, referenceTexture.height);
            float viewportAspect = viewportRect.width / Mathf.Max(1f, viewportRect.height);
            Vector2 baseSize;

            if (textureAspect >= viewportAspect)
            {
                baseSize = new Vector2(viewportRect.width, viewportRect.width / textureAspect);
            }
            else
            {
                baseSize = new Vector2(viewportRect.height * textureAspect, viewportRect.height);
            }

            Vector2 scaledSize = baseSize * previewZoom;
            Vector2 center = viewportRect.center + previewPan;
            return new Rect(
                center.x - scaledSize.x * 0.5f,
                center.y - scaledSize.y * 0.5f,
                scaledSize.x,
                scaledSize.y);
        }

        private void ZoomPreview(PreviewLayout layout, Vector2 mousePosition, float zoomDelta)
        {
            float oldZoom = previewZoom;
            float newZoom = Mathf.Clamp(previewZoom * (1f + zoomDelta), MinPreviewZoom, MaxPreviewZoom);
            if (Mathf.Approximately(oldZoom, newZoom))
            {
                return;
            }

            Vector2 imageCenter = layout.ImageRect.center;
            Vector2 offsetFromCenter = mousePosition - imageCenter;
            previewZoom = newZoom;
            previewPan -= offsetFromCenter * (newZoom / oldZoom - 1f);
        }

        private void HandlePreviewInput(PreviewLayout layout)
        {
            if (referenceTexture == null || imagePoints == null)
            {
                return;
            }

            Event current = Event.current;
            bool mouseInViewport = layout.ViewportRect.Contains(current.mousePosition);
            if (!mouseInViewport && draggingPoint < 0 && !draggingPreview)
            {
                return;
            }

            if (current.type == EventType.ScrollWheel && mouseInViewport)
            {
                ZoomPreview(layout, current.mousePosition, -current.delta.y * 0.08f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseDown && current.button == 0)
            {
                draggingPoint = FindNearestPoint(layout.ImageRect, current.mousePosition);
                if (draggingPoint >= 0)
                {
                    imagePoints[draggingPoint] = ScreenToNormalized(layout.ImageRect, current.mousePosition);
                    hasResult = false;
                    projectedPoints = null;
                    current.Use();
                }
            }
            else if (current.type == EventType.MouseDown && (current.button == 1 || current.button == 2) && mouseInViewport)
            {
                draggingPreview = true;
                lastPreviewMousePosition = current.mousePosition;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && draggingPoint >= 0)
            {
                imagePoints[draggingPoint] = ScreenToNormalized(layout.ImageRect, current.mousePosition);
                hasResult = false;
                projectedPoints = null;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseDrag && draggingPreview)
            {
                previewPan += current.mousePosition - lastPreviewMousePosition;
                lastPreviewMousePosition = current.mousePosition;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                draggingPoint = -1;
                draggingPreview = false;
            }
        }

        private void DrawSolveSection()
        {
            EditorGUILayout.LabelField("Solve", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Solve", GUILayout.Height(30f)))
            {
                Solve();
            }

            using (new EditorGUI.DisabledScope(!hasResult))
            {
                if (GUILayout.Button("Apply To Selected Camera", GUILayout.Height(30f)))
                {
                    ApplyToSelectedCamera();
                }

                if (GUILayout.Button("Create Camera", GUILayout.Height(30f)))
                {
                    CreateSolvedCamera();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawResultSection()
        {
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            if (!hasResult)
            {
                EditorGUILayout.LabelField("No solved result yet.");
                return;
            }

            CameraReverseParameters parameters = lastResult.Parameters;
            EditorGUILayout.Vector3Field("Position", parameters.Position);
            EditorGUILayout.Vector3Field("Rotation", parameters.Rotation.eulerAngles);
            EditorGUILayout.FloatField("Vertical FOV", parameters.VerticalFov);

            float width = referenceTexture != null ? referenceTexture.width : 1f;
            float height = referenceTexture != null ? referenceTexture.height : 1f;
            float pixelScale = Mathf.Sqrt(width * width + height * height);
            EditorGUILayout.FloatField("Average Error Pixels", lastResult.AverageNormalizedError * pixelScale);
            EditorGUILayout.FloatField("Max Error Pixels", lastResult.MaxNormalizedError * pixelScale);

            if (projectedPoints != null && imagePoints != null && projectedPoints.Length == imagePoints.Length)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Per Point Error", EditorStyles.boldLabel);
                for (int i = 0; i < projectedPoints.Length; i++)
                {
                    float error = Vector2.Distance(projectedPoints[i], imagePoints[i]) * pixelScale;
                    EditorGUILayout.LabelField($"Point {i}", $"{error:F2}px");
                }
            }
        }

        private void Solve()
        {
            if (referenceTexture == null)
            {
                SetStatus("Reference image is missing.", MessageType.Error);
                return;
            }

            Vector3[] worldPoints = CreateWorldPoints();
            if (imagePoints == null || imagePoints.Length != worldPoints.Length)
            {
                SetStatus("Point count does not match the selected mode.", MessageType.Error);
                return;
            }

            float aspect = (float)referenceTexture.width / Mathf.Max(1, referenceTexture.height);
            CameraReverseParameters initial = CreateInitialGuess(worldPoints);
            lastResult = CameraReverseSolver.Solve(worldPoints, imagePoints, initial, aspect);
            hasResult = lastResult.Success;
            projectedPoints = BuildProjectedPoints(worldPoints, lastResult.Parameters, aspect);

            if (!lastResult.Success)
            {
                SetStatus("Solve failed. Check point order and initial camera guess.", MessageType.Error);
                return;
            }

            float pixelScale = Mathf.Sqrt(referenceTexture.width * referenceTexture.width + referenceTexture.height * referenceTexture.height);
            SetStatus($"Solved. Average error: {lastResult.AverageNormalizedError * pixelScale:F2}px, max error: {lastResult.MaxNormalizedError * pixelScale:F2}px.", MessageType.Info);
            Repaint();
        }

        private CameraReverseParameters CreateInitialGuess(Vector3[] worldPoints)
        {
            Camera camera = GetSelectedCamera();
            if (initialGuessMode == InitialGuessMode.SelectedCamera && camera != null)
            {
                return FromCamera(camera);
            }

            if (initialGuessMode == InitialGuessMode.SceneViewCamera && SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                return FromCamera(SceneView.lastActiveSceneView.camera);
            }

            float radius = EstimatePointRadius(worldPoints);
            return new CameraReverseParameters(new Vector3(0f, 0f, -Mathf.Max(3f, radius * 3f)), Quaternion.identity, 60f);
        }

        private Vector3[] CreateWorldPoints()
        {
            return solveMode == SolveMode.Plane4Points
                ? CameraReverseGeometry.CreatePlanePoints(planeWidth, planeHeight)
                : CameraReverseGeometry.CreateCubePoints(cubeWidth, cubeHeight, cubeDepth);
        }

        private Vector2[] BuildProjectedPoints(Vector3[] worldPoints, CameraReverseParameters parameters, float aspect)
        {
            var points = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                if (!CameraReverseProjection.TryProject(worldPoints[i], parameters, aspect, out points[i]))
                {
                    points[i] = new Vector2(-1f, -1f);
                }
            }

            return points;
        }

        private void ApplyToSelectedCamera()
        {
            Camera camera = GetSelectedCamera();
            if (camera == null)
            {
                SetStatus("Select a Camera or a GameObject with a Camera component first.", MessageType.Error);
                return;
            }

            ApplyToCamera(camera, lastResult.Parameters);
            SetStatus("Applied solved parameters to selected camera.", MessageType.Info);
        }

        private void CreateSolvedCamera()
        {
            var gameObject = new GameObject("Reverse Solved Camera");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Reverse Solved Camera");
            Camera camera = gameObject.AddComponent<Camera>();
            ApplyToCamera(camera, lastResult.Parameters);
            Selection.activeGameObject = gameObject;
            SetStatus("Created a new camera with solved parameters.", MessageType.Info);
        }

        private void ApplyToCamera(Camera camera, CameraReverseParameters parameters)
        {
            Undo.RecordObject(camera.transform, "Apply Reverse Solved Camera Transform");
            Undo.RecordObject(camera, "Apply Reverse Solved Camera FOV");
            camera.transform.position = parameters.Position;
            camera.transform.rotation = parameters.Rotation;
            camera.fieldOfView = parameters.VerticalFov;
            EditorUtility.SetDirty(camera.transform);
            EditorUtility.SetDirty(camera);
        }

        private Camera GetSelectedCamera()
        {
            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponent<Camera>();
        }

        private CameraReverseParameters FromCamera(Camera camera)
        {
            return new CameraReverseParameters(camera.transform.position, camera.transform.rotation, camera.fieldOfView);
        }

        private void ResetPointCount()
        {
            imagePoints = CreateDefaultImagePoints(solveMode == SolveMode.Plane4Points ? 4 : 8);
        }

        private static Vector2[] CreateDefaultImagePoints(int count)
        {
            if (count == 4)
            {
                return new[]
                {
                    new Vector2(0.25f, 0.25f),
                    new Vector2(0.75f, 0.25f),
                    new Vector2(0.75f, 0.75f),
                    new Vector2(0.25f, 0.75f)
                };
            }

            return new[]
            {
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.25f),
                new Vector2(0.75f, 0.75f),
                new Vector2(0.25f, 0.75f),
                new Vector2(0.35f, 0.35f),
                new Vector2(0.85f, 0.35f),
                new Vector2(0.85f, 0.85f),
                new Vector2(0.35f, 0.85f)
            };
        }

        private static float EstimatePointRadius(Vector3[] points)
        {
            float radius = 1f;
            for (int i = 0; i < points.Length; i++)
            {
                radius = Mathf.Max(radius, points[i].magnitude);
            }

            return radius;
        }

        private int FindNearestPoint(Rect rect, Vector2 mousePosition)
        {
            int nearest = -1;
            float nearestDistance = HandleRadius * 2.5f;
            for (int i = 0; i < imagePoints.Length; i++)
            {
                float distance = Vector2.Distance(NormalizedToScreen(rect, imagePoints[i]), mousePosition);
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static Vector2 NormalizedToScreen(Rect rect, Vector2 normalized)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMax, rect.yMin, normalized.y));
        }

        private static Vector2 ScreenToNormalized(Rect rect, Vector2 screen)
        {
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, screen.x);
            float y = Mathf.InverseLerp(rect.yMax, rect.yMin, screen.y);
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }
    }
}
