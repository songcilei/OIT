Shader "Unlit/VoxelGIShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
//voxel GI        
        _VoxelTex("VoxelTex",3D) = "black"{}
        _lowAABB("lowAABB",Vector) = (0,0,0,0)
        _highAABB("upAABB",Vector) = (0,0,0,0)
        _TrackThreshold("TrackThreshold",Range(0,1)) = 0.01
        _TrackMaxCount("TrackThresholdCount",Int) = 3
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
                float3 normal:NORMAL;
                float4 color:COLOR;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
//Voxel GI-----------------------------------------
            sampler3D _VoxelTex;
            float4 _VoxelTex_ST;
            float4 _VoxelTex_TexelSize;
            
            float4 _lowAABB;
            float4 _highAABB;
            int _TrackMaxCount;
            float _TrackThreshold;
            
            float3 GetVoxelGI(float3 worldPos,float3 N)
            {
                //voxel
                float3 voxelUV = (worldPos - _lowAABB.xyz)/(_highAABB.xyz - _lowAABB.xyz);//从世界坐标映射到3D纹理坐标系
                float step = 1.0f/_VoxelTex_TexelSize.z;//获取到单次步长
                float3 detalColor = 0;
                float3 detailUV = 0;
                for (int i = 0; i < _TrackMaxCount; ++i)
                {
                    detailUV += normalize(N)*step * pow(2,i)*_TrackThreshold; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV = voxelUV + detailUV;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV,i)).rgb;//这里采用的是 密集体素 + mipmap 的追踪的方法
                }
                detalColor/=_TrackMaxCount;
                return detalColor;
            }
//Voxel  GI
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 N = mul(v.normal,(float3x3)unity_WorldToObject);
                o.normal = N;
                float3 worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.color.rgb = GetVoxelGI(worldPos,N);
                
                return o;
            }
            
            

            fixed4 frag (v2f i) : SV_Target
            {
                
                
                fixed4 col = tex2D(_MainTex, i.uv);
                
     
                return float4(i.color.rgb,1);
            }
            ENDCG
        }
    }
}
