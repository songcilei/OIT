Shader "Unlit/FourVexol"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VexolTex("VexolTex",2D) = "white"{}
        _BoundWidht ("BoundWidht",int) = 1024
        _TreeTexWidth("TreeTexWidth", int) = 1024
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
            sampler2D _VexolTex;
            float4 _VexolTex_ST;
            int _BoundWidht,_TreeTexWidth;
            
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
                return	tex2D(_VexolTex, half2(index % _TreeTexWidth, index / _TreeTexWidth)/ (half)max(_TreeTexWidth, 1));
                            
            }
            half ShadowValue(float3 wPos)
            {
                int x = wPos.x*10+0.5;
                // int y = i.worldPos.y+0.5;
                int z = wPos.z*10+0.5;
                
                int index = 0;
                int size = _BoundWidht;
    
                [unroll(10)]
                while (1)
                {
                
                    int4 node = GetTreeValue(index);
         
                    int flag = node.a;

                    if (node.z*255<0) 
                    {
                        return 1-flag;
                    }
                    if (size ==1)
                    {
                        return 1;
                    }
                    int childIndex = 0;
                    if (x > node.x*255 + size/2)
                    {
                        childIndex+=2;
                    }
                    if (z>node.y*255 +size/2)
                    {
                        childIndex ++;
                    }
                    
                    index = node.z*255 + childIndex;
                    size/=2;
                }
                return 1;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float4 col = tex2D(_MainTex, i.uv);
                
                
                float atten = ShadowValue(i.worldPos);
                
       
                
                
                return atten;
                // return float4(atten,atten,atten,1);
            }
            ENDHLSL
        }
    }
}
