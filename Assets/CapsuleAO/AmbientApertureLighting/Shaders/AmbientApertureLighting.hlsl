#ifndef AMBIENT_APERTURE_LIGHTING_INCLUDED
#define AMBIENT_APERTURE_LIGHTING_INCLUDED

#define AAL_PI 3.14159265359
#define AAL_TWO_PI 6.28318530718

float AAL_AcosSafe(float value)
{
    return acos(clamp(value, -1.0, 1.0));
}

float AAL_SphericalCapArea(float radius)
{
    radius = clamp(radius, 0.0, AAL_PI);
    return AAL_TWO_PI * (1.0 - cos(radius));
}

float AAL_SphericalCapIntersectionAreaApprox(float radius0, float radius1, float distance)
{
    radius0 = clamp(radius0, 0.0, AAL_PI);
    radius1 = clamp(radius1, 0.0, AAL_PI);
    distance = clamp(distance, 0.0, AAL_PI);

    float minRadius = min(radius0, radius1);
    float maxRadius = max(radius0, radius1);

    if (distance <= maxRadius - minRadius)
    {
        return AAL_SphericalCapArea(minRadius);
    }

    if (distance >= radius0 + radius1)
    {
        return 0.0;
    }

    float diff = abs(radius0 - radius1);
    float t = 1.0 - saturate((distance - diff) / max(radius0 + radius1 - diff, 1e-5));
    return smoothstep(0.0, 1.0, t) * AAL_SphericalCapArea(minRadius);
}

float AAL_SphericalCapIntersectionAreaExact(float radius0, float radius1, float distance)
{
    radius0 = clamp(radius0, 0.0, AAL_PI);
    radius1 = clamp(radius1, 0.0, AAL_PI);
    distance = clamp(distance, 0.0, AAL_PI);

    float minRadius = min(radius0, radius1);
    float maxRadius = max(radius0, radius1);

    if (distance <= maxRadius - minRadius)
    {
        return AAL_SphericalCapArea(minRadius);
    }

    if (distance >= radius0 + radius1)
    {
        return 0.0;
    }

    float sinR0 = sin(radius0);
    float sinR1 = sin(radius1);
    float sinD = sin(distance);

    if (abs(sinR0) < 1e-5 || abs(sinR1) < 1e-5 || abs(sinD) < 1e-5)
    {
        return AAL_SphericalCapIntersectionAreaApprox(radius0, radius1, distance);
    }

    float cosR0 = cos(radius0);
    float cosR1 = cos(radius1);
    float cosD = cos(distance);

    float angle0 = AAL_AcosSafe((cosR1 - cosR0 * cosD) / (sinR0 * sinD));
    float angle1 = AAL_AcosSafe((cosR0 - cosR1 * cosD) / (sinR1 * sinD));
    float angleI = AAL_AcosSafe((cosD - cosR0 * cosR1) / (sinR0 * sinR1));
    float excess = max(0.0, angle0 + angle1 + angleI - AAL_PI);

    return max(0.0, 2.0 * angle0 * (1.0 - cosR0) + 2.0 * angle1 * (1.0 - cosR1) - 2.0 * excess);
}

void AAL_Evaluate(
    float3 surfaceNormal,
    float3 apertureDirection,
    float apertureRadius,
    float3 lightDirection,
    float lightRadius,
    float useExact,
    out float directVisibility,
    out float ambientVisibility,
    out float lambert)
{
    surfaceNormal = normalize(surfaceNormal);
    apertureDirection = normalize(apertureDirection);
    lightDirection = normalize(lightDirection);

    float distance = AAL_AcosSafe(dot(apertureDirection, lightDirection));
    float litArea = useExact > 0.5
        ? AAL_SphericalCapIntersectionAreaExact(apertureRadius, lightRadius, distance)
        : AAL_SphericalCapIntersectionAreaApprox(apertureRadius, lightRadius, distance);
    float apertureArea = AAL_SphericalCapArea(apertureRadius);
    float lightArea = AAL_SphericalCapArea(lightRadius);

    directVisibility = litArea / max(lightArea, 1e-5);
    ambientVisibility = max(apertureArea - litArea, 0.0) / AAL_TWO_PI;
    lambert = saturate(dot(surfaceNormal, normalize(apertureDirection + lightDirection)));
}

float3 AAL_DecodeDirection(float3 encodedDirection)
{
    return normalize(encodedDirection * 2.0 - 1.0);
}

float AAL_DecodeRadius(float encodedRadius)
{
    return saturate(encodedRadius) * AAL_PI;
}

#endif
