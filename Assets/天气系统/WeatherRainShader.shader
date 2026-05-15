Shader "Unlit/WeatherRainShader"
{
    Properties {
        _Fade("Fade",Range(0,1)) = 0
        _BaseMap ("Example Texture", 2D) = "white" {}
        _BaseColor ("Example Colour", Color) = (0, 0.66, 0.73, 1)
        _RainSpeed("RainSpeed", float)=1
        _RainRate("RainRate",Range(0.1,1)) = 0.5
        _RainColor("RainColor", Color) = (0, 0.66, 0.73, 1)
        _RainTex("RainTex", 2D)= "white" {}
        _EdgeTex("EdgeTex", 2D) = "black"{}
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

            TEXTURE2D(_RainTex);
            half4 _RainTex_ST;
            SAMPLER(sampler_RainTex);
            
            TEXTURE2D(_EdgeTex);
            SAMPLER(sampler_EdgeTex);
            
            half _RainRate;
            half4 _RainColor;
            half _RainSpeed;
            half _Fade;

            
			TEXTURE2D(_CameraOpaqueTexture);
			SAMPLER(sampler_CameraOpaqueTexture);
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
                
                half4 rain = SAMPLE_TEXTURE2D(_RainTex, sampler_RainTex, i.uv*_RainTex_ST.xy+_RainTex_ST.zw);
                

                half2 dir = (float2(rain.yz)*2-1);
                float drapFrac = frac(rain.a +_Time.y*_RainSpeed)/_RainRate;
                float timeFrac =drapFrac -1 +rain.r;
                float dropFactor = 1-saturate( drapFrac);
                float factor = (dropFactor * saturate(sin(timeFrac)));
                float2 rainUV = factor*dir*_Fade;
                

                half4 edgeTex = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, i.uv);
                
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv+rainUV);
                
                half3 tempBaseMap = lerp(baseMap,edgeTex.rgb, edgeTex.a);
                tempBaseMap = lerp(tempBaseMap,_RainColor,factor*2);

                baseMap.rgb = lerp(baseMap.rgb,tempBaseMap,_Fade);
                return baseMap * _BaseColor * i.color;
            }
            ENDHLSL
        }
    }
}
