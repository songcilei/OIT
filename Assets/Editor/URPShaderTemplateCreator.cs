using System.IO;
using UnityEditor;
using UnityEngine;

public static class URPShaderTemplateCreator
{
    private const string DefaultUnlitName = "New URP Unlit Shader.shader";
    private const string DefaultLitPbrName = "New URP Lit PBR Shader.shader";
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

    [MenuItem("Assets/Create/Shader/URP/Normal Decal Shader Template", priority = 84)]
    private static void CreateNormalDecalShader()
    {
        StartShaderNameEditing(DefaultNormalDecalName, ShaderTemplateKind.NormalDecal);
    }

    [MenuItem("Assets/Create/Shader/URP/Optimize Decal Shader Template", priority = 85)]
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
            #pragma multi_compile_instancing

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
