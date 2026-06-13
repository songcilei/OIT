 Shader "Unlit/TerrainShader"
{
    Properties
    {
        SpaltIDTex ("Texture", 2D) = "white" {}
        SpaltWeightTex("BlendTex",2D) = "white"{}
        AlbedoAtlas("TerrainLayer",2DArray) = "white"{}
        _Index("Index",Int)=1
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
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal :NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal :NORMAL;
            };

            sampler2D SpaltIDTex;
            float4 SpaltIDTex_ST;
            sampler2D SpaltWeightTex;
            float4 SpaltWeightTex_ST;
            int _Index;
            UNITY_DECLARE_TEX2DARRAY(AlbedoAtlas);
            float4 AlbedoAtlas_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, SpaltIDTex);
                o.normal = mul(v.normal,(float3x3)unity_WorldToObject);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float4 id = tex2D(SpaltIDTex, i.uv)*16;
                float4 weight = tex2D(SpaltWeightTex,i.uv).rgba;
                
                float3 LDir = normalize(UnityWorldSpaceLightDir(i.vertex.xyz));
                float3 LightColor = _LightColor0.rgb;
                float3 N = normalize(i.normal);
                float NdotL = saturate(dot(N, LDir));
                
                
                
                
                float4 col = UNITY_SAMPLE_TEX2DARRAY(AlbedoAtlas, float3(i.uv.xy*AlbedoAtlas_ST.xy + AlbedoAtlas_ST.zw, _Index+id.r));
                float4 col2 = UNITY_SAMPLE_TEX2DARRAY(AlbedoAtlas, float3(i.uv.xy*AlbedoAtlas_ST.xy + AlbedoAtlas_ST.zw, _Index+id.g));
                float4 col3 = UNITY_SAMPLE_TEX2DARRAY(AlbedoAtlas, float3(i.uv.xy*AlbedoAtlas_ST.xy + AlbedoAtlas_ST.zw, _Index+id.b));
                float4 col4 = UNITY_SAMPLE_TEX2DARRAY(AlbedoAtlas, float3(i.uv.xy*AlbedoAtlas_ST.xy + AlbedoAtlas_ST.zw, _Index+id.a));
                float4 finalColor =  col*weight.r + col2 * weight.g+ col3*weight.b + col4 * weight.a;
                // fianlColor = lerp(col2,col,saturate(weight));
                // return float4(id.rg,0,1);
                finalColor.rgb = finalColor.rgb * LightColor * NdotL;
                return finalColor;
            }
            ENDCG
        }
    }
}
