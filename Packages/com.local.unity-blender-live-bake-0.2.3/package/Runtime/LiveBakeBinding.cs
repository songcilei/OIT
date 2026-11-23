using UnityEngine;
using UnityEngine.Rendering;
namespace LiveBakeBridge
{
    // Serialized on the scene instance, never applied to the source Prefab by this tool.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class LiveBakeBinding : MonoBehaviour
    {
        [HideInInspector] public bool applied;
        [HideInInspector] public Material[] originalMaterials;
        [HideInInspector] public ShadowCastingMode originalShadowCasting;
        [HideInInspector] public bool originalReceiveShadows;
        [HideInInspector] public int originalLightmapIndex;
        [HideInInspector] public Vector4 originalLightmapScaleOffset;
        [HideInInspector] public string bakeId;
        [HideInInspector] public string sourceHash;
        [HideInInspector] public bool atlas;
        [HideInInspector] public Vector4 bakeScaleOffset=new Vector4(1,1,0,0);
    }
}
