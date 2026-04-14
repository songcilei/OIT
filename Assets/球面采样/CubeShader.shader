Shader "Unlit/CubeShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Up("Up",Vector) = (0,1,0,0)
        _Down("Down",Vector) = (0,-1,0,0)
        _Left("Left",Vector) = (1,0,0,0)
        _Right("Right",Vector) = (-1,0,0,0)
        _Front("Front",Vector) = (0,0,1,0)
        _Back("Back",Vector)=(0,0,-1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalForward"}
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
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float3 normal:NORMAL;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float3 _Up;
            float3 _Down;
            float3 _Left;
            float3 _Right;
            float3 _Front;
            float3 _Back;
            
            float4 cAmbientCube[6];
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = mul(v.normal, (float3x3)unity_WorldToObject);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float3 AmbientLight(const float3 worldNormal)
            {
                float3 nSquared = worldNormal*worldNormal;
                int3 isNegative = (worldNormal<0.0);
                float3 linearColor;
                linearColor = nSquared.x * cAmbientCube[isNegative.x].rgb+
                                nSquared.y * cAmbientCube[isNegative.y+2].rgb+
                                    nSquared.z * cAmbientCube[isNegative.z+4].rgb;
                return linearColor;//1.5为经验修正
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
               
                fixed4 col = float4(0,0,0,1);
            
                
                // col.rgb += saturate(dot(i.normal,float3(0,1,0)))*_Up;
                // col.rgb += saturate(dot(i.normal,float3(0,-1,0)))*_Down;
                //
                // col.rgb += saturate(dot(i.normal,float3(1,0,0)))*_Right;
                // col.rgb += saturate(dot(i.normal,float3(-1,0,0)))*_Left;
                //
                // col.rgb += saturate(dot(i.normal,float3(0,0,1)))*_Front;
                // col.rgb += saturate(dot(i.normal,float3(0,0,-1)))*_Back;
                
                // UNITY_APPLY_FOG(i.fogCoord, col);
                
                
                col.rgb = AmbientLight(i.normal);
                return col;
            }
            ENDCG
        }
    }
}
