Shader "LiveBakeBridge/UV2 Unlit"
{
    Properties { _BakeTex("Baked appearance (UV2)",2D)="white" {} [HideInInspector] _BakeManualSRGB("Decode 16-bit PNG",Float)=0 }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            // No LightMode: built-in default pass and URP SRPDefaultUnlit.
            ZWrite On Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D_float _BakeTex; float _BakeManualSRGB; float4 _BakeTex_ST;
            struct appdata { float4 vertex:POSITION; float2 uv2:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv2*_BakeTex_ST.xy+_BakeTex_ST.zw;return o; }
            float4 frag(v2f i):SV_Target { float3 c=tex2D(_BakeTex,i.uv).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                if(_BakeManualSRGB>0.5) c=lerp(c/12.92,pow((c+0.055)/1.055,2.4),step(0.04045,c));
                #endif
                return float4(c,1); }
            ENDCG
        }
    }
    Fallback Off
}
