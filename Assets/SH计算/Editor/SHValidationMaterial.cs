using System;
using UnityEngine;

public static class SHValidationMaterial
{
    private const int CoefficientCount = 9;

    public static string GetPropertyName(int index)
    {
        if (index < 0 || index >= CoefficientCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return "_SH" + index;
    }

    public static void Apply(Material material, Vector3[] coefficients)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        if (coefficients == null || coefficients.Length != CoefficientCount)
        {
            throw new ArgumentException("Exactly nine coefficients are required.", nameof(coefficients));
        }

        for (int i = 0; i < coefficients.Length; i++)
        {
            Vector3 coefficient = coefficients[i];
            material.SetVector(GetPropertyName(i), new Vector4(coefficient.x, coefficient.y, coefficient.z, 0f));
        }
    }
}
