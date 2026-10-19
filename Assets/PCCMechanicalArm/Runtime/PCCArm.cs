using UnityEngine;
using UnityEngine.Rendering;

namespace PCCMechanicalArm
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("PCC/Mechanical Arm")]
    public sealed class PCCArm : MonoBehaviour
    {
        [Header("Target (position only)")]
        public Transform target;
        [Tooltip("Optional EMPTY child marker. Its transform is driven by the arm, not used by IK.")]
        public Transform tipMarker;
        public bool solveIK = true;
        public bool allowExtension = true;

        [Header("PCC sections (not the number of visible metal rings)")]
        public PCCSection[] sections =
        {
            new PCCSection(0.55f, 1.65f, 1.05f, new Vector2(18f, 0f)),
            new PCCSection(0.55f, 1.65f, 1.05f, new Vector2(-32f, 8f)),
            new PCCSection(0.55f, 1.65f, 1.05f, new Vector2(18f, -8f))
        };

        [Header("IK budget / responsiveness")]
        [Range(1, 32)] public int iterationsPerFrame = 8;
        [Min(0.0001f)] public float positionTolerance = 0.01f;
        [Range(0.001f, 0.3f)] public float damping = 0.035f;
        [Range(0.01f, 0.5f)] public float maxParameterStep = 0.18f;
        [Min(1f)] public float bendSpeedDegrees = 220f;
        [Min(0.01f)] public float extensionSpeed = 1.8f;

        [Header("Procedural mechanical appearance")]
        [Range(3, 24)] public int linksPerSection = 10;
        [Range(6, 12)] public int radialSides = 8;
        [Min(0.005f)] public float shellRadius = 0.14f;
        [Range(0.15f, 0.8f)] public float shellThicknessFraction = 0.62f;
        [Range(0.35f, 1f)] public float tipRadiusScale = 0.7f;
        public Material shellMaterial;
        public Material coreMaterial;
        public bool castShadows = false;

        [Header("Diagnostics (runtime)")]
        [SerializeField] private float tipError;
        [SerializeField] private float solvedPoseError;
        [SerializeField] private int lastIterations;
        [SerializeField] private float totalLength;

        private PCCSolver solver;
        private PCCArmMesh geometry;
        private GameObject visualObject;
        private MeshRenderer visualRenderer;
        private Vector3[] points;
        private Quaternion[] frames;
        private int builtLinksPerSection;
        private float shellThickness;
        private Material fallbackMaterial;

        public PCCSolver Solver => solver;
        public float TipError => tipError;
        public Vector3 TipPosition => points == null ? transform.position
            : transform.TransformPoint(points[points.Length - 1]);
        public Quaternion TipRotation => frames == null ? transform.rotation
            : transform.rotation * frames[frames.Length - 1];

        private void OnEnable()
        {
            if (Application.isPlaying) Rebuild();
        }

        [ContextMenu("Rebuild Arm (Play Mode)")]
        public void Rebuild()
        {
            if (!Application.isPlaying) return;
            ReleaseResources();
            if (sections == null || sections.Length == 0 || sections.Length > 8)
            {
                Debug.LogError("PCCArm requires 1 to 8 sections.", this);
                return;
            }
            linksPerSection = Mathf.Clamp(linksPerSection, 3, 24);
            radialSides = Mathf.Clamp(radialSides, 6, 12);
            float minimumLength = float.MaxValue;
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] == null) sections[i] = new PCCSection();
                sections[i].Sanitize();
                minimumLength = Mathf.Min(minimumLength, sections[i].minLength);
            }
            solver = new PCCSolver(sections);
            builtLinksPerSection = linksPerSection;
            int linkCount = sections.Length * builtLinksPerSection;
            points = new Vector3[linkCount + 1];
            frames = new Quaternion[linkCount + 1];
            shellThickness = minimumLength / builtLinksPerSection * shellThicknessFraction;
            geometry = new PCCArmMesh(linkCount, radialSides);
            visualObject = new GameObject("PCC Generated Mesh");
            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(transform, false);
            visualObject.AddComponent<MeshFilter>().sharedMesh = geometry.Mesh;
            visualRenderer = visualObject.AddComponent<MeshRenderer>();
            if (shellMaterial == null || coreMaterial == null)
            {
                Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline == null
                    ? "Standard" : "Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    fallbackMaterial = new Material(shader) { name = "PCC Fallback (assign real materials for builds)" };
                    fallbackMaterial.color = new Color(0.36f, 0.44f, 0.50f);
                }
                Debug.LogWarning("PCCArm: assign Shell/Core Material assets before building; runtime Shader.Find can be stripped.", this);
            }
            visualRenderer.sharedMaterials = new[]
            {
                shellMaterial != null ? shellMaterial : fallbackMaterial,
                coreMaterial != null ? coreMaterial : fallbackMaterial
            };
            Vector3 scale = transform.lossyScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
                Mathf.Abs(scale.x - scale.y) > 0.001f || Mathf.Abs(scale.x - scale.z) > 0.001f)
                Debug.LogWarning("PCCArm: use positive uniform scale on the arm and its ancestors. Nonuniform scale distorts circular arcs.", this);
            UpdateGeometry();
        }

        private void LateUpdate()
        {
            if (solver == null || geometry == null) return;
            if (solveIK && target != null)
            {
                solver.Solve(transform.InverseTransformPoint(target.position),
                    Mathf.Clamp(iterationsPerFrame, 1, 32), Mathf.Max(positionTolerance, 0.0001f),
                    damping, Mathf.Max(maxParameterStep, 0.001f), allowExtension);
            }
            solver.Advance(Time.deltaTime, Mathf.Max(0f, bendSpeedDegrees) * Mathf.Deg2Rad,
                Mathf.Max(0f, extensionSpeed));
            UpdateGeometry();
        }

        private void UpdateGeometry()
        {
            int index = 0;
            totalLength = 0f;
            for (int section = 0; section < solver.SectionCount; section++)
            {
                for (int j = section == 0 ? 0 : 1; j <= builtLinksPerSection; j++)
                {
                    solver.EvaluatePose(section, (float)j / builtLinksPerSection,
                        out points[index], out frames[index]);
                    index++;
                }
                Vector2 bend; float length;
                solver.GetSectionPose(section, out bend, out length);
                totalLength += length;
            }
            geometry.Update(points, frames, Mathf.Max(shellRadius, 0.005f),
                shellThickness, Mathf.Clamp(tipRadiusScale, 0.35f, 1f));
            visualRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            visualRenderer.receiveShadows = castShadows;
            // Never let a misassigned marker drive the target, base, or their ancestors.
            if (tipMarker != null && tipMarker != transform && !transform.IsChildOf(tipMarker) &&
                (target == null || (tipMarker != target && !target.IsChildOf(tipMarker))))
            {
                tipMarker.SetPositionAndRotation(TipPosition, TipRotation);
            }
            tipError = target != null ? Vector3.Distance(TipPosition, target.position) : 0f;
            solvedPoseError = solver.LastGoalError;
            lastIterations = solver.LastIterations;
        }

        [ContextMenu("Reset Pose (Play Mode)")]
        public void ResetPose()
        {
            if (solver == null) return;
            solver.ResetPose();
            UpdateGeometry();
        }

        /// <summary>Attach an effect/tool to any point along a PCC section, without bones.</summary>
        public bool GetWorldPose(int section, float t, out Vector3 position, out Quaternion rotation)
        {
            if (solver == null || section < 0 || section >= solver.SectionCount)
            {
                position = transform.position; rotation = transform.rotation;
                return false;
            }
            solver.EvaluatePose(section, t, out position, out rotation);
            position = transform.TransformPoint(position);
            rotation = transform.rotation * rotation;
            return true;
        }

        private void OnDisable() { ReleaseResources(); }
        private void OnDestroy() { ReleaseResources(); }
        private void ReleaseResources()
        {
            if (visualObject != null)
            {
                visualObject.SetActive(false);
                Destroy(visualObject);
            }
            if (geometry != null) geometry.Dispose();
            if (fallbackMaterial != null) Destroy(fallbackMaterial);
            visualObject = null; visualRenderer = null;
            geometry = null; solver = null; points = null; frames = null; fallbackMaterial = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null) return;
            Gizmos.color = new Color(0.15f, 0.85f, 1f);
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(transform.TransformPoint(points[i - 1]), transform.TransformPoint(points[i]));
            if (target != null)
            {
                Gizmos.color = tipError < positionTolerance ? Color.green : Color.yellow;
                Gizmos.DrawLine(TipPosition, target.position);
                Gizmos.DrawWireSphere(target.position, positionTolerance);
            }
        }
    }
}
