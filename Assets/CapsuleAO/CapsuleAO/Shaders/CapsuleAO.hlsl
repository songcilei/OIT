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
//线段最近点投影  胶囊核心几何公式
    float3 samplePosition = worldPosition + normalize(worldNormal) * normalBias;//这里是为了防止自身遮挡 所以加normalBias进行修正
    float3 segment = end - start;
    float segmentLengthSq = max(dot(segment, segment), 1e-6);//线段长度的平方
    float t = saturate(dot(samplePosition - start, segment) / segmentLengthSq);//修复后距离和 球体距离的比值一致
    float3 closest = lerp(start, end, t);
    float3 toCapsule = closest - samplePosition;
    float distanceToAxis = max(length(toCapsule), radius + 1e-4);
    float3 directionToCapsule = toCapsule / distanceToAxis;
//Facing 朗伯投影因子（投影立体角核心）
    float facing = saturate(dot(normalize(worldNormal), directionToCapsule));
//solidAngle 球体立体角近似公式（核心）     远场小球立体角简化近似（IQ SphereAO 经典近似  
    //solidAngle=1-sqrt(1-(r/d)^2)
    float normalizedRadius = saturate(radius / distanceToAxis);
    float solidAngle = 1.0 - sqrt(saturate(1.0 - normalizedRadius * normalizedRadius));
//distanceFade 距离线性衰减
    float distanceFade = 1.0 - saturate((distanceToAxis - radius) / falloffDistance);

    float segmentLength = sqrt(segmentLengthSq);
    
//lengthBoost 胶囊长度补偿（工程修正）    
    float lengthBoost = lerp(1.0, 1.75, saturate(segmentLength / max(segmentLength + radius * 2.0, 1e-4)));
//总遮挡复合与幂次对比度    
    float occlusion = facing * solidAngle * distanceFade * intensity * lengthBoost;
    return saturate(pow(saturate(occlusion), power));
}

void CapsuleAO_ClosestRaySegment(
    float3 rayOrigin,//worldPosition
    float3 rayDirection,//lightDir
    float3 segmentStart,//start => 胶囊开始点(上端)
    float3 segmentEnd,//end => 胶囊结束点(下端)
    out float rayT,
    out float segmentT,
    out float distance)
{
    float3 segment = segmentEnd - segmentStart;//胶囊轴线方向向量 = 结束点 - 开始点 
    float segmentLengthSq = max(dot(segment, segment), 1e-6);//线段的平方
    float3 w = rayOrigin - segmentStart;//从 capsule 起点指向 世界坐标 的每一个ray 起点的向量  C-P
    float b = dot(rayDirection, segment);//dot 灯光方向 胶囊长度向量  即 射线 ray 和中心线段 segment 的最近距离
    float c = segmentLengthSq;//胶囊长度的平方
    float d = dot(rayDirection, w);// w在射线方向上的投影。即 离起点最近的点
    float e = dot(segment, w);//w在胶囊轴方向上的投影。  可以理解为ray 起点相对 capsule 起点，在 capsule 轴方向上走了多少
    float denominator = c - b * b;//这是求最近点方程时的分母。射线方向和胶囊轴方向是否接近平行  denominator 接近 0 =>平行
// distance = w;
  

    if (abs(denominator) > 1e-6)//非平行情形
    {
        rayT = (b * e - c * d) / denominator;
        segmentT = (e - b * d) / denominator;
    }
    else//平行情况
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
    //数据
    float3 start = data0.xyz;
    float3 end = data1.xyz;
    float radius = max(data0.w, 1e-4);
    float intensity = data3.x;
    float softness = max(data3.y, 1e-4);
    float maxDistance = max(data3.z, 1e-4);

    //射线
    float rayT;
    float segmentT;
    float distanceToCapsule;
    float3 lightDir = normalize(lightDirection);
    CapsuleAO_ClosestRaySegment(worldPosition, lightDir, start, end, rayT, segmentT, distanceToCapsule);
// return distanceToCapsule;
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
