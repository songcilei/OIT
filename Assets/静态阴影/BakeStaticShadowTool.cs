using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BakeStaticShadowTool : MonoBehaviour
{

    public Light light;
    public Vector3 BlockLength;

    public int RTWidth = 1024;
    
    
    [Button]
    public void BakeStaticShadow()
    {
        //compute cameraPosition and create camera
        Camera cam = ComputeCamPos(BlockLength);
        
        //create rt
        RenderTexture rt = RenderTexture.GetTemporary(RTWidth, RTWidth, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;
        rt.autoGenerateMips = false;
        rt.Create();

        cam.targetTexture = rt;
        ShadeUnilit.ReplaceObjectsShader(Shader.Find("shadowDepth"));
        cam.Render();
        ShadeUnilit.RevertObjectShader();
        
        
        //Draw rt

        //Save rt 2 png

        //Set png 2 mat

    }

    private Camera ComputeCamPos(Vector3 BoundLength, float CamDist = 1)
    {
        //create root
        GameObject root = new GameObject();
        root.name = "root";
        Vector3 center = new Vector3(BoundLength.x / 2, 0, BoundLength.z / 2);
        root.transform.position = center;
        root.transform.localScale = Vector3.one;
        root.transform.eulerAngles = new Vector3(0, 0, 0);

        //create cam
        GameObject camObj = new GameObject();
        camObj.name = "camRoot";
        camObj.AddComponent<Camera>();
        Camera cam = camObj.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = BoundLength.x / 2; 

        //init cam position
        float LightPitchAngle = light.transform.eulerAngles.x;
        float CamZDist = BoundLength.x * CamDist;
        float CamHeight = CamZDist*Mathf.Tan(Mathf.Deg2Rad*LightPitchAngle);
        Debug.Log(Mathf.Tan(Mathf.Deg2Rad*LightPitchAngle));
        camObj.transform.position = new Vector3(center.x, CamHeight, center.z - CamZDist);
        camObj.transform.eulerAngles = new Vector3(light.transform.eulerAngles.x, 0, light.transform.eulerAngles.z);
        camObj.transform.SetParent(root.transform);
        root.transform.eulerAngles = new Vector3(0, light.transform.eulerAngles.y, 0);
        return cam;
    }



}
