Shader "Unlit/DebugStaticShadow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Slider("Slider",Range(0,1))=1
        _offset("offset",Range(-0.1,0.1))=0
        
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline"}
   
        
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            
        
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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

            float _offset;
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

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
            float4 frag (v2f i) : SV_Target
            {
                float3 shadowBiasPos = ApplyShadowBias(i.shadowPos.rgb,-i.normal.rgb,_MainLightPosition.rgb,float2(0.1f,0.1f));
                float4 ScrPos =ComputeScreenPos(i.shadowPos);
                // sample the texture
                float3 screenShadowUV = (i.shadowPos.xyz/i.shadowPos.w)*0.5+0.5;
                float dp = ScrPos.z/ScrPos.w;
                
                float4 col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,ScrPos.xy/ScrPos.w*_Slider);
                // col.rgb /= col.a;


                float depth = (col);
                #if UNITY_REVERSED_Z
                    dp = 1 - dp; //(1, 0)-->(0, 1)
                    depth = 1-depth;
                #else
                    dp = dp * 0.5 + 0.5; //(-1, 1)-->(0, 1)
                    //depth = depth*0.5+0.5; //这里 加0.5*0.5会出错。。没想明白为啥
                #endif
                
                // float attenuation = real(SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz));
                float attenuation = (dp+_offset)<depth?1:0;
                // apply fog
                // UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(attenuation,attenuation,attenuation,1);
            }
            ENDHLSL
        }
    }
}
