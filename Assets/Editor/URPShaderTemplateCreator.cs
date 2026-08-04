using System.IO;
using UnityEditor;
using UnityEngine;

public static class URPShaderTemplateCreator
{
    private const string DefaultUnlitName = "New URP Unlit Shader.shader";
    private const string DefaultLitPbrName = "New URP Lit PBR Shader.shader";

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

    public static string CreateUnlitTemplate(string shaderName)
    {
        return UnlitTemplate.Replace("__SHADER_NAME__", shaderName);
    }

    public static string CreateLitPbrTemplate(string shaderName)
    {
        return LitPbrTemplate.Replace("__SHADER_NAME__", shaderName);
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
        LitPbr
    }

    private sealed class CreateShaderEndNameEditAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public ShaderTemplateKind TemplateKind;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string shaderName = Path.GetFileNameWithoutExtension(pathName);
            string content = TemplateKind == ShaderTemplateKind.Unlit
                ? CreateUnlitTemplate($"Custom/URP/{shaderName}")
                : CreateLitPbrTemplate($"Custom/URP/{shaderName}");

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

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                return baseMap * _BaseColor;
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
        _Smoothness (""Smoothness"", Range(0, 1)) = 0.5
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
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
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w);
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
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

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
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
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

                color += inputData.bakedGI * brdfData.diffuse;
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color, surfaceData.alpha);
            }
            ENDHLSL
        }
    }

    FallBack ""Hidden/Universal Render Pipeline/FallbackError""
}
";
}
