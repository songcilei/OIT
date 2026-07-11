Shader "Unlit/VoxelCubeShader"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(UNITY_MATRIX_M,v.vertex);
                o.vertex = mul(UNITY_MATRIX_VP,float4(worldPos,1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(v.normal,(float3x3)unity_WorldToObject);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 L = normalize(_MainLightPosition);
                float3 LColor = _MainLightColor;
                float3 N = normalize(i.normal);
                float NdotL = saturate(dot(N,L));
                float4 col = tex2D(_MainTex, i.uv)*_Color;
                float4 finalColor =1;
                finalColor.rgb = col.rgb * LColor.rgb * NdotL;
                return _Color;
            }
            ENDHLSL
        }
    }
}
