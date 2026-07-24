Shader "Unlit/SDFClipMapShadow"
{
    Properties
    {
        _SDFWorldPosCenter("SDFWorldPosCenter",Vector) = (0,0,0,0)
//        _ClipMap("SDFTex",3D) = "white"{}
//        _StepSize("StepSize",Float)=0.03
        _MinDist("MinDist",Float)=0.01
        _rayStep("rayStep",Float)=1
        _MaxStep("MaxStep",Int) = 32
        _Color("Color",Color) = (1,1,1,1)
        _KK("KK",Float)=0.5
//        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline"}


        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler3D _ClipMap;
            float4 _ClipMap_ST;
            float4 _ClipMap_TexelSize;
            float4 _ClipMapMax,_ClipMapMin;
            float4 _Color;
            float4 _SDFWorldPosCenter;
            float _StepSize;
            int _MaxStep;
            float _MinDist;
            float _rayStep;
            float _KK;
            
            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld,v.vertex);
                o.worldPos = worldPos;
                o.vertex = mul(UNITY_MATRIX_VP,float4(worldPos,1));
                o.uv = v.uv;
                return o;
            }
            
            
            float GetSDFWorldPos(float3 uvw)
            {
                float3 sdfUV = uvw;
                return tex3Dlod(_ClipMap,float4(sdfUV,0)).r;
            }
            bool Inside01(float3 uvw)
            {
                return all(uvw >= 0) && all(uvw <= 1);
            }

            float3 SDFSoft(float3 p,float3 L)
            {
                float depth = 0.01f;
                float3 low = _ClipMapMin;
                float3 up = _ClipMapMax;
                 float res = 1.0;
                [loop]
                for (int i =0;i<_MaxStep;i++)
                {
                    if (i>= _MaxStep)
                    {
                        break;
                    }
                    
                    float3 uvw = p + L*depth;// 根据sdf 距离查找 进行下次步进的距离映射
                    uvw  = (uvw-low)/(up-low);// 映射到0-1
                    // 射线已经离开 SDF 体积。
                    // if (!Inside01(uvw))
                    // {
                    //     break;
                    // }
                    
                    float dist = GetSDFWorldPos(uvw)/_rayStep;
                    if (dist < _MinDist)
                    {
                        return 0;
                    }
                    res = min(res,_KK*dist/depth);
                    depth += dist;
                    if (depth>5)
                    {
                        break;
                    }
                }
                return saturate(res);
            }


            float4 frag (v2f i) : SV_Target
            {
                // float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 L = normalize(_MainLightPosition.xyz);
                float3 p = i.worldPos - _SDFWorldPosCenter;
                float3 ss = SDFSoft(p, L);

                return float4(ss,1);
            }
            ENDHLSL
        }
    } 
}
