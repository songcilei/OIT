Shader "Custom/URP/Capsule AO Preview"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _AmbientColor ("Ambient Tint", Color) = (0.35,0.38,0.42,1)
        _AmbientAOStrength ("Ambient AO Strength", Range(0, 4)) = 1
        _DirectionalAOStrength ("Directional AO Strength", Range(0, 4)) = 1
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
            #include "CapsuleAO.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _AmbientColor;
                float _AmbientAOStrength;
                float _DirectionalAOStrength;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                Light mainLight = GetMainLight();
                float3 lightDirection = normalize(mainLight.direction);
                float lambert = saturate(dot(normalWS, lightDirection));

                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                float ambientOcclusion = saturate(CapsuleAO_ComputeAmbient(input.positionWS, normalWS) * _AmbientAOStrength);
                float directionalOcclusion = saturate(CapsuleAO_ComputeDirectional(input.positionWS, lightDirection) * _DirectionalAOStrength);
                float3 ambientLighting = SampleSH(normalWS) * _AmbientColor.rgb;
                float3 directLighting = mainLight.color * lambert * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                float3 ambient = ambientLighting * (1.0 - ambientOcclusion);
return float4(directionalOcclusion.rrr,1);
                float3 direct = directLighting * (1.0 - directionalOcclusion);
                float3 color = albedo * (ambient + direct);
                
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
