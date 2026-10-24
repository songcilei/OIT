Shader "Unlit/MiniFogTest"
{
    Properties
    {
        _FogColor("FogColor",Color)=(1,1,1,1)
        _FogColor2("FogColor2",Color)=(1,1,1,1)
        _FogDensity("Density",Float)=0
        _FogPow("Pow",Float)=1
//        _MainTex ("Texture", 2D) = "white" {}
        _HeightMap("HeightMap",2D)="black" {}
        _FogSpeed("FogSpeed",Vector)=(0,0,0,0)
        _NoiseMap("NoiseMap",2D)="black"{}
        _NosieHeight("Noise=>MapInfo",Range(0,0.2)) = 0.1
        _MinFog("MinFog",Range(-1,2)) = 0
        _MaxFog("MaxFog",Range(-1,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
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
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };
            sampler2D _HeightMap;
            sampler2D _NoiseMap;
            // sampler2D _MainTex;
            // float4 _MainTex_ST;
            float4 _HeightMap_ST;
            sampler2D _MapInfo;
            float4 _NoiseMap_ST;
            float4 _FogColor,_FogColor2;
            
            float4 _FogSpeed;            
            float _FogDensity;
            float _FogPow;
            float _NosieHeight;
            float _MinFog;
            float _MaxFog;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = 1;
                float height = tex2D(_HeightMap,i.uv*_HeightMap_ST.xy+_HeightMap_ST.zw);
                height = exp(height)*_FogPow;
                float2 FogUV = i.uv*_NoiseMap_ST.xy+_NoiseMap_ST.zw + _Time.y * _FogSpeed.xy/10;
                float noise = tex2D(_NoiseMap,FogUV).r;
                float4 Fog = lerp(_FogColor,_FogColor2,noise);
                float alpha = saturate(height-_FogDensity)+saturate(1-Fog.a);

                float map = 1-tex2D(_MapInfo,i.uv+noise*_NosieHeight).r;
                // col = lerp(Fog,col,alpha);
                col = Fog;
                map = smoothstep(_MinFog,_MaxFog,map);
                col.a = pow(saturate(1 - alpha),2.2)*map;
                return col;
            }
            ENDCG
        }
    }
}
