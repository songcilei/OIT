Shader "Learning/Simple BRG Unlit"
{
    Properties
    {
        _BaseColor("基础颜色", Color) = (1, 1, 1, 1)
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // BRG 通过 DOTS Instancing 宏从 GraphicsBuffer 读取每个实例的数据。
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            // 声明 _BaseColor 是一项 DOTS/BRG 实例属性。
            // unity_ObjectToWorld 和 unity_WorldToObject 已由 Unity 的 Shader 库声明，无需重复写。
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define BRG_BASE_COLOR UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #else
                #define BRG_BASE_COLOR _BaseColor
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
            };

            Varyings Vert(Attributes input)
            {
                // 必须先设置 Instance ID，后面的矩阵和颜色读取才知道当前是第几个盒子。
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = BRG_BASE_COLOR;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
