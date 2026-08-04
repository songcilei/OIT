Shader "Custom/URP/OptimizeDecal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Opacity("Opacity",Range(0,1)) = 1
        
        
        [Toggle(_ProjectionAngleDiscardEnable)] _ProjectionAngleDiscardEnable("_ProjectionAngleDiscardEnable (default = off)", float) = 0
        _ProjectionAngleDiscardThreshold("剔除拉伸角度 (default = 0)", range(-1,1)) = 0
        [Toggle(_UnityFogEnable)] _UnityFogEnable("启用Fog (default = on)", Float) = 1
        [Toggle(_SupportOrthographicCamera)] _SupportOrthographicCamera("支持正交相机 (default = off)", Float) = 0
        
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-499"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OptimizeDepthDecal"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            Zwrite Off
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1; // xyz: viewRayOS, w: extra copy of positionVS.z 
                float4 cameraPosOSAndFogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Opacity;
                float _ProjectionAngleDiscardThreshold;
            CBUFFER_END
            


            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS);
                output.positionHCS = positionInputs.positionCS;
                
#if _UnityFogEnable
                output.cameraPosOSAndFogFactor.a = ComputeFogFactor(output.positionHCS.z);      
#else
                output.cameraPosOSAndFogFactor.a = 0;           
#endif                
                
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                
                float3 viewRay = positionInputs.positionVS;
                output.viewRayOS.w = viewRay.z;
                
                viewRay*=-1;
                
                float4x4 ViewToObjectMatrix = mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V);
                output.viewRayOS.xyz = mul((float3x3)ViewToObjectMatrix, viewRay);
                output.cameraPosOSAndFogFactor.xyz = mul(ViewToObjectMatrix,float4(0,0,0,1)).xyz;
                return output;
            }
            
            // copied from URP12.1.2's ShaderVariablesFunctions.hlsl
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
                
                float2 screenSpaceUV = input.screenPos.xy / input.screenPos.w;;
                
                float sceneRawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,screenSpaceUV).r;
                float3 decalSpaceScenePos;
#if _SupportOrthographicCamera
                 if(unity_OrthoParams.w)
                {
                    float sceneDepthVS = LinearDepthToEyeDepth(sceneRawDepth);
				    float2 viewRayEndPosVS_xy = float2(unity_OrthoParams.xy * (input.screenPos.xy - 0.5) * 2 /* to clip space */);  // Ortho near/far plane xy pos 
				    float4 vposOrtho = float4(viewRayEndPosVS_xy, -sceneDepthVS, 1);                                            // Constructing a view space pos
				    float3 wposOrtho = mul(UNITY_MATRIX_I_V, vposOrtho).xyz;                                                 // Trans. view space to world space
                    decalSpaceScenePos = mul(GetWorldToObjectMatrix(), float4(wposOrtho, 1)).xyz;
                }
                else
                {
#endif
                    float sceneDepthVS = LinearEyeDepth(sceneRawDepth,_ZBufferParams);
                    decalSpaceScenePos = input.cameraPosOSAndFogFactor.xyz + input.viewRayOS.xyz * sceneDepthVS;
#if _SupportOrthographicCamera
                                }
#endif
                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;
                
                float shouldClip = 0;
#if _ProjectionAngleDiscardEnable
                float3 decalSpaceHardNormal = normalize(cross(ddx(decalSpaceScenePos), ddy(decalSpaceScenePos)));//reconstruct scene hard normal using scene pos ddx&ddy
                shouldClip = decalSpaceHardNormal.z > _ProjectionAngleDiscardThreshold ? 1 : 0;
#endif
                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);
                
                float2 uv = decalSpaceUV.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;//Texture tiling & offset

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,uv);
                col *= _BaseColor;// tint color
                col.a *= _Opacity;
                
#if _UnityFogEnable
                col.rgb = MixFog(col.rgb, input.cameraPosOSAndFogFactor.a);
#endif
                return col;
                
            }
            ENDHLSL
        }
    }
}
