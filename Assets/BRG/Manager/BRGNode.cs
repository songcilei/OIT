using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BRGNode : MonoBehaviour
{
    private void OnEnable()
    {
        
    }

    void Start()
    {
        BrgObjInfo info = new BrgObjInfo()
        {
            name = "test",
            trans = this.transform,
            mesh = GetComponent<MeshFilter>().sharedMesh,
            material = GetComponent<MeshRenderer>().sharedMaterial
            
        };
        Renderer rd = this.GetComponent<Renderer>();
        rd.enabled = false;
        BRGManager.Instance.InitBuffer(info);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
