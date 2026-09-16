Shader "Custom/URP/testPBR"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MetalTex("MetalTex",2D)="white"{}
        
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _SmoothTex("SmoothTex",2D)="white"{}
        
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // IBL / Reflection Probe variants.
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SmoothTex);
            SAMPLER(sampler_SmoothTex);
            TEXTURE2D(_MetalTex);
            SAMPLER(sampler_MetalTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(URPShaderTemplateProperties)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(URPShaderTemplateProperties)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(
                    normalInputs.tangentWS,
                    input.tangentOS.w * GetOddNegativeScale());
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half3 SampleNormalWS(Varyings input)
            {
                half4 tangentWS = input.tangentWS;
                half3 bitangentWS = cross(input.normalWS, tangentWS.xyz) * tangentWS.w;
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv));
                return TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangentWS, input.normalWS));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(URPShaderTemplateProperties, _BaseColor);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * baseColor;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(SampleNormalWS(input));
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseMap.rgb;
                surfaceData.alpha = baseMap.a;
                surfaceData.metallic = _Metallic*SAMPLE_TEXTURE2D(_MetalTex,sampler_MetalTex,input.uv).r;
                surfaceData.smoothness = _Smoothness*(1-SAMPLE_TEXTURE2D(_SmoothTex,sampler_SmoothTex,input.uv).r);
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.specular = half3(0, 0, 0);

                Light mainLight = GetMainLight(inputData.shadowCoord);
                BRDFData brdfData;
                InitializeBRDFData(
                    surfaceData.albedo,
                    surfaceData.metallic,
                    surfaceData.specular,
                    surfaceData.smoothness,
                    surfaceData.alpha,
                    brdfData);

                half3 color = LightingPhysicallyBased(
                    brdfData,
                    mainLight,
                    inputData.normalWS,
                    inputData.viewDirectionWS);

                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                    color += LightingPhysicallyBased(
                        brdfData,
                        additionalLight,
                        inputData.normalWS,
                        inputData.viewDirectionWS);
                }

                // URP IBL:
                // bakedGI supplies diffuse irradiance from spherical harmonics.
                // GlobalIllumination adds diffuse GI and the prefiltered
                // skybox / reflection-probe specular response, including
                // roughness LOD, Fresnel and the environment BRDF.
                color += GlobalIllumination(
                    brdfData,
                    inputData.bakedGI,
                    surfaceData.occlusion,
                    inputData.positionWS,
                    inputData.normalWS,
                    inputData.viewDirectionWS);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color, surfaceData.alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
