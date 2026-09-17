Shader "Custom/URP/anisoPBR"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        _Roughness ("Roughness", Range(0, 1)) = 0.5
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        // R controls strength. G rotates the anisotropy direction by one turn.
        _AnisotropyMap ("Anisotropy (R) Direction (G)", 2D) = "white" {}
        _Anisotropy ("Anisotropy", Range(-0.95, 0.95)) = 0
        _AnisotropyRotation ("Anisotropy Rotation", Range(0, 1)) = 0

        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _AlphaClip ("Alpha Clip", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
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
            Name "ForwardAnisotropicLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define ANISO_PI 3.14159265359
            #define ANISO_TWO_PI 6.28318530718

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_AnisotropyMap);
            SAMPLER(sampler_AnisotropyMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _NormalScale;
                half _Metallic;
                half _Roughness;
                half _OcclusionStrength;
                half _Anisotropy;
                half _AnisotropyRotation;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(URPShaderTemplateProperties)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(URPShaderTemplateProperties)

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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
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

            half3 FresnelSchlick(half cosTheta, half3 f0)
            {
                half x = 1.0h - saturate(cosTheta);
                half x2 = x * x;
                half x5 = x2 * x2 * x;
                return f0 + (1.0h - f0) * x5;
            }

            void GetAnisotropicRoughness(
                half roughness,
                half anisotropy,
                out half alphaX,
                out half alphaY)
            {
                half alpha = max(roughness * roughness, 0.002h);
                half aspect = sqrt(max(1.0h - 0.9h * abs(anisotropy), 0.1h));
                half longAxis = alpha / aspect;
                half shortAxis = alpha * aspect;

                alphaX = anisotropy >= 0.0h ? longAxis : shortAxis;
                alphaY = anisotropy >= 0.0h ? shortAxis : longAxis;
                alphaX = max(alphaX, 0.002h);
                alphaY = max(alphaY, 0.002h);
            }

            half DistributionAnisotropicGGX(
                half3 normalWS,
                half3 tangentWS,
                half3 bitangentWS,
                half3 halfDirectionWS,
                half alphaX,
                half alphaY)
            {
                half nDotH = saturate(dot(normalWS, halfDirectionWS));
                half tDotH = dot(tangentWS, halfDirectionWS);
                half bDotH = dot(bitangentWS, halfDirectionWS);
                half3 h = half3(tDotH / alphaX, bDotH / alphaY, nDotH);
                half denominator = dot(h, h);
                return rcp(max(ANISO_PI * alphaX * alphaY * denominator * denominator, 1e-4h));
            }

            half SmithMaskingAnisotropic(
                half3 directionWS,
                half3 normalWS,
                half3 tangentWS,
                half3 bitangentWS,
                half alphaX,
                half alphaY)
            {
                half nDotDirection = saturate(dot(normalWS, directionWS));
                half tDotDirection = dot(tangentWS, directionWS);
                half bDotDirection = dot(bitangentWS, directionWS);
                half projected = alphaX * alphaX * tDotDirection * tDotDirection +
                                 alphaY * alphaY * bDotDirection * bDotDirection;
                half root = sqrt(projected + nDotDirection * nDotDirection);
                return 2.0h * nDotDirection / max(nDotDirection + root, 1e-4h);
            }

            half3 EvaluateAnisotropicLight(
                Light light,
                half3 albedo,
                half metallic,
                half3 f0,
                half3 normalWS,
                half3 tangentWS,
                half3 bitangentWS,
                half3 viewDirectionWS,
                half alphaX,
                half alphaY)
            {
                half3 lightDirectionWS = SafeNormalize(light.direction);
                half nDotL = saturate(dot(normalWS, lightDirectionWS));
                half nDotV = saturate(dot(normalWS, viewDirectionWS));

                if (nDotL <= 0.0h || nDotV <= 0.0h)
                    return 0.0h;

                half3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);
                half distribution = DistributionAnisotropicGGX(
                    normalWS, tangentWS, bitangentWS, halfDirectionWS, alphaX, alphaY);
                half geometry = SmithMaskingAnisotropic(
                    viewDirectionWS, normalWS, tangentWS, bitangentWS, alphaX, alphaY) *
                    SmithMaskingAnisotropic(
                        lightDirectionWS, normalWS, tangentWS, bitangentWS, alphaX, alphaY);
                half3 fresnel = FresnelSchlick(
                    saturate(dot(halfDirectionWS, viewDirectionWS)), f0);

                half3 specular = distribution * geometry * fresnel /
                    max(4.0h * nDotV * nDotL, 1e-4h);
                half3 diffuseWeight = (1.0h - fresnel) * (1.0h - metallic);
                half3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation;
                
                return (diffuseWeight * albedo  + specular) * radiance * nDotL;
            }

            half3 GetAnisotropicReflectionDirection(
                half3 normalWS,
                half3 tangentWS,
                half3 bitangentWS,
                half3 viewDirectionWS,
                half roughness,
                half anisotropy)
            {
                // A regular reflection probe is isotropically prefiltered. Bending
                // the reflection direction approximates the stretched highlight.
                half3 grainDirection = anisotropy >= 0.0h ? tangentWS : bitangentWS;
                half3 grainNormal = cross(viewDirectionWS, grainDirection);
                half3 stretchedNormal = SafeNormalize(cross(grainDirection, grainNormal));
                half bend = abs(anisotropy) * saturate(1.0h - roughness * 0.5h);
                half3 bentNormal = SafeNormalize(lerp(normalWS, stretchedNormal, bend));
                return reflect(-viewDirectionWS, bentNormal);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(
                    URPShaderTemplateProperties,
                    _BaseColor);
                half4 baseMap = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv) * baseColor;

                half metallic = saturate(
                    SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, input.uv).r *
                    _Metallic);
                half roughness = clamp(
                    SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r *
                    _Roughness,
                    0.045h,
                    1.0h);
                half occlusion = lerp(
                    1.0h,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r,
                    _OcclusionStrength);
                half2 anisotropySample = SAMPLE_TEXTURE2D(
                    _AnisotropyMap,
                    sampler_AnisotropyMap,
                    input.uv).rg;
                half anisotropy = clamp(
                    _Anisotropy * anisotropySample.r,
                    -0.95h,
                    0.95h);

                half3 geometricNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 geometricTangentWS = SafeNormalize(input.tangentWS.xyz);
                half3 geometricBitangentWS = input.tangentWS.w *
                    cross(geometricNormalWS, geometricTangentWS);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv),
                    _NormalScale);
                half3 normalWS = SafeNormalize(TransformTangentToWorld(
                    normalTS,
                    half3x3(
                        geometricTangentWS,
                        geometricBitangentWS,
                        geometricNormalWS)));

                // Re-orthogonalize and rotate the tangent frame after normal mapping.
                half3 tangentWS = SafeNormalize(
                    geometricTangentWS - normalWS * dot(geometricTangentWS, normalWS));
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                half angle = frac(anisotropySample.g + _AnisotropyRotation) * ANISO_TWO_PI;
                half sine;
                half cosine;
                sincos(angle, sine, cosine);
                half3 rotatedTangentWS = cosine * tangentWS + sine * bitangentWS;
                half3 rotatedBitangentWS = -sine * tangentWS + cosine * bitangentWS;

                half alphaX;
                half alphaY;
                GetAnisotropicRoughness(roughness, anisotropy, alphaX, alphaY);

                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), baseMap.rgb, metallic);

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 color = EvaluateAnisotropicLight(
                    mainLight,
                    baseMap.rgb,
                    metallic,
                    f0,
                    normalWS,
                    rotatedTangentWS,
                    rotatedBitangentWS,
                    viewDirectionWS,
                    alphaX,
                    alphaY);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(
                            lightIndex,
                            input.positionWS,
                            half4(1, 1, 1, 1));
                        color += EvaluateAnisotropicLight(
                            additionalLight,
                            baseMap.rgb,
                            metallic,
                            f0,
                            normalWS,
                            rotatedTangentWS,
                            rotatedBitangentWS,
                            viewDirectionWS,
                            alphaX,
                            alphaY);
                    }
                #endif

                BRDFData brdfData;
                InitializeBRDFData(
                    baseMap.rgb,
                    metallic,
                    half3(0.0h, 0.0h, 0.0h),
                    1.0h - roughness,
                    baseMap.a,
                    brdfData);

                half3 reflectionDirectionWS = GetAnisotropicReflectionDirection(
                    normalWS,
                    rotatedTangentWS,
                    rotatedBitangentWS,
                    viewDirectionWS,
                    roughness,
                    anisotropy);
                half3 indirectDiffuse = SampleSH(normalWS);
                half3 indirectSpecular = GlossyEnvironmentReflection(
                    reflectionDirectionWS,
                    input.positionWS,
                    roughness,
                    1.0h,
                    GetNormalizedScreenSpaceUV(input.positionHCS));
                half nDotV = saturate(dot(normalWS, viewDirectionWS));
                half fresnelTerm = Pow4(1.0h - nDotV);
                color += EnvironmentBRDF(
                    brdfData,
                    indirectDiffuse,
                    indirectSpecular,
                    fresnelTerm) * occlusion;

                color = MixFog(color, input.fogFactor);
                return half4(color, baseMap.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
