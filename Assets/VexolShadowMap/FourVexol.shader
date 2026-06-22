Shader "Unlit/FourVexol"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VexolTex("VexolTex",2D) = "white"{}
        _Depth("Depth",float) = 1024
        _TreeTexWidth("TreeTexWidth", float) = 1024
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma target 5.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"  

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            TEXTURE2D( _VexolTex);
            SAMPLER(sampler_VexolTex);
            // SamplerState sm_point_clamp_VexolTex;
            
            float4 _VexolTex_ST;
            float _Depth;
            float _TreeTexWidth;
            
            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = vertexInput.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 GetTreeValue(int index)
            {
                int texWidth = (int)_TreeTexWidth;
                int col = index % texWidth;
                int row = index / texWidth;
                float2 uv = (float2(col, row) + 0.5f) / _TreeTexWidth;//这里需要计算像素中心点 因为需要中心点采样
                return SAMPLE_TEXTURE2D(_VexolTex, sampler_VexolTex, uv);
                // return _VexolTex.Sample(sm_point_clamp_VexolTex, float4(u,v,0,0)/_TreeTexWidth);
            }
            float ShadowValue(float3 wPos)
            {
                float x = (wPos.x)*10;
                float z = (wPos.z)*10;
                
                int index = 0;//这里之所以从0开始 是因为树节点的索引是从0开始的
                float size = _Depth;
                
                // [loop] // 告诉编译器：不要展开，原生循环执行
                // for (int i=0; i<10; i++)
                // {
                //     float4 node = GetTreeValue(index);
                //
                //     int flag = node.a;
                //
                //     if (node.z<=0) //说明没有子节点
                //     {
                //         return 1-flag;
                //     }
                //
                //     if (flag == 1) //说明已经是阴影节点
                //     {
                //         return 0;
                //     }
                //     if (size==0)//说明已经循环到了最大深度
                //     {
                //         return 1;
                //     }
                //     int childIndex = 0;
                //     if (x > node.x)
                //     {
                //         childIndex+=2;
                //     }
                //     if (z>node.y)
                //     {
                //         childIndex ++;
                //     }
                //     
                //     index = (int)round(node.z) + childIndex;
                //     size-=1;
                // }
                // return 1;
                
                [unroll(20)]
                while (1)
                {
                
                    float4 node = GetTreeValue(index);

                    int flag = node.a;

                    if (node.z<=0) //说明没有子节点
                    {
                        return 1-flag;
                    }

                    if (flag == 1) //说明已经是阴影节点
                    {
                        return 0;
                    }
                    if (size==0)//说明已经循环到了最大深度
                    {
                        return 1;
                    }
                    int childIndex = 0;
                    if (x > node.x)
                    {
                        childIndex+=2;
                    }
                    if (z>node.y)
                    {
                        childIndex ++;
                    }
                    
                    index = (int)round(node.z) + childIndex;
                    size-=1;
                }
                return 1;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float4 col = tex2D(_MainTex,1- i.uv);
                
                // return float4(frac(i.worldPos),1);
                float atten = ShadowValue(i.worldPos);
                
                // float4 aa = _VexolTex.Sample(sm_point_clamp_VexolTex, half4(1-i.uv,0,0));
                
                // return aa.aaaa;
                // return aa.bbbb==5;
                
                
                return atten;
                // return float4(atten,atten,atten,1);
            }
            ENDHLSL
        }
    }
}
