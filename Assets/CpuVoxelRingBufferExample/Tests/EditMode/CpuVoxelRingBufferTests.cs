using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CpuVoxelRingBufferTests
{
    [Test]
    public void WorldPositionToVoxelUsesFloorForNegativeCoordinates()
    {
        Vector3Int voxel = CpuVoxelRingBufferDemo.WorldPositionToVoxel(
            new Vector3(-0.1f, 2.9f, -2.0f),
            1.0f);

        Assert.AreEqual(new Vector3Int(-1, 2, -2), voxel);
    }

    [Test]
    public void CalculateCenteredMinPlacesHalfTheCacheBeforeCenter()
    {
        Vector3Int min = CpuVoxelRingBufferDemo.CalculateCenteredMin(
            new Vector3Int(10, 3, -5),
            new Vector3Int(8, 4, 8));

        Assert.AreEqual(new Vector3Int(6, 1, -9), min);
    }

    [Test]
    public void ConstructorBuildsEveryVoxelAndSupportsNegativeCoordinates()
    {
        var buffer = new CpuVoxelRingBuffer(
            new Vector3Int(4, 3, 2),
            new Vector3Int(-2, -1, -1));

        Assert.AreEqual(new Vector3Int(4, 3, 2), buffer.Size);
        Assert.AreEqual(new Vector3Int(-2, -1, -1), buffer.MinWorldVoxel);
        Assert.AreEqual(24, buffer.LastUpdatedVoxelCount);
        Assert.AreEqual(
            new Vector3Int(3, 2, 1),
            buffer.WorldToBufferIndex(new Vector3Int(-1, -1, -1)));
        Assert.AreEqual(
            CpuVoxelRingBuffer.CreateSampleValue(-1, -1, -1),
            buffer.GetWorldVoxel(new Vector3Int(-1, -1, -1)));
    }

    [TestCase(0, 1, 1)]
    [TestCase(1, 0, 1)]
    [TestCase(1, 1, 0)]
    [TestCase(-1, 1, 1)]
    public void ConstructorRejectsInvalidSize(int x, int y, int z)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuVoxelRingBuffer(new Vector3Int(x, y, z), Vector3Int.zero));
    }

    [Test]
    public void ReadingOutsideCoveredRangeThrows()
    {
        var buffer = CreateBuffer();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            buffer.GetWorldVoxel(new Vector3Int(4, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            buffer.GetWorldVoxel(new Vector3Int(-1, 0, 0)));
    }

    [Test]
    public void MovingOneCellOnXWritesOnlyOneYZSlice()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(new Vector3Int(1, 0, 0));

        Assert.AreEqual(6, buffer.LastUpdatedVoxelCount);
        AssertCoveredValuesAreCorrect(buffer);
    }

    [Test]
    public void MovingOnTwoAxesWritesUnionOfNewSlices()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(new Vector3Int(1, 0, 1));

        Assert.AreEqual(15, buffer.LastUpdatedVoxelCount);
        AssertCoveredValuesAreCorrect(buffer);
    }

    [Test]
    public void MovingMultipleCellsWritesAllNewSlices()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(new Vector3Int(2, 0, 0));

        Assert.AreEqual(12, buffer.LastUpdatedVoxelCount);
        AssertCoveredValuesAreCorrect(buffer);
    }

    [Test]
    public void MovingToSameOriginWritesNothing()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(Vector3Int.zero);

        Assert.AreEqual(0, buffer.LastUpdatedVoxelCount);
        AssertCoveredValuesAreCorrect(buffer);
    }

    [Test]
    public void TeleportingAtLeastOneCacheWidthRebuildsEverything()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(new Vector3Int(4, 0, 0));

        Assert.AreEqual(24, buffer.LastUpdatedVoxelCount);
        AssertCoveredValuesAreCorrect(buffer);
    }

    [Test]
    public void MovingIntoNegativeWorldCoordinatesKeepsIndicesValid()
    {
        var buffer = CreateBuffer();

        buffer.MoveTo(new Vector3Int(-2, -1, -1));

        for (int z = -1; z < 1; z++)
        for (int y = -1; y < 2; y++)
        for (int x = -2; x < 2; x++)
        {
            Vector3Int index = buffer.WorldToBufferIndex(new Vector3Int(x, y, z));
            Assert.That(index.x, Is.InRange(0, 3));
            Assert.That(index.y, Is.InRange(0, 2));
            Assert.That(index.z, Is.InRange(0, 1));
        }

        AssertCoveredValuesAreCorrect(buffer);
    }

    private static CpuVoxelRingBuffer CreateBuffer()
    {
        return new CpuVoxelRingBuffer(new Vector3Int(4, 3, 2), Vector3Int.zero);
    }

    private static void AssertCoveredValuesAreCorrect(CpuVoxelRingBuffer buffer)
    {
        Vector3Int min = buffer.MinWorldVoxel;
        Vector3Int max = min + buffer.Size;

        for (int z = min.z; z < max.z; z++)
        for (int y = min.y; y < max.y; y++)
        for (int x = min.x; x < max.x; x++)
        {
            var worldVoxel = new Vector3Int(x, y, z);
            Assert.AreEqual(
                CpuVoxelRingBuffer.CreateSampleValue(x, y, z),
                buffer.GetWorldVoxel(worldVoxel),
                $"世界体素 {worldVoxel} 的缓存值不正确。" );
        }
    }
}
