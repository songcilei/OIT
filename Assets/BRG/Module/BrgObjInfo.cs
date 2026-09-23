using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[Serializable]
public class BrgObjInfo
{
    public string name;
    public BatchBufferInfo _batchInfo;
    public Mesh mesh;
    public Material material;
    public Transform trans;
}
