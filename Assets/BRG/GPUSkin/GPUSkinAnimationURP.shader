Shader "Learning/GPU Skin Animation URP"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        // Unity 会根据实际数据压缩骨骼权重顶点通道，例如头发可能只有两个权重。
        // Shader 必须知道有效分量数，不能始终把 BLENDWEIGHTS 当作四个有效权重。
        [HideInInspector] _GPUBoneInfluenceCount("Bone Influence Count", Float) = 4
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "GPUAnimationForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            Texture2D<float4> _GPUAnimTexture;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _GPUBoneInfluenceCount;
            CBUFFER_END

            float _GPUAnimTexWidth;
            float _GPUAnimTexHeight;
            float _GPUFrameA0;
            float _GPUFrameA1;
            float _GPUFrameALerp;
            float _GPUFrameB0;
            float _GPUFrameB1;
            float _GPUFrameBLerp;
            float _GPUTransition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 weights : BLENDWEIGHTS;
                uint4 indices : BLENDINDICES;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float3x4 LoadBoneMatrix(uint boneIndex, int frame)
            {
                int x = (int)boneIndex * 3;
                float4 row0 = _GPUAnimTexture.Load(int3(x + 0, frame, 0));
                float4 row1 = _GPUAnimTexture.Load(int3(x + 1, frame, 0));
                float4 row2 = _GPUAnimTexture.Load(int3(x + 2, frame, 0));
                return float3x4(row0, row1, row2);
            }

            float3x4 SampleBone(uint boneIndex, int frame0, int frame1, float frameLerp)
            {
                return lerp(LoadBoneMatrix(boneIndex, frame0), LoadBoneMatrix(boneIndex, frame1), frameLerp);
            }

            float3x4 SkinMatrixForFrames(uint4 indices, float4 weights, int frame0, int frame1, float frameLerp)
            {
                return SampleBone(indices.x, frame0, frame1, frameLerp) * weights.x
                     + SampleBone(indices.y, frame0, frame1, frameLerp) * weights.y
                     + SampleBone(indices.z, frame0, frame1, frameLerp) * weights.z
                     + SampleBone(indices.w, frame0, frame1, frameLerp) * weights.w;
            }

            float4 GetValidBoneWeights(float4 weights)
            {
                // 当 Mesh 的顶点通道只有 1/2/3 个分量，而 Shader 输入声明为 float4 时，
                // 图形 API 通常会把缺少的 w 分量补成 1。若直接参与蒙皮，会无故多加
                // 一整份骨骼矩阵，最典型的表现就是头发和武器被放大。
                //
                // 单骨骼 Mesh 可能完全省略 BlendWeight 通道，此时权重隐式为 1。
                if (_GPUBoneInfluenceCount < 1.5)
                    return float4(1.0, 0.0, 0.0, 0.0);

                if (_GPUBoneInfluenceCount < 2.5)
                    weights.zw = 0.0;
                else if (_GPUBoneInfluenceCount < 3.5)
                    weights.w = 0.0;

                // 导入压缩可能带来很小的量化误差，归一化可保证矩阵权重和始终为 1。
                float weightSum = max(dot(weights, float4(1.0, 1.0, 1.0, 1.0)), 1e-6);
                return weights / weightSum;
            }

            Varyings Vert(Attributes input)
            {
                float4 validWeights = GetValidBoneWeights(input.weights);
                float3x4 skinA = SkinMatrixForFrames(input.indices, validWeights,
                    (int)_GPUFrameA0, (int)_GPUFrameA1, _GPUFrameALerp);
                float3x4 skin = skinA;

                if (_GPUTransition > 0.0)
                {
                    float3x4 skinB = SkinMatrixForFrames(input.indices, validWeights,
                        (int)_GPUFrameB0, (int)_GPUFrameB1, _GPUFrameBLerp);
                    skin = lerp(skinA, skinB, _GPUTransition);
                }

                float3 positionOS = mul(skin, float4(input.positionOS, 1.0));
                float3 normalOS = normalize(mul((float3x3)skin, input.normalOS));

                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 lightDirection = normalize(half3(0.35, 0.8, 0.25));
                half lighting = saturate(dot(normalize(input.normalWS), lightDirection)) * 0.65h + 0.35h;
                return half4(albedo.rgb * lighting, albedo.a);
            }
            ENDHLSL
        }
    }
}
