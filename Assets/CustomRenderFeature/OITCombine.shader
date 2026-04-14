Shader "Unlit/OITCombineShader"
{
    Properties
    {
        [mainTexture]_MainTex ("Texture", 2D) = "white"{}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {

        Tags { "RenderType"="Transparent" "Queue"="Transparent" "LightMode" = "UniversalForward" }
        Pass
        {
    
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float z : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _OITColorTexture;
            float4 _OITColorTexture_ST;
            sampler2D _OITAlphaTexture;
            float4 _OITAlphaTexture_ST;
            sampler2D _CameraOpaqueTexture;
            float4 _TintColor;
            float4 _Color;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                
                
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 main = tex2D(_CameraOpaqueTexture, i.uv);
                float4 col = tex2D(_OITColorTexture, i.uv);
                float alpha = tex2D(_OITAlphaTexture,i.uv).r;
                // float4 color = float4(_Color.rgb * _Color.a, _Color.a);
                // return (1.0 - col.a) * col + col.a * background;
                float4 finalCol  = float4(col.rgb/clamp(col.a, 1e-4, 5e4),alpha);
                
                return (1.0f-finalCol.a)*finalCol + finalCol.a*main;
            }
            ENDCG
        }
    }
// 
}
