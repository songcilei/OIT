using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SHCalculation.Editor.Tests
{
    public sealed class CubemapSphericalHarmonicsTests
    {
        [Test]
        public void SolidAnglesCoverSphere()
        {
            Assert.That(
                CubemapSphericalHarmonics.CalculateTotalSolidAngle(32),
                Is.EqualTo(4.0 * Math.PI).Within(1e-5));
        }

        [Test]
        public void ConstantCubemapOnlyProducesL0()
        {
            Color color = new Color(0.25f, 0.5f, 1f, 1f);
            Cubemap cubemap = CreateConstantCubemap(16, color);

            try
            {
                Vector3[] coefficients = CubemapSphericalHarmonics.Calculate(cubemap);
                float expectedScale = Mathf.PI / 0.2820947918f;

                Assert.That(coefficients, Has.Length.EqualTo(9));
                Assert.That(coefficients[0].x, Is.EqualTo(color.r * expectedScale).Within(2e-3f));
                Assert.That(coefficients[0].y, Is.EqualTo(color.g * expectedScale).Within(2e-3f));
                Assert.That(coefficients[0].z, Is.EqualTo(color.b * expectedScale).Within(2e-3f));

                for (int i = 1; i < coefficients.Length; i++)
                {
                    Assert.That(coefficients[i].magnitude, Is.LessThan(2e-3f), $"SH{i} was not zero.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cubemap);
            }
        }

        [TestCase(CubemapFace.PositiveX, 3, 1f)]
        [TestCase(CubemapFace.NegativeX, 3, -1f)]
        [TestCase(CubemapFace.PositiveY, 1, 1f)]
        [TestCase(CubemapFace.NegativeY, 1, -1f)]
        [TestCase(CubemapFace.PositiveZ, 2, 1f)]
        [TestCase(CubemapFace.NegativeZ, 2, -1f)]
        public void ColoredFaceProducesExpectedFirstOrderSign(CubemapFace face, int coefficientIndex, float expectedSign)
        {
            Cubemap cubemap = CreateFaceCubemap(16, face, Color.white);

            try
            {
                Vector3[] coefficients = CubemapSphericalHarmonics.Calculate(cubemap);
                Assert.That(Mathf.Sign(coefficients[coefficientIndex].x), Is.EqualTo(expectedSign));
                Assert.That(Mathf.Abs(coefficients[coefficientIndex].x), Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cubemap);
            }
        }

        [Test]
        public void FormatPrintsNineRgbLinesInOrder()
        {
            Vector3[] values = Enumerable.Range(0, 9)
                .Select(i => new Vector3(i, i + 0.25f, i + 0.5f))
                .ToArray();

            string output = CubemapSphericalHarmonics.Format(values);

            StringAssert.StartsWith("SH0 = float3(0.000000000, 0.250000000, 0.500000000);", output);
            StringAssert.Contains("SH8 = float3(8.000000000, 8.250000000, 8.500000000);", output);
            Assert.That(output.Split('\n'), Has.Length.EqualTo(9));
        }

        [Test]
        public void ValidationMaterialUsesStableCoefficientPropertyNames()
        {
            Assert.That(SHValidationMaterial.GetPropertyName(0), Is.EqualTo("_SH0"));
            Assert.That(SHValidationMaterial.GetPropertyName(8), Is.EqualTo("_SH8"));
            Assert.Throws<ArgumentOutOfRangeException>(() => SHValidationMaterial.GetPropertyName(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SHValidationMaterial.GetPropertyName(9));
        }

        private static Cubemap CreateConstantCubemap(int size, Color color)
        {
            var cubemap = new Cubemap(size, TextureFormat.RGBAFloat, false);
            Color[] pixels = Enumerable.Repeat(color, size * size).ToArray();

            foreach (CubemapFace face in Enum.GetValues(typeof(CubemapFace)))
            {
                if (face != CubemapFace.Unknown)
                {
                    cubemap.SetPixels(pixels, face);
                }
            }

            cubemap.Apply(false, false);
            return cubemap;
        }

        private static Cubemap CreateFaceCubemap(int size, CubemapFace coloredFace, Color color)
        {
            var cubemap = new Cubemap(size, TextureFormat.RGBAFloat, false);
            Color[] blackPixels = Enumerable.Repeat(Color.black, size * size).ToArray();
            Color[] coloredPixels = Enumerable.Repeat(color, size * size).ToArray();

            foreach (CubemapFace face in Enum.GetValues(typeof(CubemapFace)))
            {
                if (face != CubemapFace.Unknown)
                {
                    cubemap.SetPixels(face == coloredFace ? coloredPixels : blackPixels, face);
                }
            }

            cubemap.Apply(false, false);
            return cubemap;
        }
    }
}
