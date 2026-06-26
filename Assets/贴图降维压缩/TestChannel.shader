Shader "Unlit/TestChannel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
                _Center("Center", Vector) = (1,1,1,1)
        _TNormal("Normal", Vector) = (1,1,1,1)
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
        float4 _Center;
        float4 _TNormal;
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
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                float b = -((col.x - _Center.x) * _TNormal.x + (col.y - _Center.y) * _TNormal.y) / _TNormal.z + _Center.z;
                // return float4(b,b,b,1);
                return float4(col.rgb,1);
            }
            ENDCG
        }
    }
}
