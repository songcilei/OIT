// Example Shader for Universal RP
// Written by @Cyanilux
// https://cyangamedev.wordpress.com/urp-shader-code/
Shader "Custom/TestSSS" {
    Properties {
        _BaseMap ("Example Texture", 2D) = "white" {}
        _BaseColor ("Example Colour", Color) = (0, 0.66, 0.73, 1)
        _JadeOffset("JadeOffset",Float)=0
        _JadeLightDistance("JadeLightDistance",Float)=0
        _JadeDepthDistance("JadeDepthDistance",Float)=0
        _JadePow("JadePow",Float)=1
        
        _JadeMinDist("_JadeMinDist",Float)=0
        _JadeMaxDist("_JadeMaxDist",Float)=1
        _JadeColor("_JadeColor",Color)=(1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        CBUFFER_END
        ENDHLSL

        Pass {
            Name "Example"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct a2v {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normal:NORMAL;
            };

            struct v2f {
                float4 positionCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normal :NORMAL;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            float _JadeLightDistance,_JadeDepthDistance;
            float _JadeOffset;
            half _JadePow;
            half _JadeMinDist,_JadeMaxDist;
            half4 _JadeColor;
                        
            float3 SSSDiffuseColor(float3 posWS)
            {
             float3 lightPointPos = (_JadeOffset)- TransformWorldToObjectDir(_MainLightPosition) * _JadeLightDistance;
             float TravelDist = saturate(pow(distance(lightPointPos , TransformWorldToObject(posWS))/(_JadeDepthDistance + _JadeLightDistance),_JadePow));
             float PenetrationWeight = (TravelDist - _JadeMinDist) / (_JadeMaxDist - _JadeMinDist);
             float3 DiffuseContrib = PenetrationWeight;;
             return saturate(DiffuseContrib * _JadeColor);
            }

            v2f vert(a2v v) {
                v2f o;

                //VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                //o.positionCS = positionInputs.positionCS;
                // Or this :
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.normal = mul(v.normal,(float3x3)unity_WorldToObject);
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                
                
                float3 L = normalize(_MainLightPosition);
                float3 N = normalize(i.normal);
                float NdotL = saturate(dot(N,L));
                
                
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);

                float4 col = baseMap * _BaseColor * i.color*NdotL;
                col.rgb = col.rgb + SSSDiffuseColor(col);
                return col; 
            }
            ENDHLSL
        }
    }
}
