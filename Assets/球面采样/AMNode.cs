using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class AMNode : MonoBehaviour
{
    public Material mat;
    public ReflectionProbe RProbe;
    public AmbientCubeData data;
    private MeshRenderer rd;
    private MeshFilter mf;
    
    
    [Button]
    public void Bake(ComputeMode mode)
    {

        Debug.Log("bake!!!!!");
        rd = this.GetComponent<MeshRenderer>();
        mf = this.GetComponent<MeshFilter>();
        RProbe = this.GetComponent<ReflectionProbe>();
        mat = rd.sharedMaterial;
        Texture cubemap = RProbe.realtimeTexture;
        ComputeShader cs = Resources.Load<ComputeShader>("SampleCubeMap");
//Compute ambient cube        
        data = AmbientCubeBaker.BakeAmbientCube(cubemap, cs,mode,4096);

//set material
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Vector4[] CambientCube = new[]
        {

            //这里是因为 shader内计算的顺序和 Unity 的顺序不一致，所以这里要转换一下
            //shader内为了快速计算，所以把顺序换了
            new Vector4(data.right.x,data.right.y,data.right.z,1),//右
            new Vector4(data.left.x,data.left.y,data.left.z,1),//左
            new Vector4(data.up.x,data.up.y,data.up.z,1),// 上
            new Vector4(data.down.x,data.down.y,data.down.z,1),// 下
            new Vector4(data.forward.x,data.forward.y,data.forward.z,1),// 前
            new Vector4(data.back.x,data.back.y,data.back.z,1),// 后
            
        };
        mpb.SetVectorArray("cAmbientCube",CambientCube);
        rd.SetPropertyBlock(mpb);
    }


    public AmbientCubeData GetData()
    {
        return data;
    }

    public Color[] GetColorFromData()
    {
        return new Color[]
        {
            new Color(data.right.x,data.right.y,data.right.z,1),
            new Color(data.left.x,data.left.y,data.left.z,1),
            new Color(data.up.x,data.up.y,data.up.z,1),
            new Color(data.down.x,data.down.y,data.down.z,1),
            new Color(data.forward.x,data.forward.y,data.forward.z,1),
            new Color(data.back.x,data.back.y,data.back.z,1)
        };
    }
}
