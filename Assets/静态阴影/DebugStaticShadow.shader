Shader "Unlit/DebugStaticShadow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Slider("Slider",Float)=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            float UnpackDepth(float4 color)
            {
                float4 bitShift = float4(1.0, 1/255.0, 1/65025.0, 1/16581375.0);
                return dot(color, bitShift);
            }
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 shadowPos: TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };
            
            float4x4 _SMat;
            float _Slider;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.shadowPos = mul(_SMat, float4(worldPos,1));
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float2 screenShadowUV = (i.shadowPos.xy/i.shadowPos.w)*0.5+0.5;
                
                
                fixed4 col = tex2D(_MainTex, screenShadowUV*_Slider);
                float depth = UnpackDepth(col);
                // apply fog
                // UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(depth,depth,depth,1);
            }
            ENDCG
        }
    }
}
