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
                float3 worldPos:TEXCOORD1;
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
            
            ///求法向量 构建垂直正交基 后  求法向量和各个象限均分向量  
            void GetNormalFourSlopeDir(float3 normal, out float3 lu, out float3 ru, out float3 ld, out float3 rd)
            {
                float3 temp = abs(normal.y) < 0.99f ? float3(0,1,0) : float3(0,0,1);
                float3 tan = normalize(cross(temp, normal));
                float3 bi = normalize(cross(normal, tan));

                
                
                lu = normalize(-tan + bi+ normal);
                ru = normalize(tan + bi+ normal);
                ld = normalize(-tan - bi+ normal);
                rd = normalize(tan - bi+ normal);
            }
            
            float3 GetVoxelGI(float3 worldPos,float3 N)
            {
                //voxel
                float3 voxelUV = (worldPos - _lowAABB.xyz)/(_highAABB.xyz - _lowAABB.xyz);//从世界坐标映射到3D纹理坐标系
                float step = 1.0f/_VoxelTex_TexelSize.z;//获取到单次步长
                float3 detalColor = 0;
                float3 detailUV_lu,detailUV_ru,detailUV_ld,detailUV_rd = 0;
                float3 lu,ru,ld,rd;
                GetNormalFourSlopeDir(N,lu,ru,ld,rd);
                for (int i = 0; i < _TrackMaxCount; ++i)
                {
                    detailUV_lu += normalize(lu)*step * pow(2,i)*_TrackThreshold; // 这里是在法线方向上移动步长，进行追踪

                    float3 RayUV_lu = voxelUV + detailUV_lu;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_lu,i)).rgb;//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    detailUV_ru += normalize(ru)*step * pow(2,i)*_TrackThreshold; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV_ru = voxelUV + detailUV_ru;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_ru,i)).rgb;//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    detailUV_ld += normalize(ld)*step * pow(2,i)*_TrackThreshold; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV_ld = voxelUV + detailUV_ld;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_ld,i)).rgb;//这里采用的是 密集体素 + mipmap 的追踪的方法
                    
                    detailUV_rd += normalize(rd)*step * pow(2,i)*_TrackThreshold; // 这里是在法线方向上移动步长，进行追踪
                    float3 RayUV_rd = voxelUV + detailUV_rd;
                    detalColor += tex3Dlod(_VoxelTex,float4(RayUV_rd,i)).rgb;//这里采用的是 密集体素 + mipmap 的追踪的方法
                }
                // detalColor/=4;
                return detalColor;
            }
//Voxel  GI
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 N = mul(v.normal,(float3x3)unity_WorldToObject);
                o.normal = normalize(N);
                float3 worldPos = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.worldPos = worldPos;
                o.color.rgb = GetVoxelGI(worldPos,normalize(o.normal));
                return o;
            }
            
            

            fixed4 frag (v2f i) : SV_Target
            {
                // float3 gi = GetVoxelGI(i.worldPos,normalize(i.normal));
                
                fixed4 col = tex2D(_MainTex, i.uv);
                
     
                return float4(i.color.rgb,1);
            }
            ENDCG
        }
    }
}
