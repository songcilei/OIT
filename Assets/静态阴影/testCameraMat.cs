using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

public class testCameraMat : MonoBehaviour
{
    public GameObject obj;

    private Camera cam;
    public Material TestMat;
    
    [Button]
    public void ChangeT()
    {
        cam = GetComponent<Camera>();
        Matrix4x4 mat = this.transform.worldToLocalMatrix;
        Matrix4x4 pmat = cam.projectionMatrix;
      
        Matrix4x4 mvp = pmat * mat;
        TestMat.SetMatrix("_SMat",mvp);
        
        Debug.Log(mvp);
        Vector3 pos = mvp * new Vector4(obj.transform.position.x,obj.transform.position.y,obj.transform.position.z,1);
        Debug.Log(pos);
    }
}
