Shader "Hidden/LiveBakeBridge/TextureExport"
{
    Properties { _MainTex("Texture",2D)="white"{} _DecodeNormal("Decode normal",Float)=0 }
    SubShader { Cull Off ZWrite Off ZTest Always Pass {
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex; float _DecodeNormal;
        float4 frag(v2f_img i):SV_Target {
            float4 c=tex2D(_MainTex,i.uv);
            if(_DecodeNormal>.5) return float4(UnpackNormal(c)*.5+.5,1);
            return c;
        }
        ENDCG
    }}
    Fallback Off
}
