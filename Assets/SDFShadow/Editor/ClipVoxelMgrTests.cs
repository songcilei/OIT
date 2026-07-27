using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using SDFShadow;
using UnityEngine;

public sealed class ClipVoxelMgrTests
{
    [Test]
    public void ResetVoxelsClearsReusedToroidalSlots()
    {
        float[,,] cache = new float[4, 2, 4];
        ClipVoxelMgr manager = (ClipVoxelMgr)FormatterServices.GetUninitializedObject(typeof(ClipVoxelMgr));
        SetField(manager, "cacheGrid", new Vector3Int(4, 2, 4));
        SetField(manager, "clipMapArray", cache);
        cache[0, 0, 0] = -5f;

        MethodInfo method = typeof(ClipVoxelMgr).GetMethod(
            "ResetVoxels",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        method.Invoke(manager, new object[]
        {
            new List<Vector3Int> { new Vector3Int(4, 0, 0) }
        });

        Assert.That(cache[0, 0, 0], Is.EqualTo(1f));
    }

    [Test]
    public void ResetAllVoxelsClearsEntireCache()
    {
        float[,,] cache = new float[2, 2, 2];
        cache[0, 0, 0] = -1f;
        cache[1, 1, 1] = -2f;
        ClipVoxelMgr manager = CreateManager(cache, new Vector3Int(2, 2, 2));

        Invoke(manager, "ResetAllVoxels");

        foreach (float value in cache)
        {
            Assert.That(value, Is.EqualTo(1f));
        }
    }

    [Test]
    public void DetailAabbRejectsExclusiveMaximum()
    {
        ClipVoxelMgr manager = CreateManager(new float[4, 4, 4], new Vector3Int(4, 4, 4));

        bool inside = (bool)Invoke(
            manager,
            "DetailAABBIntersect",
            Vector3.zero,
            new Vector3(4, 4, 4),
            new Vector3(3, 3, 3));
        bool atMaximum = (bool)Invoke(
            manager,
            "DetailAABBIntersect",
            Vector3.zero,
            new Vector3(4, 4, 4),
            new Vector3(4, 3, 3));

        Assert.That(inside, Is.True);
        Assert.That(atMaximum, Is.False);
    }

    [Test]
    public void AabbDoesNotIntersectWhenHalfOpenBoundsOnlyTouch()
    {
        ClipVoxelMgr manager = CreateManager(new float[4, 4, 4], new Vector3Int(4, 4, 4));

        bool intersects = (bool)Invoke(
            manager,
            "IsAABBIntersect",
            Vector3.zero,
            new Vector3(4, 4, 4),
            new Vector3(4, 0, 0),
            new Vector3(8, 4, 4));

        Assert.That(intersects, Is.False);
    }

    private static ClipVoxelMgr CreateManager(float[,,] cache, Vector3Int dimensions)
    {
        ClipVoxelMgr manager = (ClipVoxelMgr)FormatterServices.GetUninitializedObject(typeof(ClipVoxelMgr));
        SetField(manager, "cacheGrid", dimensions);
        SetField(manager, "clipMapArray", cache);
        return manager;
    }

    private static object Invoke(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
