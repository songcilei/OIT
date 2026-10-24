using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FogTest : MonoBehaviour
{
    public float width;
    public float height;
    public GameObject obj;
    void Start()
    {
        FogValueMgr.Instance.Init(new Vector2(0,0),new Vector2(width,height));
    }


    void Update()
    {
        FogValueMgr.Instance.UpdateHero(new Vector3[]{obj.transform.position});
    }
    
}
