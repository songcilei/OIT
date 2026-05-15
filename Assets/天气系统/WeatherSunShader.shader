Shader "Unlit/WeatherSunShader"
{
    Properties {
        _Fade("Fade",Range(0,1))=0
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0, 0.66, 0.73, 1)
        
        _SunSpeedX("SunSpeedX",Float)=1
        _SunSpeedY("SunSpeedY",Float)=1
        _SunSpeed2X("SunSpeed2X",Float)=1
        _SunSpeed2Y("SunSpeed2Y",Float)=1
        _SunTex("SunTex", 2D)="black"{}
        _SunDistort("SunDistort", Range(0,1))=1
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

            TEXTURE2D(_SunTex);
            half4 _SunTex_ST;
            SAMPLER(sampler_SunTex);
            
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            half4 _MaskColor;
            
            float _SunDistort;
            float _SunSpeedX,_SunSpeed2X;
            float _SunSpeedY,_SunSpeed2Y;
            
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
                
                
                half2 newUV = i.uv * _SunTex_ST.xy+_SunTex_ST.zw + float2(_SunSpeedX*_Time.y,_SunSpeedY*_Time.y);
                half2 newUV2 = i.uv * _SunTex_ST.xy+_SunTex_ST.zw + float2(_SunSpeed2X*_Time.y,_SunSpeed2Y*_Time.y);
                half2 sunTex1 = SAMPLE_TEXTURE2D(_SunTex, sampler_SunTex, newUV);
                half2 sunTex2 = SAMPLE_TEXTURE2D(_SunTex, sampler_SunTex, newUV2*0.6f);
                half2 sunMask = SAMPLE_TEXTURE2D(_Mask, sampler_SunTex, i.uv);
                
                half2 uv = lerp(i.uv,min(sunTex1, sunTex2),_SunDistort*sunMask.r);
                uv = lerp(i.uv,uv,_Fade);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 temp =  lerp(baseMap.rgb,_MaskColor,(1-sunMask.g)*_MaskColor.a);
                baseMap.rgb = lerp(baseMap,temp,_Fade);
                return baseMap * _BaseColor * i.color;
            }
            ENDHLSL
        }
    }
}
