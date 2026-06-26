
Shader "URP/CMPShader"
{
    Properties
    {
        // 基础纹理
        [MainColor] _BaseColor ("Color", Color) = (0.5, 0.5, 0.5, 1)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" { }
        
        // 透明度裁剪
        [Toggle(_ALPHATEST_ON)] _EnableAlphaTest ("Alpha Cutoff", Float) = 0.0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        
        // 法线贴图
        [Toggle(_NORMALMAP)] _EnableBumpMap ("Bump Map", Float) = 0.0
        _BumpScale ("Normal Scale", Float) = 1.0
//        _BumpMap ("Normal Map", 2D) = "bump" { }
        
        // 漫反射叠加颜色
        _Diffuse ("Diffuse", Color) = (1, 1, 1, 1)
        // 高光反射叠加颜色
        _Specular ("Specular", Color) = (1, 1, 1, 1)
        // 高光系数
        _Gloss ("Gloss", Range(8.0, 256)) = 20
        
        _Center("Center", Vector) = (1,1,1,1)
        _TNormal("Normal", Vector) = (1,1,1,1)
        // 是否计算多光源
        [Toggle(_AdditionalLights)] _AddLights ("AddLights", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _BaseMap_ST;
        half _Cutoff;
        float4 _BumpMap_ST;
        float _BumpScale;
        float4 _Diffuse;
        float4 _Specular;
        float _Gloss;
        float4 _Center;
        float4 _TNormal;
        CBUFFER_END
        ENDHLSL
        
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            
            // 设置关键字
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _AdditionalLights
            
            // 接收阴影所需关键字
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            float4 _BaseMap_TexelSize;
            
            struct Attributes
            {
                float4 positionOS: POSITION;//位置
                float3 normalOS: NORMAL;//法线
                float4 tangentOS: TANGENT;//切线
                float2 texcoord: TEXCOORD0;//纹理坐标
            };
            
            struct Varyings
            {
                float4 positionCS: SV_POSITION;
                float2 uv: TEXCOORD0;
                float3 positionWS: TEXCOORD1;
                
                #ifdef _NORMALMAP//如果使用了法线贴图
                    float4 normalWS: TEXCOORD3;    // xyz: normal, w: viewDir.x
                    float4 tangentWS: TEXCOORD4;    // xyz: tangent, w: viewDir.y
                    float4 bitangentWS: TEXCOORD5;    // xyz: bitangent, w: viewDir.z
                #else
                    float3 normalWS: TEXCOORD3;
                    float3 viewDirWS: TEXCOORD4;
                #endif
            };
            
            
            
            Varyings vert(Attributes v)
            {
                Varyings o;
                // 获取不同空间下坐标信息
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = positionInputs.positionCS;
                o.positionWS = positionInputs.positionWS;
                
                
                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
                
                // 获取世界空间下法线相关向量
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                // 视角方向
                half3 viewDirWS = GetCameraPositionWS() - positionInputs.positionWS;
                
                #ifdef _NORMALMAP// 如果使用了法线贴图
                    o.normalWS = half4(normalInput.normalWS, viewDirWS.x);
                    o.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
                    o.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
                #else
                    o.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
                    o.viewDirWS = viewDirWS;
                #endif
                
                return o;
            }
            
            /// albedo：反射系数
            /// lightColor：光源颜色
            /// lightDirectionWS：世界空间下光线方向
            /// lightAttenuation：光照衰减
            /// normalWS：世界空间下法线
            /// viewDirectionWS：世界空间下视角方向
            half3 LightingBased(half3 albedo, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half3 normalWS, half3 viewDirectionWS)
            {
                // 兰伯特漫反射计算
                half NdotL = saturate(dot(normalWS, lightDirectionWS));
                half3 radiance = lightColor * (lightAttenuation * NdotL) * _Diffuse.rgb;
                // BlinnPhong高光反射
                half3 halfDir = normalize(lightDirectionWS + viewDirectionWS);
                half3 specular = lightColor * pow(saturate(dot(normalWS, halfDir)), _Gloss) * _Specular.rgb;
                
                return(radiance + specular) * albedo;
            }
            // 计算漫反射与高光
            half3 LightingBased(half3 albedo, Light light, half3 normalWS, half3 viewDirectionWS)
            {
                // 注意light.distanceAttenuation * light.shadowAttenuation，这里已经将距离衰减与阴影衰减进行了计算
                return LightingBased(albedo, light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation, normalWS, viewDirectionWS);
            }
            
            half4 frag(Varyings i): SV_Target
            {
                half3 viewDirWS ;
                half3 normalWS;
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv+_BaseMap_TexelSize.xy*0.5f).rgb ;
                float3 c = albedo;
                _Center = pow(_Center,2.2f);
                // _TNormal = pow(_TNormal,2.2f);
             
                float b = -((c.x - _Center.x) * _TNormal.x + (c.y - _Center.y) * _TNormal.y) / _TNormal.z + _Center.z;
                // float b = (c.x-pow(_Center.x,2.2f));
                albedo.b = b;
                return float4(albedo,1);
                
                // b = pow(abs(b), 1/2.0f);
                #ifdef _NORMALMAP// 是否使用法线纹理
                    viewDirWS = half3(i.normalWS.w, i.tangentWS.w, i.bitangentWS.w);
                    //可以使用该方法替代下面的法线纹理采样，但是需要引用函数库ShaderLibrary/SurfaceInput.hlsl
                    //half3 normalTS = SampleNormal(i.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                    //or
                    // half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    normalTS =  UnpackNormalScale(float4(albedo.b,albedo.a,0,0),_BumpScale);
                    normalWS = TransformTangentToWorld(normalTS, half3x3(i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz));
                #else
                    viewDirWS = i.viewDirWS;
                    normalWS = i.normalWS;
                #endif
                
                normalWS = NormalizeNormalPerPixel(normalWS);
                viewDirWS = SafeNormalize(viewDirWS);
                
                //纹理采样
                //可以使用该方法替代下面的纹理采集操作，但是需要引用函数库ShaderLibrary/SurfaceInput.hlsl
                //half4 albedoAlpha = SampleAlbedoAlpha(i.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                // or
                // half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                //
                
                // 透明度裁剪
                //可以使用该方法替代下面的裁剪操作，但是需要引用函数库ShaderLibrary/SurfaceInput.hlsl
                //half alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
                //or
                #if defined(_ALPHATEST_ON)
                    clip(albedoAlpha.a - _Cutoff);
                #endif
                
                // 漫反射系数
                //half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                // or

                
                
                // 获取阴影坐标
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS.xyz);
                
                // 计算主光源与阴影
                Light mainLight = GetMainLight(shadowCoord);
                half3 diffuse = LightingBased(albedo, mainLight, normalWS, viewDirWS);
                
                // 计算其他光源与阴影
                // 需要将光源组件的ShadowType参数打开，并且将管线中的CastShadows勾选，副光源才会产生阴影
                #ifdef _AdditionalLights
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++ lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, i.positionWS);
                        diffuse += LightingBased(albedo, light, normalWS, viewDirWS);
                    }
                #endif
                
                // 计算环境光
                half3 ambient = SampleSH(normalWS) * albedo;
                return half4(ambient + diffuse, 1.0);
            }
            
            ENDHLSL
            
        }
        
        // 计算阴影的Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            
            // 设置关键字
            #pragma shader_feature _ALPHATEST_ON
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct Attributes
            {
                float4 positionOS: POSITION;
                float3 normalOS: NORMAL;
                float2 texcoord: TEXCOORD0;
            };
            
            struct Varyings
            {
                float2 uv: TEXCOORD0;
                float4 positionCS: SV_POSITION;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            // 获取裁剪空间下的阴影坐标
            float4 GetShadowPositionHClips(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 获取阴影专用裁剪空间下的坐标
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                // 判断是否是在DirectX平台翻转过坐标
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return positionCS;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = GetShadowPositionHClips(input);
                return output;
            }
            
            
            half4 frag(Varyings input): SV_TARGET
            {
                //可以使用该方法替代下面的裁剪操作，但是需要引用函数库ShaderLibrary/SurfaceInput.hlsl
                //Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                //or
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                    clip(albedoAlpha.a - _Cutoff);
                #endif
                
                return 0;
            }
            
            ENDHLSL            
        }
    }
    FallBack "Packages/com.unity.render-pipelines.universal/FallbackError"
}
