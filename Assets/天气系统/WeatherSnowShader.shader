Shader "Unlit/WeatherSnowShader"
{
    Properties {
        _BaseMap ("Example Texture", 2D) = "white" {}
        _BaseColor ("Example Colour", Color) = (0, 0.66, 0.73, 1)
        _SnowTex("SnowTex",2D)="black"{}
        
        _DissolveSlider("DissolveSlider",Range(0,1))=0
        _Dissolve("Dissolve",2D)="white"{}

        
    }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        CBUFFER_END
        ENDHLSL

        Pass {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct a2v {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct v2f {
                float4 positionCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_Dissolve);
            SAMPLER(sampler_Dissolve);
            float _DissolveSlider;
            
            
            TEXTURE2D(_SnowTex);
            SAMPLER(sampler_SnowTex);


            v2f vert(a2v v) {
                v2f o;

                //VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                //o.positionCS = positionInputs.positionCS;
                // Or this :
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target {

                

                float dissolve = SAMPLE_TEXTURE2D(_Dissolve, sampler_Dissolve, i.uv).r;
                

                
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 snowMap = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, i.uv);
                
                float snowAlpha = saturate((1-dissolve) + (_DissolveSlider*2-1))*snowMap.a;
                baseMap = lerp(baseMap,snowMap,snowAlpha);
                return baseMap * _BaseColor * i.color;
            }
            ENDHLSL
        }
    }
}
