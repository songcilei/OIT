using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ComputeMode
{
    CPU,
    GPU
}


public class AMCreateTool : MonoBehaviour
{
    public Bounds bounds;
    public int resolution;
    AMNode[,] node;
    public GameObject amNode;
    public List<GameObject> AMNodes;
    public ComputeMode ComputeMode = ComputeMode.GPU;
    public bool Debug = true;
    
    [Button(ButtonSizes.Gigantic)]
    public void Create()
    { 
        AMNodes.Clear();
        Vector3 startPos = bounds.center+transform.position-bounds.extents;
        int xLoop = (int)bounds.extents.x*2/resolution;
        int zLoop = (int)bounds.extents.z*2/resolution;
        int yLoop = (int)bounds.extents.y*2/resolution;

        for (int x = 0; x <= xLoop; x++)
        {
            for (int z = 0; z <= zLoop; z++)
            {
                for (int y = 0; y <= yLoop; y++)
                {
                    Vector3 pos = new Vector3(x*resolution,y*resolution,z*resolution)+startPos;
                    var node = Instantiate(amNode, pos, Quaternion.identity, this.transform);
                    AMNodes.Add(node);
                }
            }
        }

        /*
        foreach (var node in AMNodes)
        {
            AMNode nodeScript = node.GetComponent<AMNode>();
            nodeScript.Bake();
        }
        */
    }
    
    [Button(ButtonSizes.Gigantic)]
    public void BakeReflect()
    {
        foreach (var node in AMNodes)
        {
            ReflectionProbe probe = node.GetComponent<ReflectionProbe>();
            probe.enabled = true;
        }
        foreach (var node in AMNodes)
        {
            AMNode nodeScript = node.GetComponent<AMNode>();
            nodeScript.Bake(ComputeMode);
        }

        foreach (var node in AMNodes)
        {
            ReflectionProbe probe = node.GetComponent<ReflectionProbe>();
            probe.enabled = false;
        }
    }

    [Button(ButtonSizes.Gigantic)]
    public void Clear()
    {
        foreach (var n in AMNodes)
        {
            GameObject.DestroyImmediate(n);
        }
        
        AMNodes.Clear();
        
    }
    
    private void OnDrawGizmos()
    {
        if (!Debug)
        {
            return;
        }
        if (bounds!=null)
        {
            Gizmos.color = new Color(0, .8f, 0, 0.5f);
            Gizmos.DrawCube(bounds.center+transform.position, bounds.size);
        }
    }
}
