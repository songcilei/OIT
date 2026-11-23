using UnityEngine;
namespace LiveBakeBridge
{
    // Scene-instance backup only; original mesh and source Prefab remain untouched.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class LiveBakeUV2Binding : MonoBehaviour
    {
        [HideInInspector] public Mesh originalMesh;
        [HideInInspector] public Mesh generatedMesh;
    }
}
