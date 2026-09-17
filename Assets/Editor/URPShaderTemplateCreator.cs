using System.IO;
using UnityEditor;
using UnityEngine;

public static class URPShaderTemplateCreator
{
    private const string DefaultUnlitName = "New URP Unlit Shader.shader";
    private const string DefaultLitPbrName = "New URP Lit PBR Shader.shader";
    private const string DefaultAnisotropicLitPbrName = "New URP Anisotropic Lit PBR Shader.shader";
    private const string DefaultNormalDecalName = "New URP Normal Decal Shader.shader";
    private const string DefaultOptimizeDecalName = "New URP Optimize Decal Shader.shader";

    [MenuItem("Assets/Create/Shader/URP/Unlit Shader Template", priority = 82)]
    private static void CreateUnlitShader()
    {
        StartShaderNameEditing(DefaultUnlitName, ShaderTemplateKind.Unlit);
    }

    [MenuItem("Assets/Create/Shader/URP/Lit PBR Shader Template", priority = 83)]
    private static void CreateLitPbrShader()
    {
        StartShaderNameEditing(DefaultLitPbrName, ShaderTemplateKind.LitPbr);
    }

    [MenuItem("Assets/Create/Shader/URP/Anisotropic Lit PBR Shader Template", priority = 84)]
    private static void CreateAnisotropicLitPbrShader()
    {
        StartShaderNameEditing(DefaultAnisotropicLitPbrName, ShaderTemplateKind.AnisotropicLitPbr);
    }

    [MenuItem("Assets/Create/Shader/URP/Normal Decal Shader Template", priority = 85)]
    private static void CreateNormalDecalShader()
    {
        StartShaderNameEditing(DefaultNormalDecalName, ShaderTemplateKind.NormalDecal);
    }

    [MenuItem("Assets/Create/Shader/URP/Optimize Decal Shader Template", priority = 86)]
    private static void CreateOptimizeDecalShader()
    {
        StartShaderNameEditing(DefaultOptimizeDecalName, ShaderTemplateKind.OptimizeDecal);
    }

    public static string CreateUnlitTemplate(string shaderName)
    {
        return UnlitTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    public static string CreateLitPbrTemplate(string shaderName)
    {
        return LitPbrTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    public static string CreateAnisotropicLitPbrTemplate(string shaderName)
    {
        return AnisotropicLitPbrTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    public static string CreateNormalDecalTemplate(string shaderName)
    {
        return NormalDecalTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    public static string CreateOptimizeDecalTemplate(string shaderName)
    {
        return OptimizeDecalTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    private static void StartShaderNameEditing(string defaultFileName, ShaderTemplateKind templateKind)
    {
        string directory = GetSelectedDirectory();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{defaultFileName}");
        var action = ScriptableObject.CreateInstance<CreateShaderEndNameEditAction>();
        action.TemplateKind = templateKind;

        Texture2D icon = EditorGUIUtility.IconContent("Shader Icon").image as Texture2D;
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, assetPath, icon, null);
    }

    private static string GetSelectedDirectory()
    {
        string path = "Assets";

        foreach (Object selectedObject in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string selectedPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(selectedPath))
                continue;

            path = Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath);
            break;
        }

        return string.IsNullOrEmpty(path) ? "Assets" : path.Replace("\\", "/");
    }

    private enum ShaderTemplateKind
    {
        Unlit,
        LitPbr,
        AnisotropicLitPbr,
        NormalDecal,
        OptimizeDecal
    }

    private sealed class CreateShaderEndNameEditAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public ShaderTemplateKind TemplateKind;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string shaderName = Path.GetFileNameWithoutExtension(pathName);
            string shaderNameWithPath = $"Custom/URP/{shaderName}";
            string content;

            switch (TemplateKind)
            {
                case ShaderTemplateKind.Unlit:
                    content = CreateUnlitTemplate(shaderNameWithPath);
                    break;
                case ShaderTemplateKind.LitPbr:
                    content = CreateLitPbrTemplate(shaderNameWithPath);
                    break;
                case ShaderTemplateKind.AnisotropicLitPbr:
                    content = CreateAnisotropicLitPbrTemplate(shaderNameWithPath);
                    break;
                case ShaderTemplateKind.NormalDecal:
                    content = CreateNormalDecalTemplate(shaderNameWithPath);
                    break;
                default:
                    content = CreateOptimizeDecalTemplate(shaderNameWithPath);
                    break;
            }

            File.WriteAllText(pathName, content);
            AssetDatabase.ImportAsset(pathName);

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(pathName);
            ProjectWindowUtil.ShowCreatedAsset(asset);
        }
    }

    private const string UnlitTemplate = @"Shader ""__SHADER_NAME__""
{
    Properties
    {
        _BaseMap (""Base Map"", 2D) = ""white"" {}
        _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            ""RenderType"" = ""Opaque""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Geometry""
        }

        Pass
        {
            Name ""ForwardUnlit""
            Tags { ""LightMode"" = ""UniversalForward"" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(URPShaderTemplateProperties)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(URPShaderTemplateProperties)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(URPShaderTemplateProperties, _BaseColor);
                return baseMap * baseColor;
            }
            ENDHLSL
        }
    }

    FallBack ""Hidden/Universal Render Pipeline/FallbackError""
}
";

    private const string LitPbrTemplate = @"Shader ""__SHADER_NAME__""
{
    Properties
    {
        _BaseMap (""Base Map"", 2D) = ""white"" {}
        _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
        _BumpMap (""Normal Map"", 2D) = ""bump"" {}
        _Metallic (""Metallic"", Range(0, 1)) = 0
        _MetalTex(""MetalTex"",2D)=""white""{}
        _Smoothness (""Smoothness"", Range(0, 1)) = 0.5
        _SmoothTex(""SmoothTex"",2D)=""white""{}
    }

    SubShader
    {
        Tags
        {
            ""RenderType"" = ""Opaque""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Geometry""
        }

        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode"" = ""UniversalForward"" }

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

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

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
        UsePass ""Universal Render Pipeline/Lit/ShadowCaster""
        UsePass ""Universal Render Pipeline/Lit/DepthOnly""
    }

    FallBack ""Hidden/Universal Render Pipeline/FallbackError""
}
";

    private const string AnisotropicLitPbrTemplate = @"Shader ""__SHADER_NAME__""
{
    Properties
    {
        [MainTexture] _BaseMap (""Base Map"", 2D) = ""white"" {}
        [MainColor] _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap (""Normal Map"", 2D) = ""bump"" {}
        _NormalScale (""Normal Scale"", Range(0, 2)) = 1

        _MetallicMap (""Metallic Map"", 2D) = ""white"" {}
        _Metallic (""Metallic"", Range(0, 1)) = 0
        _RoughnessMap (""Roughness Map"", 2D) = ""white"" {}
        _Roughness (""Roughness"", Range(0, 1)) = 0.5
        _OcclusionMap (""Occlusion Map"", 2D) = ""white"" {}
        _OcclusionStrength (""Occlusion Strength"", Range(0, 1)) = 1

        // R controls strength. G rotates the anisotropy direction by one turn.
        _AnisotropyMap (""Anisotropy (R) Direction (G)"", 2D) = ""white"" {}
        _Anisotropy (""Anisotropy"", Range(-0.95, 0.95)) = 0
        _AnisotropyRotation (""Anisotropy Rotation"", Range(0, 1)) = 0

        [HideInInspector] _Cutoff (""Alpha Cutoff"", Range(0, 1)) = 0.5
        [HideInInspector] _Surface (""Surface"", Float) = 0
        [HideInInspector] _AlphaClip (""Alpha Clip"", Float) = 0
        [HideInInspector] _Cull (""Cull"", Float) = 2
    }

    SubShader
    {
        Tags
        {
            ""RenderType"" = ""Opaque""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Geometry""
        }

        Pass
        {
            Name ""ForwardAnisotropicLit""
            Tags { ""LightMode"" = ""UniversalForward"" }

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

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

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

        UsePass ""Universal Render Pipeline/Lit/ShadowCaster""
        UsePass ""Universal Render Pipeline/Lit/DepthOnly""
    }

    FallBack ""Hidden/Universal Render Pipeline/FallbackError""
}
";

    private const string NormalDecalTemplate = @"Shader ""__SHADER_NAME__""
{
    Properties
    {
        _BaseMap (""Base Map"", 2D) = ""white"" {}
        _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
        _Opacity (""Opacity"", Range(0, 1)) = 1
        _Cutoff (""Alpha Cutoff"", Range(0, 1)) = 0.001
    }

    SubShader
    {
        Tags
        {
            ""RenderType"" = ""Transparent""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Transparent""
            ""IgnoreProjector"" = ""True""
        }

        Pass
        {
            Name ""NormalDepthDecal""
            Tags { ""LightMode"" = ""SRPDefaultUnlit"" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.positionOS = input.positionOS.xyz;
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
";

    private const string OptimizeDecalTemplate = @"Shader ""__SHADER_NAME__""
{
    Properties
    {
        _BaseMap (""Base Map"", 2D) = ""white"" {}
        _BaseColor (""Base Color"", Color) = (1, 1, 1, 1)
        _Opacity (""Opacity"", Range(0, 1)) = 1

        [Toggle(_ProjectionAngleDiscardEnable)] _ProjectionAngleDiscardEnable (""Projection Angle Discard Enable"", Float) = 0
        _ProjectionAngleDiscardThreshold (""Projection Angle Discard Threshold"", Range(-1, 1)) = 0
        [Toggle(_UnityFogEnable)] _UnityFogEnable (""Unity Fog Enable"", Float) = 1
        [Toggle(_SupportOrthographicCamera)] _SupportOrthographicCamera (""Support Orthographic Camera"", Float) = 0
    }

    SubShader
    {
        Tags
        {
            ""RenderType"" = ""Overlay""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""Queue"" = ""Transparent-499""
            ""IgnoreProjector"" = ""True""
        }

        Pass
        {
            Name ""OptimizeDepthDecal""
            Tags { ""LightMode"" = ""UniversalForward"" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ProjectionAngleDiscardEnable
            #pragma shader_feature_local _UnityFogEnable
            #pragma shader_feature_local_fragment _SupportOrthographicCamera

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1;
                float4 cameraPosOSAndFogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _ProjectionAngleDiscardThreshold;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;

                #if _UnityFogEnable
                    output.cameraPosOSAndFogFactor.a = ComputeFogFactor(output.positionHCS.z);
                #else
                    output.cameraPosOSAndFogFactor.a = 0;
                #endif

                output.screenPos = ComputeScreenPos(positionInputs.positionCS);

                float3 viewRay = positionInputs.positionVS;
                output.viewRayOS.w = viewRay.z;
                viewRay *= -1;

                float4x4 viewToObjectMatrix = mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V);
                output.viewRayOS.xyz = mul((float3x3)viewToObjectMatrix, viewRay);
                output.cameraPosOSAndFogFactor.xyz =
                    mul(viewToObjectMatrix, float4(0, 0, 0, 1)).xyz;
                return output;
            }

            #if SHADER_LIBRARY_VERSION_MAJOR < 12
            float LinearDepthToEyeDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return _ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
                #else
                    return _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
                #endif
            }
            #endif

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                input.viewRayOS.xyz /= input.viewRayOS.w;

                float2 screenSpaceUV = input.screenPos.xy / input.screenPos.w;
                float sceneRawDepth =
                    SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenSpaceUV).r;

                float3 decalSpaceScenePos;
                #if _SupportOrthographicCamera
                    if (unity_OrthoParams.w)
                    {
                        float sceneDepthVS = LinearDepthToEyeDepth(sceneRawDepth);
                        float2 viewRayEndPosVSXY =
                            unity_OrthoParams.xy * (input.screenPos.xy - 0.5) * 2;
                        float4 vposOrtho = float4(viewRayEndPosVSXY, -sceneDepthVS, 1);
                        float3 wposOrtho = mul(UNITY_MATRIX_I_V, vposOrtho).xyz;
                        decalSpaceScenePos =
                            mul(GetWorldToObjectMatrix(), float4(wposOrtho, 1)).xyz;
                    }
                    else
                    {
                #endif
                        float sceneDepthVS = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                        decalSpaceScenePos =
                            input.cameraPosOSAndFogFactor.xyz + input.viewRayOS.xyz * sceneDepthVS;
                #if _SupportOrthographicCamera
                    }
                #endif

                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;

                float shouldClip = 0;
                #if _ProjectionAngleDiscardEnable
                    float3 decalSpaceHardNormal =
                        normalize(cross(ddx(decalSpaceScenePos), ddy(decalSpaceScenePos)));
                    shouldClip =
                        decalSpaceHardNormal.z > _ProjectionAngleDiscardThreshold ? 1 : 0;
                #endif

                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);

                float2 uv = decalSpaceUV * _BaseMap_ST.xy + _BaseMap_ST.zw;

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                color *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                color.a *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Opacity);

                #if _UnityFogEnable
                    color.rgb = MixFog(color.rgb, input.cameraPosOSAndFogFactor.a);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
";
}
