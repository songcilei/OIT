using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class AmbientSample : MonoBehaviour
{


    public ComputeShader cs;
    public Cubemap cubemap;
    public AmbientCubeData data;
    public Material debugMat;
    
    [Button]
    public void Bake()
    {
        data = AmbientCubeBaker.BakeAmbientCube(cubemap, cs);
        Debug.Log("Up:"+data.up);
        Debug.Log("Down:"+data.down);
        Debug.Log("left:"+data.left);
        Debug.Log("right:"+data.right);
        Debug.Log("forward:"+data.forward);
        Debug.Log("back:"+data.back);
        
        debugMat.SetVector("_Up",data.up);
        debugMat.SetVector("_Down",data.down);
        debugMat.SetVector("_Left",data.left);
        debugMat.SetVector("_Right",data.right);
        debugMat.SetVector("_Front",data.forward);
        debugMat.SetVector("_Back",data.back);

        Vector4[] CambientCube = new[]
        {

            //这里是因为 shader内计算的顺序和 Unity 的顺序不一致，所以这里要转换一下
            //shader内为了快速计算，所以把顺序换了
            new Vector4(data.right.x,data.right.y,data.right.z,1),
            new Vector4(data.left.x,data.left.y,data.left.z,1),
            new Vector4(data.up.x,data.up.y,data.up.z,1),
            new Vector4(data.down.x,data.down.y,data.down.z,1),
            new Vector4(data.forward.x,data.forward.y,data.forward.z,1),
            new Vector4(data.back.x,data.back.y,data.back.z,1),
            
        };
        debugMat.SetVectorArray("cAmbientCube",CambientCube);
    }
}
