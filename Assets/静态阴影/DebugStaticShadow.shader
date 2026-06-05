Shader "Unlit/DebugStaticShadow"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Slider("Slider",Range(0,1))=1
        _offset("offset",Range(-0.1,0.1))=0
//        [Toggle(_CUSTOMSHADOW)] _CustomShadow("customShadow",Int)=0
        
        
        [Toggle(_ESM)] _esm("指数阴影",Int) =0
        _EValue("Evalue",Float)=1
        [Toggle(_VSM)] _vsm("方差阴影",Int) = 0
        _ShadowTex("ShadowTex",2D)="black"{}
        _ShadowColor("ShadowColor",Color) = (0.5,0.5,0.5,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline"}
   
        
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            
            HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float UnpackDepth(float4 color)
            {
                float4 bitShift = float4(1.0, 1/255.0, 1/65025.0, 1/16581375.0);
                return dot(color, bitShift);
            }
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 shadowPos: TEXCOORD1;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 normal:NORMAL;
            };
            
            float4x4 _SMat;
            float _Slider;

            float4 _Color;
            float _offset;
            float _CustomShadow;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _ShadowColor;
            float _EValue;
            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);
            

 
            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.worldPos = worldPos;
                o.vertex = mul(UNITY_MATRIX_VP,float4(worldPos,1));
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(UNITY_MATRIX_M,float4(v.normal.xyz,0)).xyz;
                o.shadowPos = mul(_SMat, float4(worldPos,1));
   
                return o;
            }
            float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection, float2 shadowBias)
            {
                float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
                float scale = invNdotL * shadowBias.y;

                // normal bias is negative since we want to apply an inset normal offset
                positionWS = -lightDirection * shadowBias.xxx + positionWS;
                positionWS = -normalWS * scale.xxx + positionWS;
                return positionWS;
            }
            
 

            half4 frag (v2f i) : SV_TARGET
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(_MainLightPosition.xyz);
                    
                
                float NdotL  = saturate(dot(N,L)*0.5+0.5);
                
                
                half realtimeShadow = 1;
                // float3 shadowBiasPos = ApplyShadowBias(i.shadowPos.rgb,-i.normal.rgb,_MainLightPosition.rgb,float2(0.1f,0.1f));
                float4 ScrPos =ComputeScreenPos(i.shadowPos);//等价与  float3 screenShadowUV = (i.shadowPos.xyz/i.shadowPos.w)*0.5+0.5;
                // sample the texture
                float dp = ScrPos.z/ScrPos.w;
                float3 adobe = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Color*NdotL;
                float dist =1;
                #ifdef _CUSTOMSHADOW  //自定义静态投影
                    float2 vdepth = SAMPLE_TEXTURE2D(_ShadowTex,sampler_ShadowTex,ScrPos.xy/ScrPos.w*_Slider).rg;
                    #if UNITY_REVERSED_Z
                        dp = 1 - dp; //(1, 0)-->(0, 1)
                        vdepth.x = 1-vdepth.x;
                    #else
                        dp = dp * 0.5 + 0.5; //(-1, 1)-->(0, 1)  这里是Opengl的Z 是 (-1, 1)
                        //depth = depth*0.5+0.5; //这里 不用  是因为Depth 本身就是NDC 坐标系下的0-1
                    #endif
                
                    realtimeShadow = vdepth<(dp+_offset)?0:1;
                
                    #ifdef _ESM
                        float ddp =  dp-vdepth.x ;//当前深度-遮挡深度
                        realtimeShadow =  1-exp(_EValue*ddp); //本质是指数阴影 和 shadowmap分辨率相关 
                    #endif    
                    //
                    #ifdef _VSM
                         float depth =  dp-vdepth.x;
                         float variance = vdepth.y - (vdepth.x*vdepth.x);//方差
                         float p = (variance)/(variance + depth*depth);//单边切比雪夫不等式 p=投影命中的概率 
                         realtimeShadow = 1-max(saturate(p),dp<vdepth.x?0:1);
                    
                    #endif
                
                // return float4(realtimeShadow.rrr,1);
                    // dist = saturate(1-smoothstep(0 ,0.07,(dp-vdepth)));//ESM 

                #endif//实时投影
                
                    float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos);
                

                    float atten = MainLightRealtimeShadow(shadowCoord);
                #ifdef _ESM
                    float Rddp = atten-dp;
                    //realtimeShadow *= (exp(_EValue*Rddp));
                    realtimeShadow *= atten;
                
                #else
                    realtimeShadow *= atten;
                #endif
                
                adobe = lerp(_ShadowColor*adobe,adobe,realtimeShadow);


                
                return half4(adobe,1);
            }
            
            ENDHLSL
            
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CUSTOMSHADOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ESM
            #pragma multi_compile _ _VSM
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            // #pragma vertex vertDepth
            // #pragma fragment fragDepth
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

    }
}
