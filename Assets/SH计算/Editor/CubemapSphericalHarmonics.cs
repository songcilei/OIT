using System;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class CubemapSphericalHarmonics
{
    private const int CoefficientCount = 9;
    private const double Y00 = 0.28209479177387814;
    private const double Y1 = 0.4886025119029199;
    private const double Y20 = 0.31539156525252005;
    private const double Y22 = 0.5462742152960396;

    public static Vector3[] Calculate(Cubemap cubemap)
    {
        if (cubemap == null) throw new ArgumentNullException(nameof(cubemap));
        if (cubemap.width != cubemap.height || cubemap.width <= 0) throw new ArgumentException("Cubemap faces must be square.", nameof(cubemap));

        int size = cubemap.width;
        var result = new Vector3[CoefficientCount];
        double totalWeight = 0.0;
        CubemapFace[] faces = { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };

        foreach (CubemapFace face in faces)
        {
            Color[] pixels = cubemap.GetPixels(face);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = 2f * (x + 0.5f) / size - 1f;
                    float v = 2f * (y + 0.5f) / size - 1f;
                    Vector3 direction = FaceDirection(face, u, v);
                    double weight = TexelSolidAngle(size, x, y);
                    float[] basis = EvaluateBasis(direction);
                    Color color = pixels[y * size + x];
                    for (int i = 0; i < CoefficientCount; i++)
                    {
                        float value = basis[i] * (float)weight;
                        result[i] += new Vector3(color.r * value, color.g * value, color.b * value);
                    }
                    totalWeight += weight;
                }
            }
        }

        float normalization = (float)(4.0 * Math.PI / totalWeight);
        for (int i = 0; i < result.Length; i++)
        {
            result[i] *= normalization * (i == 0 ? Mathf.PI : i < 4 ? 2f * Mathf.PI / 3f : Mathf.PI / 4f);
        }
        return result;
    }

    public static double CalculateTotalSolidAngle(int faceSize)
    {
        if (faceSize <= 0) throw new ArgumentOutOfRangeException(nameof(faceSize));
        double total = 0.0;
        for (int y = 0; y < faceSize; y++)
            for (int x = 0; x < faceSize; x++) total += TexelSolidAngle(faceSize, x, y);
        return total * 6.0;
    }

    public static string Format(Vector3[] coefficients)
    {
        if (coefficients == null || coefficients.Length != CoefficientCount) throw new ArgumentException("Exactly nine coefficients are required.", nameof(coefficients));
        var builder = new StringBuilder();
        for (int i = 0; i < coefficients.Length; i++)
        {
            Vector3 c = coefficients[i];
            if (i > 0) builder.Append('\n');
            builder.AppendFormat(CultureInfo.InvariantCulture, "SH{0} = float3({1:F9}, {2:F9}, {3:F9});", i, c.x, c.y, c.z);
        }
        return builder.ToString();
    }

    private static Vector3 FaceDirection(CubemapFace face, float u, float v)
    {
        Vector3 direction;
        switch (face)
        {
            case CubemapFace.PositiveX: direction = new Vector3(1f, -v, -u); break;
            case CubemapFace.NegativeX: direction = new Vector3(-1f, -v, u); break;
            case CubemapFace.PositiveY: direction = new Vector3(u, 1f, v); break;
            case CubemapFace.NegativeY: direction = new Vector3(u, -1f, -v); break;
            case CubemapFace.PositiveZ: direction = new Vector3(u, -v, 1f); break;
            case CubemapFace.NegativeZ: direction = new Vector3(-u, -v, -1f); break;
            default: throw new ArgumentOutOfRangeException(nameof(face));
        }
        return direction.normalized;
    }

    private static double TexelSolidAngle(int size, int x, int y)
    {
        double step = 2.0 / size;
        double x0 = -1.0 + x * step, x1 = x0 + step;
        double y0 = -1.0 + y * step, y1 = y0 + step;
        return RectangleIntegral(x1, y1) - RectangleIntegral(x0, y1) - RectangleIntegral(x1, y0) + RectangleIntegral(x0, y0);
    }

    private static double RectangleIntegral(double x, double y) => Math.Atan2(x * y, Math.Sqrt(1.0 + x * x + y * y));

    private static float[] EvaluateBasis(Vector3 d)
    {
        float x = d.x, y = d.y, z = d.z;
        return new[]
        {
            (float)Y00,
            (float)(Y1 * y),
            (float)(Y1 * z),
            (float)(Y1 * x),
            1.0925484305920792f * x * y,
            1.0925484305920792f * y * z,
            (float)(Y20 * (3.0 * z * z - 1.0)),
            1.0925484305920792f * x * z,
            (float)(Y22 * (x * x - y * y))
        };
    }
}
