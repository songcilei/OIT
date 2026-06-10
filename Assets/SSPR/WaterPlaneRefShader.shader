Shader "Unlit/WaterPlaneRefShader"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }
        LOD 100

        Pass
        {
            Name "SSPR Reflector Pass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"    

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
         
                float4 positionCS : SV_POSITION;
                float4 positionNDC : TEXCOORD1;
            };

            TEXTURE2D(_SSPRReflectionTexture);
            SAMPLER(sampler_SSPRReflectionTexture);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(v.vertex);
                o.positionCS = vertexInputs.positionCS;
                o.positionNDC = vertexInputs.positionNDC;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 suv = i.positionNDC.xy / i.positionNDC.w;
                half3 RefCol = SAMPLE_TEXTURE2D(_SSPRReflectionTexture,sampler_SSPRReflectionTexture,suv);
                
                half3 mainColor = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                half3 finalColor = 1;
                finalColor = mainColor *_Color
                            +saturate(RefCol)
                ;
                return float4(finalColor.rgb,1);
            }
            ENDHLSL
        }
    }
} 
