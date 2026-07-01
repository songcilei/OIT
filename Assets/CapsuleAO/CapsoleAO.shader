Shader "Unlit/CapsoleAO"
{
    Properties
    {
        _rValue("r",Float)=0
        _pos("pos", Vector) = (0,0,0,0)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal:TEXCOORD2;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _rValue;
            float4 _pos;
            float _Sum;
            float _Diff;
            float _phaA;
            float _phaB;
            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;
                o.vertex = mul(UNITY_MATRIX_VP,float4(worldPos,1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(v.normal,(float3x3)unity_WorldToObject);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                
                float3 N = normalize(i.normal);
                float3 L = normalize(_MainLightPosition.xyz);
//Ambient Term----------------  bl(R,D) = cosA(r/d)^2
                float3 Pdir = normalize(_pos.xyz-i.worldPos);
                float as = dot(N,Pdir);
                float d = distance(i.worldPos,_pos.xyz);
                float bl = as*(_rValue/d)*(_rValue/d);
//Directional  Term--------------------------
                float angleBetweenAxes = acos( saturate( dot(N, Pdir) ) );
                float angleSum = _Sum;
                float angleDiff = _Diff;
                float t = smoothstep(angleSum, angleDiff, angleBetweenAxes);
                float cosPhiA = cos(_phaA);
                float cosPhiB = cos(_phaB);
                float intersectionCosTheta = lerp(1.0, max(cosPhiA, cosPhiB), t);
                float DirBl = (1.0 - intersectionCosTheta) / (1.0 - cosPhiA);
                // return float4(DirBl.rrr,1);
//----------------------------------------------
                float4 col = tex2D(_MainTex, i.uv);
                float4 finalColor = 1;
                finalColor.rgb = col.rgb*(1-bl);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
