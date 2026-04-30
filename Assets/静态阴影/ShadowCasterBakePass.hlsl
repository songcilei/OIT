#ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
#define UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Shadow Casting Light geometric parameters. These variables are used when applying the shadow Normal Bias and are set by UnityEngine.Rendering.Universal.ShadowUtils.SetupShadowCasterConstantBuffer in com.unity.render-pipelines.universal/Runtime/ShadowUtils.cs
// For Directional lights, _LightDirection is used when applying shadow Normal Bias.
// For Spot lights and Point lights, _LightPosition is used to compute the actual light direction because it is different at each shadow caster geometry vertex.
float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GetShadowPositionHClip(Attributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    // #if defined (SHADER_TARGET_GLSL) 
    // depth = depth*0.5 + 0.5; //(-1, 1)-->(0, 1)
    // #elif defined (UNITY_REVERSED_Z)
    // depth = 1 - depth;       //(1, 0)-->(0, 1)
    // #endif
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    #if defined(_ALPHATEST_ON)
        output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif

    output.positionCS = GetShadowPositionHClip(input);
    return output;
}
float4 PackDepth(float depth)
{
    float4 bitShift = float4(1.0, 255.0, 65025.0, 16581375.0);
    float4 fracVal = frac(depth * bitShift);

    float4 res;
    res.rgb = fracVal.rgb - fracVal.gba * (1.0 / 255.0);
    res.a = fracVal.a;

    return res;
}

float UnpackDepth(float4 color)
{
    float4 bitShift = float4(1.0, 1/255.0, 1/65025.0, 1/16581375.0);
    return dot(color, bitShift);
}

float4 pack(float depth)
{
    float4 mid = frac(depth*float4(1.0, 255.0, pow(255.0, 2.0), pow(255.0, 3.0)));
    return mid - mid.yzww * float4(pow(255.0, -1.0), pow(255.0, -1.0), pow(255.0, -1.0), 0.0);
}

float unpack(float4 rgba)
{
    return dot(rgba, float4(1.0, pow(255.0, -1.0), pow(255.0, -2.0), pow(255.0, -3.0)));
}

float4 PackDepthToRGBA(float depth)
{
    const float r = 1.0 / 255.0;
    const float g = r * r;
    const float b = g * r;
    const float a = b * r;

    float4 res = frac(depth * float4(1.0, 255.0, 65025.0, 16581375.0));
    res.rgb -= res.gba * r;
    return res;
}

float4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(_ALPHATEST_ON)
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    #endif

    #if defined(LOD_FADE_CROSSFADE)
        LODFadeCrossFade(input.positionCS);
    #endif

    float4 depth = pack(input.positionCS.z/input.positionCS.w*0.5+0.5);
    // float dd = UnpackDepth(depth);
    return depth;
    //return float4(input.positionCS.zzz*0.5+0.5, 1.0f);
}

#endif
