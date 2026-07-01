#ifndef CAPSULE_AO_INCLUDED
#define CAPSULE_AO_INCLUDED

#ifndef CAPSULE_AO_MAX
#define CAPSULE_AO_MAX 64
#endif

int _CapsuleAOCount;
float4 _CapsuleAOData0[CAPSULE_AO_MAX]; // xyz = start, w = radius
float4 _CapsuleAOData1[CAPSULE_AO_MAX]; // xyz = end, w = ambient intensity
float4 _CapsuleAOData2[CAPSULE_AO_MAX]; // x = falloff, y = normal bias, z = power
float4 _CapsuleAOData3[CAPSULE_AO_MAX]; // x = directional intensity, y = directional softness, z = directional max distance

float CapsuleAO_AmbientOne(float3 worldPosition, float3 worldNormal, float4 data0, float4 data1, float4 data2)
{
    float3 start = data0.xyz;
    float3 end = data1.xyz;
    float radius = max(data0.w, 1e-4);
    float intensity = data1.w;
    float falloffDistance = max(data2.x, 1e-4);
    float normalBias = max(data2.y, 0.0);
    float power = max(data2.z, 0.05);

    float3 samplePosition = worldPosition + normalize(worldNormal) * normalBias;
    float3 segment = end - start;
    float segmentLengthSq = max(dot(segment, segment), 1e-6);
    float t = saturate(dot(samplePosition - start, segment) / segmentLengthSq);
    float3 closest = lerp(start, end, t);
    float3 toCapsule = closest - samplePosition;
    float distanceToAxis = max(length(toCapsule), radius + 1e-4);
    float3 directionToCapsule = toCapsule / distanceToAxis;

    float facing = saturate(dot(normalize(worldNormal), directionToCapsule));
    float normalizedRadius = saturate(radius / distanceToAxis);
    float solidAngle = 1.0 - sqrt(saturate(1.0 - normalizedRadius * normalizedRadius));
    float distanceFade = 1.0 - saturate((distanceToAxis - radius) / falloffDistance);

    float segmentLength = sqrt(segmentLengthSq);
    float lengthBoost = lerp(1.0, 1.75, saturate(segmentLength / max(segmentLength + radius * 2.0, 1e-4)));
    float occlusion = facing * solidAngle * distanceFade * intensity * lengthBoost;
    return saturate(pow(saturate(occlusion), power));
}

void CapsuleAO_ClosestRaySegment(
    float3 rayOrigin,
    float3 rayDirection,
    float3 segmentStart,
    float3 segmentEnd,
    out float rayT,
    out float segmentT,
    out float distance)
{
    float3 segment = segmentEnd - segmentStart;
    float segmentLengthSq = max(dot(segment, segment), 1e-6);
    float3 w = rayOrigin - segmentStart;
    float b = dot(rayDirection, segment);
    float c = segmentLengthSq;
    float d = dot(rayDirection, w);
    float e = dot(segment, w);
    float denominator = c - b * b;

    if (abs(denominator) > 1e-6)
    {
        rayT = (b * e - c * d) / denominator;
        segmentT = (e - b * d) / denominator;
    }
    else
    {
        rayT = 0.0;
        segmentT = saturate(-e / segmentLengthSq);
    }

    segmentT = saturate(segmentT);
    rayT = max(0.0, dot(segmentStart + segment * segmentT - rayOrigin, rayDirection));
    segmentT = saturate(dot(rayOrigin + rayDirection * rayT - segmentStart, segment) / segmentLengthSq);
    rayT = max(0.0, dot(segmentStart + segment * segmentT - rayOrigin, rayDirection));

    float3 rayPoint = rayOrigin + rayDirection * rayT;
    float3 segmentPoint = segmentStart + segment * segmentT;
    distance = length(rayPoint - segmentPoint);
}

float CapsuleAO_DirectionalOne(float3 worldPosition, float3 lightDirection, float4 data0, float4 data1, float4 data3)
{
    float3 start = data0.xyz;
    float3 end = data1.xyz;
    float radius = max(data0.w, 1e-4);
    float intensity = data3.x;
    float softness = max(data3.y, 1e-4);
    float maxDistance = max(data3.z, 1e-4);

    float rayT;
    float segmentT;
    float distanceToCapsule;
    float3 lightDir = normalize(lightDirection);
    CapsuleAO_ClosestRaySegment(worldPosition, lightDir, start, end, rayT, segmentT, distanceToCapsule);

    float projectionStart = dot(start - worldPosition, lightDir);
    float projectionEnd = dot(end - worldPosition, lightDir);
    float inFrontOfReceiver = step(0.0, max(projectionStart, projectionEnd) + radius);
    float contact = 1.0 - smoothstep(radius, radius + softness, distanceToCapsule);
    float distanceFade = 1.0 - saturate(rayT / maxDistance);
    return saturate(contact * distanceFade * intensity * inFrontOfReceiver);
}

float CapsuleAO_ComputeAmbient(float3 worldPosition, float3 worldNormal)
{
    float occlusion = 0.0;

    [loop]
    for (int i = 0; i < _CapsuleAOCount && i < CAPSULE_AO_MAX; i++)
    {
        float capsuleOcclusion = CapsuleAO_AmbientOne(worldPosition, worldNormal, _CapsuleAOData0[i], _CapsuleAOData1[i], _CapsuleAOData2[i]);
        occlusion += capsuleOcclusion * (1.0 - occlusion);
    }

    return saturate(occlusion);
}

float CapsuleAO_ComputeDirectional(float3 worldPosition, float3 lightDirection)
{
    float occlusion = 0.0;

    [loop]
    for (int i = 0; i < _CapsuleAOCount && i < CAPSULE_AO_MAX; i++)
    {
        float capsuleOcclusion = CapsuleAO_DirectionalOne(worldPosition, lightDirection, _CapsuleAOData0[i], _CapsuleAOData1[i], _CapsuleAOData3[i]);
        occlusion += capsuleOcclusion * (1.0 - occlusion);
    }

    return saturate(occlusion);
}

float CapsuleAO_Compute(float3 worldPosition, float3 worldNormal)
{
    return CapsuleAO_ComputeAmbient(worldPosition, worldNormal);
}

float3 CapsuleAO_ApplyAmbient(float3 color, float3 worldPosition, float3 worldNormal, float strength)
{
    float occlusion = CapsuleAO_ComputeAmbient(worldPosition, worldNormal) * strength;
    return color * (1.0 - saturate(occlusion));
}

float3 CapsuleAO_Apply(float3 color, float3 worldPosition, float3 worldNormal, float strength)
{
    return CapsuleAO_ApplyAmbient(color, worldPosition, worldNormal, strength);
}

#endif
