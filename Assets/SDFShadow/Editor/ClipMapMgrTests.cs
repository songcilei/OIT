using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ClipMapMgrTests
{
    [TestCase(-4f, 0.31f, -4.01f, -32, 2, -33, 32, 10, 31, -4f, 0.25f, -4.125f, 4f, 1.25f, 3.875f)]
    public void CalculateAlignedBoundsUsesVoxelBoundaries(
        float desiredX,
        float desiredY,
        float desiredZ,
        int expectedMinX,
        int expectedMinY,
        int expectedMinZ,
        int expectedMaxX,
        int expectedMaxY,
        int expectedMaxZ,
        float expectedWorldMinX,
        float expectedWorldMinY,
        float expectedWorldMinZ,
        float expectedWorldMaxX,
        float expectedWorldMaxY,
        float expectedWorldMaxZ)
    {
        MethodInfo method = typeof(ClipMapMgr).GetMethod(
            "CalculateAlignedBounds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "ClipMap bounds calculation must be centralized and voxel-aligned.");

        object[] arguments =
        {
            new Vector3(desiredX, desiredY, desiredZ),
            new Vector3Int(64, 8, 64),
            0.125f,
            null,
            null,
            null,
            null
        };

        method.Invoke(null, arguments);

        Assert.That((Vector3Int)arguments[3], Is.EqualTo(new Vector3Int(expectedMinX, expectedMinY, expectedMinZ)));
        Assert.That((Vector3Int)arguments[4], Is.EqualTo(new Vector3Int(expectedMaxX, expectedMaxY, expectedMaxZ)));
        Assert.That((Vector3)arguments[5], Is.EqualTo(new Vector3(expectedWorldMinX, expectedWorldMinY, expectedWorldMinZ)));
        Assert.That((Vector3)arguments[6], Is.EqualTo(new Vector3(expectedWorldMaxX, expectedWorldMaxY, expectedWorldMaxZ)));
    }

    [Test]
    public void CollectEnteringVoxelsIncludesPositiveMaxSlice()
    {
        List<Vector3Int> result = InvokeCollectEnteringVoxels(
            new Vector3Int(-2, 0, -2), new Vector3Int(2, 2, 2),
            new Vector3Int(-1, 0, -2), new Vector3Int(3, 2, 2));

        Assert.That(result, Does.Contain(new Vector3Int(2, 0, -2)));
        Assert.That(result, Does.Contain(new Vector3Int(2, 1, 1)));
        Assert.That(result.Count, Is.EqualTo(8));
    }

    [Test]
    public void CollectEnteringVoxelsIncludesNegativeMinSlice()
    {
        List<Vector3Int> result = InvokeCollectEnteringVoxels(
            new Vector3Int(-2, 0, -2), new Vector3Int(2, 2, 2),
            new Vector3Int(-3, 0, -2), new Vector3Int(1, 2, 2));

        Assert.That(result, Does.Contain(new Vector3Int(-3, 0, -2)));
        Assert.That(result, Does.Contain(new Vector3Int(-3, 1, 1)));
        Assert.That(result.Count, Is.EqualTo(8));
    }

    [Test]
    public void CollectEnteringVoxelsExcludesOverlap()
    {
        List<Vector3Int> result = InvokeCollectEnteringVoxels(
            new Vector3Int(-2, 0, -2), new Vector3Int(2, 2, 2),
            new Vector3Int(-1, 0, -2), new Vector3Int(3, 2, 2));

        Assert.That(result.Contains(new Vector3Int(-1, 0, -2)), Is.False);
    }

    [TestCase(127, 0, 0, false)]
    [TestCase(128, 0, 0, true)]
    [TestCase(0, 8, 0, true)]
    public void RequiresFullRefreshUsesCacheDimensions(
        int deltaX, int deltaY, int deltaZ, bool expected)
    {
        MethodInfo method = typeof(ClipMapMgr).GetMethod(
            "RequiresFullRefresh",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        bool actual = (bool)method.Invoke(null, new object[]
        {
            Vector3Int.zero,
            new Vector3Int(deltaX, deltaY, deltaZ),
            new Vector3Int(128, 8, 128)
        });

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateDesiredWorldMinKeepsZeroOffsetBehavior()
    {
        Vector3 result = InvokeCalculateDesiredWorldMin(
            new Vector3(2f, 3f, 4f),
            Quaternion.identity,
            Vector3.zero,
            new Vector3Int(4, 2, 6),
            0.5f);

        Assert.That(result, Is.EqualTo(new Vector3(1f, 2.5f, 2.5f)));
    }

    [Test]
    public void CalculateDesiredWorldMinRotatesLocalOffsetWithCamera()
    {
        const float halfSqrtTwo = 0.70710678f;
        Quaternion yaw90 = new Quaternion(0f, halfSqrtTwo, 0f, halfSqrtTwo);

        Vector3 result = InvokeCalculateDesiredWorldMin(
            Vector3.zero,
            yaw90,
            new Vector3(0f, 0f, 2f),
            new Vector3Int(4, 2, 6),
            0.5f);

        Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(-0.5f).Within(0.0001f));
        Assert.That(result.z, Is.EqualTo(-1.5f).Within(0.0001f));
    }

    private static List<Vector3Int> InvokeCollectEnteringVoxels(
        Vector3Int oldMin,
        Vector3Int oldMax,
        Vector3Int newMin,
        Vector3Int newMax)
    {
        MethodInfo method = typeof(ClipMapMgr).GetMethod(
            "CollectEnteringVoxels",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        List<Vector3Int> result = new List<Vector3Int>();
        method.Invoke(null, new object[] { oldMin, oldMax, newMin, newMax, result });
        return result;
    }

    private static Vector3 InvokeCalculateDesiredWorldMin(
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Vector3 localCenterOffset,
        Vector3Int gridDimensions,
        float voxelSize)
    {
        MethodInfo method = typeof(ClipMapMgr).GetMethod(
            "CalculateDesiredWorldMin",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (Vector3)method.Invoke(null, new object[]
        {
            cameraPosition,
            cameraRotation,
            localCenterOffset,
            gridDimensions,
            voxelSize
        });
    }
}
