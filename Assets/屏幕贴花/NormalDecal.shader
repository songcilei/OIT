Shader "Custom/URP/NormalDecal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Opacity("Opacity",Range(0,1)) = 1
        _Cutoff("Alpha Cutoff",Range(0,1))=0.001
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "NormalDepthDecal"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            Zwrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)
                UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.positionOS = input.positionOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {   
                UNITY_SETUP_INSTANCE_ID(input);
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = SampleSceneDepth(screenUV);
                
                #if UNITY_REVERSED_Z
                    if (sceneDepth <= 0.00001)
                        discard;
                #else
                    if (sceneDepth >= 0.99999)
                        discard;
                #endif
                
                #if !UNITY_REVERSED_Z
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, sceneDepth);
                #endif
                
                float3 scenePositionWS = ComputeWorldSpacePosition(
                    screenUV,
                    sceneDepth,
                    UNITY_MATRIX_I_VP);
                
                float3 decalPositionOS = TransformWorldToObject(scenePositionWS);
                if (any(decalPositionOS < -0.5) ||
                    any(decalPositionOS > 0.5))
                    discard;
                float2 decalUV = decalPositionOS.xz + 0.5;
                decalUV = decalUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
                
                half4 decal = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    decalUV) * UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);

                decal.a *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Opacity);
                clip(decal.a - UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Cutoff));
                return decal;
                
            }
            ENDHLSL
        }
    }
}
