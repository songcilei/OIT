using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AABBBound : MonoBehaviour
{
    private Bounds _bounds;
    private SkinnedMeshRenderer smr;
    void Start()
    {
        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        _bounds = smr.bounds;
    }


    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(_bounds.center, _bounds.extents*2);
    }
}
