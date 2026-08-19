Shader "SHCalculation/Validation Sphere"
{
    Properties
    {
        _SH0 ("SH0", Vector) = (0, 0, 0, 0)
        _SH1 ("SH1", Vector) = (0, 0, 0, 0)
        _SH2 ("SH2", Vector) = (0, 0, 0, 0)
        _SH3 ("SH3", Vector) = (0, 0, 0, 0)
        _SH4 ("SH4", Vector) = (0, 0, 0, 0)
        _SH5 ("SH5", Vector) = (0, 0, 0, 0)
        _SH6 ("SH6", Vector) = (0, 0, 0, 0)
        _SH7 ("SH7", Vector) = (0, 0, 0, 0)
        _SH8 ("SH8", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SH Validation"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SH0;
                float4 _SH1;
                float4 _SH2;
                float4 _SH3;
                float4 _SH4;
                float4 _SH5;
                float4 _SH6;
                float4 _SH7;
                float4 _SH8;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float3 EvaluateIrradianceSH(float3 normalWS)
            {
                float x = normalWS.x;
                float y = normalWS.y;
                float z = normalWS.z;

                float3 irradiance = _SH0.rgb * 0.2820947918;
                irradiance += _SH1.rgb * (0.4886025119 * y);
                irradiance += _SH2.rgb * (0.4886025119 * z);
                irradiance += _SH3.rgb * (0.4886025119 * x); 
                irradiance += _SH4.rgb * (1.0925484306 * x * y);
                irradiance += _SH5.rgb * (1.0925484306 * y * z);
                irradiance += _SH6.rgb * (0.3153915653 * (3.0 * z * z - 1.0));
                irradiance += _SH7.rgb * (1.0925484306 * x * z);
                irradiance += _SH8.rgb * (0.5462742153 * (x * x - y * y));
                return pow(irradiance,2.2f);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                return float4(input.normalWS,1);
                return half4(max(EvaluateIrradianceSH(normalWS), 0.0), 1.0);
            }
            ENDHLSL
        }
    }
}
