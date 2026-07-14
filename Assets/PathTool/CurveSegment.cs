using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CurveSegment
{
    public Vector3 point;
    public Vector3 controlPoint1;
    public Vector3 controlPoint2;
    public bool isBezier = true;
}
