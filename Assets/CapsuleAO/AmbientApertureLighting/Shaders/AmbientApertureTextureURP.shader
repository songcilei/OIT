Shader "Custom/URP/Ambient Aperture Texture"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ApertureMap ("Aperture Map", 2D) = "white" {}
        _LightAngularRadius ("Light Angular Radius", Range(0.001, 3.14159)) = 0.12
        _AmbientColor ("Ambient Tint", Color) = (1,1,1,1)
        _DirectStrength ("Direct Strength", Range(0, 8)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 8)) = 1
        _UseExactIntersection ("Use Exact Intersection", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "AmbientApertureLighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ApertureMap);
            SAMPLER(sampler_ApertureMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _LightAngularRadius;
                float _DirectStrength;
                float _AmbientStrength;
                float _UseExactIntersection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 baseUv : TEXCOORD0;
                float2 apertureUv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.apertureUv = input.uv;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float4 apertureSample = SAMPLE_TEXTURE2D(_ApertureMap, sampler_ApertureMap, input.apertureUv);
                float3 apertureOS = AAL_DecodeDirection(apertureSample.rgb);
                float3 apertureWS = normalize(TransformObjectToWorldNormal(apertureOS));
                float apertureRadius = AAL_DecodeRadius(apertureSample.a);

                Light mainLight = GetMainLight();
                float3 lightDirection = normalize(mainLight.direction);

                float directVisibility;
                float ambientVisibility;
                float lambert;
                AAL_Evaluate(
                    normalWS,
                    apertureWS,
                    apertureRadius,
                    lightDirection,
                    _LightAngularRadius,
                    _UseExactIntersection,
                    directVisibility,
                    ambientVisibility,
                    lambert);

                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUv).rgb * _BaseColor.rgb;
                float3 directLighting = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float3 ambientLighting = SampleSH(normalWS) * _AmbientColor.rgb;

                float3 direct = directLighting * directVisibility * lambert * _DirectStrength;
                float3 ambient = ambientLighting * ambientVisibility * _AmbientStrength;
                return half4(albedo * (direct + ambient), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
