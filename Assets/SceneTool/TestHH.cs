using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestHH : MonoBehaviour
{
    public ReadHeightMap rm;
    public void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float x = transform.position.x;
        float z = transform.position.z;
        rm = GetComponent<ReadHeightMap>();
        
        float h =rm.GetHeight(x, z);
        transform.position = new Vector3(transform.position.x, h, transform.position.z);
    }
}
