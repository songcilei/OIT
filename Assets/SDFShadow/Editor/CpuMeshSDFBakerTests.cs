using NUnit.Framework;
using UnityEngine;

namespace SDFShadow.Editor
{
    public sealed class CpuMeshSDFBakerTests
    {
        [Test]
        public void DistanceToTriangleReturnsPointHeightAboveFace()
        {
            float distance = CpuMeshSDFBaker.DistanceToTriangle(
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0.25f, 0.25f, 2f));

            Assert.That(distance, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void IsPointInsideClosedMeshDetectsSimpleTetrahedronInterior()
        {
            var triangles = new[]
            {
                new CpuMeshSDFBaker.Triangle(new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f)),
                new CpuMeshSDFBaker.Triangle(Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f)),
                new CpuMeshSDFBaker.Triangle(Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f)),
                new CpuMeshSDFBaker.Triangle(Vector3.zero, new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f))
            };

            Assert.IsTrue(CpuMeshSDFBaker.IsPointInsideClosedMesh(new Vector3(0.1f, 0.1f, 0.1f), triangles));
            Assert.IsFalse(CpuMeshSDFBaker.IsPointInsideClosedMesh(new Vector3(2f, 2f, 2f), triangles));
        }
    }
}
