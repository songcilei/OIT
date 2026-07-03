using UnityEngine;

namespace AmbientApertureLighting
{
    public static class AmbientApertureMath
    {
        public const float Pi = Mathf.PI;
        public const float TwoPi = Mathf.PI * 2f;
        private const float Epsilon = 1e-5f;

        public static float SphericalCapArea(float radiusRadians)
        {
            radiusRadians = Mathf.Clamp(radiusRadians, 0f, Pi);
            return TwoPi * (1f - Mathf.Cos(radiusRadians));
        }

        public static float RadiusFromVisibleFraction(float visibleFraction)
        {
            visibleFraction = Mathf.Clamp01(visibleFraction);
            return Mathf.Acos(Mathf.Clamp(1f - visibleFraction, -1f, 1f));
        }

        public static float ApproximateIntersectionArea(float radius0, float radius1, float distance)
        {
            radius0 = Mathf.Clamp(radius0, 0f, Pi);
            radius1 = Mathf.Clamp(radius1, 0f, Pi);
            distance = Mathf.Clamp(distance, 0f, Pi);

            float minRadius = Mathf.Min(radius0, radius1);
            float maxRadius = Mathf.Max(radius0, radius1);

            if (distance <= maxRadius - minRadius)
            {
                return SphericalCapArea(minRadius);
            }

            if (distance >= radius0 + radius1)
            {
                return 0f;
            }

            float diff = Mathf.Abs(radius0 - radius1);
            float denominator = Mathf.Max(radius0 + radius1 - diff, Epsilon);
            float t = 1f - Mathf.Clamp01((distance - diff) / denominator);
            float smooth = t * t * (3f - 2f * t);
            return smooth * SphericalCapArea(minRadius);
        }

        public static float ExactIntersectionArea(float radius0, float radius1, float distance)
        {
            radius0 = Mathf.Clamp(radius0, 0f, Pi);
            radius1 = Mathf.Clamp(radius1, 0f, Pi);
            distance = Mathf.Clamp(distance, 0f, Pi);

            float minRadius = Mathf.Min(radius0, radius1);
            float maxRadius = Mathf.Max(radius0, radius1);

            if (distance <= maxRadius - minRadius)
            {
                return SphericalCapArea(minRadius);
            }

            if (distance >= radius0 + radius1)
            {
                return 0f;
            }

            float sinR0 = Mathf.Sin(radius0);
            float sinR1 = Mathf.Sin(radius1);
            float sinD = Mathf.Sin(distance);

            if (Mathf.Abs(sinR0) < Epsilon || Mathf.Abs(sinR1) < Epsilon || Mathf.Abs(sinD) < Epsilon)
            {
                return ApproximateIntersectionArea(radius0, radius1, distance);
            }

            float cosR0 = Mathf.Cos(radius0);
            float cosR1 = Mathf.Cos(radius1);
            float cosD = Mathf.Cos(distance);

            float angle0 = AcosSafe((cosR1 - cosR0 * cosD) / (sinR0 * sinD));
            float angle1 = AcosSafe((cosR0 - cosR1 * cosD) / (sinR1 * sinD));
            float angleI = AcosSafe((cosD - cosR0 * cosR1) / (sinR0 * sinR1));
            float sphericalTriangleExcess = Mathf.Max(0f, angle0 + angle1 + angleI - Pi);

            float area = 2f * angle0 * (1f - cosR0)
                + 2f * angle1 * (1f - cosR1)
                - 2f * sphericalTriangleExcess;

            return Mathf.Max(0f, area);
        }

        public static AmbientApertureResult Evaluate(
            Vector3 apertureDirection,
            float apertureRadius,
            Vector3 lightDirection,
            float lightRadius,
            Vector3 surfaceNormal,
            bool useExactIntersection)
        {
            apertureDirection = apertureDirection.sqrMagnitude > Epsilon ? apertureDirection.normalized : surfaceNormal.normalized;
            lightDirection = lightDirection.sqrMagnitude > Epsilon ? lightDirection.normalized : surfaceNormal.normalized;
            surfaceNormal = surfaceNormal.sqrMagnitude > Epsilon ? surfaceNormal.normalized : Vector3.up;

            float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(apertureDirection, lightDirection), -1f, 1f));
            float litArea = useExactIntersection
                ? ExactIntersectionArea(apertureRadius, lightRadius, distance)
                : ApproximateIntersectionArea(apertureRadius, lightRadius, distance);

            float apertureArea = SphericalCapArea(apertureRadius);
            float lightArea = SphericalCapArea(lightRadius);
            Vector3 centroid = apertureDirection + lightDirection;
            if (centroid.sqrMagnitude <= Epsilon)
            {
                centroid = apertureDirection;
            }

            float lambert = Mathf.Clamp01(Vector3.Dot(surfaceNormal, centroid.normalized));

            return new AmbientApertureResult
            {
                ApertureArea = apertureArea,
                LitArea = litArea,
                DirectVisibility = lightArea > Epsilon ? litArea / lightArea : 0f,
                AmbientVisibility = Mathf.Max(0f, apertureArea - litArea) / TwoPi,
                Lambert = lambert
            };
        }

        private static float AcosSafe(float value)
        {
            return Mathf.Acos(Mathf.Clamp(value, -1f, 1f));
        }
    }

    public struct AmbientApertureResult
    {
        public float ApertureArea;
        public float LitArea;
        public float DirectVisibility;
        public float AmbientVisibility;
        public float Lambert;
    }
}
