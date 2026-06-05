
Shader "Custom/FootPrintShader" {
    Properties {
        _MaskAlpha("MaskAlpha",Range(0,1))=1
        _Mask (" Mask", 2D) = "white" {}
        _BaseColor ("Example Colour", Color) = (0, 0.66, 0.73, 1)
        _Normal("Normal",2D)= "bump"{}
    }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _Mask_ST;
        float4 _BaseColor;
        float _MaskAlpha;
        
        CBUFFER_END
        ENDHLSL

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct a2v {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normal :NORMAL;
                float4 tangent:TANGENT;
            };

            struct v2f {
                float4 positionCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normal :NORMAL;
                float3 tangent: TANGENT;
                float3 bnormal:TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);

            v2f vert(a2v v) {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _Mask);
                o.color = v.color;
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.tangent = TransformObjectToWorldDir(v.tangent);
                o.bnormal = cross(o.normal, TransformObjectToWorldDir(v.tangent.xyz)) * v.tangent.w;
                o.worldPos = mul(UNITY_MATRIX_M, v.positionOS);
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                half3x3 tbn = half3x3(i.tangent, i.bnormal, i.normal);
                half3 normal = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, i.uv));
                half3 N = TransformTangentToWorld(normal,tbn);
                half3 L = _MainLightPosition.xyz;
                half NdotL = saturate(dot(N, L));
                
                half4 mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv)*_MaskAlpha;
                return float4(NdotL.rrr*_BaseColor,mask.r*NdotL.r);
     
            }
            ENDHLSL
        }
    }
}