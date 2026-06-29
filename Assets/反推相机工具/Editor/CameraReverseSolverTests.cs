using NUnit.Framework;
using UnityEngine;

namespace CameraReverseTool.Editor
{
    public sealed class CameraReverseSolverTests
    {
        [Test]
        public void TwoPlanePresetCreatesPerpendicularPlanesWithSharedEdge()
        {
            Vector3[] points = CameraReverseGeometry.CreateTwoPlanePoints(4f, 2f, 3f);

            Assert.AreEqual(8, points.Length);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), points[0]);
            Assert.AreEqual(new Vector3(4f, -1f, 0f), points[1]);
            Assert.AreEqual(new Vector3(4f, 1f, 0f), points[2]);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), points[3]);
            Assert.AreEqual(points[0], points[4]);
            Assert.AreEqual(new Vector3(0f, -1f, 3f), points[5]);
            Assert.AreEqual(new Vector3(0f, 1f, 3f), points[6]);
            Assert.AreEqual(points[3], points[7]);
        }

        [Test]
        public void CubePresetCreatesEightCenteredCorners()
        {
            Vector3[] points = CameraReverseGeometry.CreateCubePoints(2f, 4f, 6f);

            Assert.AreEqual(8, points.Length);
            Assert.AreEqual(new Vector3(-1f, -2f, -3f), points[0]);
            Assert.AreEqual(new Vector3(1f, -2f, -3f), points[1]);
            Assert.AreEqual(new Vector3(1f, 2f, -3f), points[2]);
            Assert.AreEqual(new Vector3(-1f, 2f, -3f), points[3]);
            Assert.AreEqual(new Vector3(-1f, -2f, 3f), points[4]);
            Assert.AreEqual(new Vector3(1f, -2f, 3f), points[5]);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), points[6]);
            Assert.AreEqual(new Vector3(-1f, 2f, 3f), points[7]);
        }

        [Test]
        public void ProjectPointReturnsNormalizedImagePosition()
        {
            var parameters = new CameraReverseParameters(
                new Vector3(0f, 0f, -5f),
                Quaternion.identity,
                60f);

            bool visible = CameraReverseProjection.TryProject(
                Vector3.zero,
                parameters,
                16f / 9f,
                out Vector2 normalized);

            Assert.IsTrue(visible);
            Assert.That(normalized.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(normalized.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SolverRecoversSyntheticCubeCamera()
        {
            Vector3[] worldPoints = CameraReverseGeometry.CreateCubePoints(2f, 2f, 2f);
            var expected = new CameraReverseParameters(
                new Vector3(0.35f, -0.2f, -6f),
                Quaternion.Euler(2f, -4f, 1f),
                52f);

            var imagePoints = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                Assert.IsTrue(CameraReverseProjection.TryProject(worldPoints[i], expected, 1f, out imagePoints[i]));
            }

            var initial = new CameraReverseParameters(Vector3.back * 5f, Quaternion.identity, 60f);
            CameraReverseSolveResult result = CameraReverseSolver.Solve(worldPoints, imagePoints, initial, 1f);

            Assert.IsTrue(result.Success);
            Assert.That(result.AverageNormalizedError, Is.LessThan(0.015f));
            Assert.That(result.Parameters.Position.x, Is.EqualTo(expected.Position.x).Within(0.25f));
            Assert.That(result.Parameters.Position.y, Is.EqualTo(expected.Position.y).Within(0.25f));
            Assert.That(result.Parameters.Position.z, Is.EqualTo(expected.Position.z).Within(0.4f));
            Assert.That(result.Parameters.VerticalFov, Is.EqualTo(expected.VerticalFov).Within(5f));
        }
    }
}
