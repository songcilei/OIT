Shader "Custom/PBR IBL"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 1

        _MetallicMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0.95
        _RoughnessMap("Roughness Map", 2D) = "white" {}
        _Roughness("Roughness", Range(0, 1)) = 0.1
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1

        [NoScaleOffset] _IrradianceMap("Irradiance Cubemap", Cube) = "black" {}
        [NoScaleOffset] _PrefilterMap("Prefiltered Cubemap", Cube) = "black" {}
        [NoScaleOffset] _BRDFLUT("BRDF LUT", 2D) = "gray" {}
        _MaxReflectionLOD("Prefilter Max Mip", Range(0, 12)) = 4
        [Toggle] _FlipBRDFLUTY("Flip BRDF LUT Y", Float) = 0

        _IOR("IOR", Range(1, 3)) = 1.5
        _IORLevel("IOR Level", Range(0, 1)) = 0.5
        _LightIntensity("Main Light Intensity", Range(0, 8)) = 1
        _IBLStrength("IBL Strength", Range(0, 8)) = 1

        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define PI 3.14159265359

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_BRDFLUT);
            SAMPLER(sampler_BRDFLUT);
            TEXTURECUBE(_IrradianceMap);
            SAMPLER(sampler_IrradianceMap);
            TEXTURECUBE(_PrefilterMap);
            SAMPLER(sampler_PrefilterMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _Metallic;
                half _Roughness;
                half _OcclusionStrength;
                half _MaxReflectionLOD;
                half _FlipBRDFLUTY;
                half _IOR;
                half _IORLevel;
                half _LightIntensity;
                half _IBLStrength;
            CBUFFER_END

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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 F0FromIOR(float ior, float level)
            {
                float ratio = (ior - 1.0) / max(ior + 1.0, 1e-4);
                float f0 = ratio * ratio;
                return saturate(f0 * level / 0.5).xxx;
            }

            float3 FresnelSchlick(float cosTheta, float3 f0)
            {
                return f0 + (1.0 - f0) * pow(1.0 - saturate(cosTheta), 5.0);
            }

            float3 FresnelSchlickRoughness(float cosTheta, float3 f0, float roughness)
            {
                float3 grazing = max((1.0 - roughness).xxx, f0);
                return f0 + (grazing - f0) * pow(1.0 - saturate(cosTheta), 5.0);
            }

            float DistributionGGX(float3 n, float3 h, float roughness)
            {
                float a = roughness * roughness;
                float a2 = a * a;
                float nDotH = saturate(dot(n, h));
                float d = nDotH * nDotH * (a2 - 1.0) + 1.0;
                return a2 / max(PI * d * d, 1e-5);
            }

            float GeometrySchlickGGX(float nDotX, float roughness)
            {
                float r = roughness + 1.0;
                float k = r * r * 0.125;
                return nDotX / max(nDotX * (1.0 - k) + k, 1e-5);
            }

            float GeometrySmith(float3 n, float3 v, float3 l, float roughness)
            {
                float nDotV = saturate(dot(n, v));
                float nDotL = saturate(dot(n, l));
                return GeometrySchlickGGX(nDotV, roughness) *
                       GeometrySchlickGGX(nDotL, roughness);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(
                    normalInputs.tangentWS,
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;

                float metallic = saturate(
                    SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r * _Metallic);
                float roughness = clamp(
                    SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r * _Roughness,
                    0.045,
                    1.0);
                float ao = lerp(
                    1.0,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r,
                    _OcclusionStrength);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv),
                    _NormalScale);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 tangentWS = SafeNormalize(input.tangentWS.xyz);
                float3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, normalWS);
                float3 n = SafeNormalize(TransformTangentToWorld(normalTS, tangentToWorld));

                float3 v = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float nDotV = saturate(dot(n, v));
                float3 reflection = reflect(-v, n);

                float3 dielectricF0 = F0FromIOR(_IOR, _IORLevel);
                float3 f0 = lerp(dielectricF0, albedo, metallic);

                Light mainLight = GetMainLight(input.shadowCoord);
                float3 l = SafeNormalize(mainLight.direction);
                float3 h = SafeNormalize(l + v);
                float nDotL = saturate(dot(n, l));

                float ndf = DistributionGGX(n, h, roughness);
                float geometry = GeometrySmith(n, v, l, roughness);
                float3 fresnel = FresnelSchlick(saturate(dot(h, v)), f0);
                float3 kS = fresnel;
                float3 kD = (1.0 - kS) * (1.0 - metallic);

                float denominator = max(4.0 * nDotV * nDotL, 1e-4);
                float3 specularBRDF = ndf * geometry * fresnel / denominator;
                float3 radiance = mainLight.color *
                                  mainLight.distanceAttenuation *
                                  mainLight.shadowAttenuation *
                                  _LightIntensity;
                float3 direct = (kD * albedo / PI + specularBRDF) * radiance * nDotL;

                float3 fresnelIBL = FresnelSchlickRoughness(nDotV, f0, roughness);
                float3 kDIBL = (1.0 - fresnelIBL) * (1.0 - metallic);
                float3 irradiance = SAMPLE_TEXTURECUBE(
                    _IrradianceMap,
                    sampler_IrradianceMap,
                    n).rgb;
                irradiance = SampleSH(input.normalWS);
                float3 diffuseIBL = irradiance * albedo;

                float3 prefiltered = SAMPLE_TEXTURECUBE_LOD(
                    _PrefilterMap,
                    sampler_PrefilterMap,
                    reflection,
                    roughness * _MaxReflectionLOD).rgb;

                float lutY = lerp(roughness, 1.0 - roughness, _FlipBRDFLUTY);
                float2 envBRDF = SAMPLE_TEXTURE2D(
                    _BRDFLUT,
                    sampler_BRDFLUT,
                    float2(nDotV, lutY)).rg;
                float3 specularIBL = prefiltered * (f0 * envBRDF.x + envBRDF.y);

                float3 indirect = (kDIBL * diffuseIBL + specularIBL) * ao * _IBLStrength;
                return half4(direct + indirect, _BaseColor.a);
            }
            ENDHLSL
        }

        // Reuse URP's opaque depth and shadow caster passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack Off
}
