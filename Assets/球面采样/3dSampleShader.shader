Shader "Unlit/3dSampleShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _StartPos("StartPos", Vector) = (0,0,0,0)
//        _Range("Range",Vector) = (0,0,0,0)
        _Extent("Extent",Vector) = (1,1,1,1)
        _Resolution("Resolution",Float) = 1
        _ThreeDTex("3DTexture",3D) = "black"{}
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
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 normal:NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _StartPos;
            sampler3D _ThreeDTex;
            float4 _ThreeDTex_ST;
            
            float3 _ThreeDTex_size;
            float3 _ThreeDTex_texelSize;//X =>1/width Y=>1/height Z=>widht w =>height
            
            float _Resolution;
            // float4 _Range;
            float4 _Extent;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = mul(v.normal,(float3x3)unity_WorldToObject);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            float3 AmbientLight(const float3 worldNormal, float3 world2UV)
            {
                float3 nSquared = worldNormal*worldNormal;
                int3 isNegative = (worldNormal<0.0);
                float3 linearColor;
                // world2UV.x += 0.5f*_ThreeDTex_texelSize.x;
                // world2UV.z += 0.5f*_ThreeDTex_texelSize.z;
                float3 uvNorX = world2UV;
                
                uvNorX.x = uvNorX.x+isNegative.x*_ThreeDTex_texelSize.x;
                float3 uvNorY = world2UV;
                uvNorY.x = uvNorY.x+isNegative.x*_ThreeDTex_texelSize.x + _ThreeDTex_texelSize.x*2;
                float3 uvNorZ = world2UV;
                uvNorZ.x = uvNorZ.x+isNegative.x*_ThreeDTex_texelSize.x + _ThreeDTex_texelSize.x*4;
                
                float3 XX = tex3Dlod(_ThreeDTex, float4(uvNorX,0));
                float3 YY = tex3Dlod(_ThreeDTex, float4(uvNorY,0));
                float3 ZZ = tex3Dlod(_ThreeDTex, float4(uvNorZ,0));
                
                
                linearColor = nSquared.x * XX+
                                nSquared.y * YY+
                                    nSquared.z * ZZ;
                return linearColor;//1.5为经验修正
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                
                float4 StartPos = _StartPos;
                float3 N = normalize(i.normal);
                float3 world2UV = floor(i.worldPos - StartPos)*_Resolution/_Extent;//3D纹理坐标
                float3 ambientCube = AmbientLight(N,world2UV);
                return float4(ambientCube.rgb,1);
                
                fixed4 col = tex2D(_MainTex, (i.worldPos.xz-StartPos.xz)*_Resolution/_Extent.xz);
                world2UV.x/=6;
                col = tex3Dlod(_ThreeDTex, float4(world2UV.x,0,0,0));
                
                // UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
