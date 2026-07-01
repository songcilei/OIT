using UnityEngine;

namespace CapsuleAOTool
{
    public static class CapsuleAOShaderGlobals
    {
        public const int MaxCapsules = 64;

        private static readonly int CapsuleAOCountId = Shader.PropertyToID("_CapsuleAOCount");
        private static readonly int CapsuleAOData0Id = Shader.PropertyToID("_CapsuleAOData0");
        private static readonly int CapsuleAOData1Id = Shader.PropertyToID("_CapsuleAOData1");
        private static readonly int CapsuleAOData2Id = Shader.PropertyToID("_CapsuleAOData2");
        private static readonly int CapsuleAOData3Id = Shader.PropertyToID("_CapsuleAOData3");

        private static readonly Vector4[] Data0 = new Vector4[MaxCapsules];
        private static readonly Vector4[] Data1 = new Vector4[MaxCapsules];
        private static readonly Vector4[] Data2 = new Vector4[MaxCapsules];
        private static readonly Vector4[] Data3 = new Vector4[MaxCapsules];

        public static void Upload()
        {
            int count = 0;

            for (int i = 0; i < CapsuleAORegistry.ActiveCapsules.Count && count < MaxCapsules; i++)
            {
                CapsuleAO capsule = CapsuleAORegistry.ActiveCapsules[i];
                if (capsule == null || !capsule.isActiveAndEnabled)
                {
                    continue;
                }

                capsule.GetWorldCapsule(out Vector3 start, out Vector3 end, out float radius);
                Data0[count] = new Vector4(start.x, start.y, start.z, radius);
                Data1[count] = new Vector4(end.x, end.y, end.z, capsule.ambientIntensity);
                Data2[count] = new Vector4(capsule.ambientFalloffDistance, capsule.ambientNormalBias, capsule.ambientPower, 0f);
                Data3[count] = new Vector4(capsule.directionalIntensity, capsule.directionalSoftness, capsule.directionalMaxDistance, 0f);
                count++;
            }

            Shader.SetGlobalInt(CapsuleAOCountId, count);
            Shader.SetGlobalVectorArray(CapsuleAOData0Id, Data0);
            Shader.SetGlobalVectorArray(CapsuleAOData1Id, Data1);
            Shader.SetGlobalVectorArray(CapsuleAOData2Id, Data2);
            Shader.SetGlobalVectorArray(CapsuleAOData3Id, Data3);
        }
    }
}
