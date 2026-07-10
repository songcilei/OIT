Shader "Unlit/TopLayerShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
//        _SplitMap("Mask",2D)="white"{}
        _DistorTex("DistorTex",2D) = "black"{}
        _NoiseColor("NoiseColor",Color)=(1,1,1,1)
        _NoiseScale("NoiseScale",Range(0,1))=1
        _Noise("Noise",2D)="white"{}
        _NoiseUVScale("NoiseUVScale",Range(0,1))=0
        _NoiseSpeed("NoiseSpeed", Vector)=(0,0,0,0)
//        _Smin("Min",Range(-1,1)) = 0
//        _Smax("Max",Range(0,2)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcALpha OneMinusSrcAlpha
            Zwrite off
            
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
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _SplitMap;
            float4 _SplitMap_ST;
            float _NoiseScale;
            float4 _NoiseSpeed;
            sampler2D _Noise;
            float4 _Noise_ST;
            float _NoiseUVScale;
            float4 _NoiseColor;
            
            sampler2D _DistorTex;
            float4 _DistorTex_ST;
            
            // float _Smin,_Smax;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 mask = tex2D(_SplitMap, i.uv);
                
                fixed4 distor = tex2D(_DistorTex, i.uv*_DistorTex_ST.xy+  _DistorTex_ST.zw);
                // mask = smoothstep(_Smin,_Smax,mask.r);
                // return float4(mask.rrr,1);
                float2 newUV1 = i.uv* _Noise_ST.xy+ _Noise_ST.zw + _Time.y * _NoiseSpeed.xy;
                float2 newUV2 = i.uv* _Noise_ST.xy*_NoiseUVScale+ _Noise_ST.zw + _Time.y * _NoiseSpeed.zw;
                fixed4 noise1 = tex2D(_Noise, newUV1)*_NoiseColor;
                fixed4 noise2 = tex2D(_Noise, newUV2)*_NoiseColor;
                float range = saturate((noise1*noise2)+mask.r*_NoiseScale);
                col.rgb = lerp(col.rgb,noise1+noise2,range);

                return float4(col.rgb,1);
            }
            ENDCG
        }
    }
}
