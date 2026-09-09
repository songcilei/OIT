using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class matrixTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Matrix4x4 mat;


        float fov=60;
        float ScreenHeight=1024,ScreenWidht=1024;
        float nearClip=0.01f, farClip = 100.0f;


        Matrix4x4 world = transform.localToWorldMatrix;
        Matrix4x4 view = Camera.main.worldToCameraMatrix;
        Matrix4x4 project = Matrix4x4.Perspective(fov, ScreenWidht / ScreenHeight, nearClip, farClip);

        Matrix4x4 ortho = Matrix4x4.Ortho(-10, 10, -10, 10, 0, 100);
    }

    
}
