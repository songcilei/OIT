Shader "Unlit/transparentShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Matcap("Matcap",2D)="balck"{}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "LightMode" = "UniversalForward" }
        LOD 100

        Pass
        {
            Blend One One
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #define multi_compile _ _WEIGHTED1
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_FOG_COORDS(1)
                float3 worldPos:TEXCOORD2;
                float4 vertex : SV_POSITION;
                float z:TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            
            sampler2D _Matcap;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(v.normal,(float3x3)UNITY_MATRIX_M);
                o.z = abs(mul(UNITY_MATRIX_MV,v.vertex).z);
                o.worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
			float w(float z, float alpha) {
				#ifdef _WEIGHTED0
					return pow(z, -2.5);
				#elif _WEIGHTED1
					return alpha * max(1e-2, min(3 * 1e3, 10.0/(1e-5 + pow(z/5, 2) + pow(z/200, 6))));
				#elif _WEIGHTED2
					return alpha * max(1e-2, min(3 * 1e3, 0.03/(1e-5 + pow(z/200, 4))));
				#endif
				return 1.0;
			}
            
            float2 MatCapUVNormal(float3 worldNormal){
                half2 matCapUV ;
                //matCapUV.x = dot(UNITY_MATRIX_IT_MV[0].xyz,N);
                //matCapUV.y = dot(UNITY_MATRIX_IT_MV[1].xyz,N);
                matCapUV = mul(UNITY_MATRIX_V,worldNormal);
                matCapUV = matCapUV * 0.5 + 0.5;
                return matCapUV.xy;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos.xyz);
                float NdotV = saturate(dot(N,V));
                float NdotL = saturate(dot(N,L)*0.5+0.5);
                
                float3 matcap = tex2D(_Matcap,MatCapUVNormal(N));
                //fresnel
                float fresnel = saturate(pow(1.0 - NdotV, 5.0));
                
                fixed4 col = tex2D(_MainTex, i.uv)*_Color;

                col.rgb*=NdotL;
                col.rgb += matcap;
                // col.rgb +=fresnel;
                float4 finalColor = float4(col.rgb*col.a*w(i.z, col.a),col.a);
                
                return finalColor;
            }
            ENDCG
        }
    }
}
