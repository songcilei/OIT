Shader "Unlit/WeatherFogShader"
{
    Properties {
        _Fade("Fade",Range(0,1))=0
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0, 0.66, 0.73, 1)
        

        _MaskColor("EdgeMaskColor",Color) = (0.5,0.5,0.5,1)
        _Mask("EdgeMask",2D)= "white"{}

        
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

 
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            half4 _MaskColor;
            
    
            
            half _Fade;


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
                
                
                
                half4 mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv);
                
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                
                half4 temp = lerp(baseMap, _MaskColor, (1-mask.r)*_MaskColor.a);
                
                baseMap = lerp(baseMap,temp,_Fade);
                return baseMap * _BaseColor * i.color;
            }
            ENDHLSL
        }
    }
}
