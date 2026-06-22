using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VexolShadowMap;

public class GenerateShadow : MonoBehaviour
{
    private Bounds _bound;
    
    public int maxDepth;
    private FourTree tree;
    public Light _light;
    public Material mat;
    public bool debugMode = true;
    public int DebugIndex = 1;
    public void Start()
    {
        GenerateTree();
    }

    public void GenerateTree()
    {
        Bounds aabb = this.GetComponent<MeshRenderer>().bounds;
        int maxSize = Mathf.NextPowerOfTwo((int)aabb.size.x);
 
        //这里要设置为2的次幂  原因是为了转成图片之之后可以根据整数进行子叶的索引判断 
        _bound.size = new Vector3(maxSize, aabb.size.y, maxSize);
        Debug.Log(_bound.size);
        _bound.center = new Vector3(maxSize / 2, aabb.center.y, maxSize / 2);
        // _bound.center = _bound.center / 2 * 2;
        tree = new FourTree(_bound,maxDepth);
        fillTreeData();
        CombineNodes();
       
        CreateTex();
    }

    /// <summary>
    /// 对最下层子集进行检测是否被阴影遮挡
    /// </summary>
    public void fillTreeData()
    {
        foreach (var node in tree.Nodes)
        {
            if (node.depth == maxDepth)
            {
                ComputeRay(node);
            }
        }
    }

    /// <summary>
    /// 进行检测是否被遮挡
    /// </summary>
    /// <param name="node"></param>
    private void ComputeRay(Node node)
    {
        Vector3 lightDir = -_light.transform.forward;
        RaycastHit hit;
        if (Physics.Raycast(node.bound.center, lightDir, out hit, 1000))
        {
            // Debug.DrawRay(node.bound.center, lightDir, Color.red);
            node.flag = 1;
        }
        else
        {
            node.flag = 0;
        }
    }

    /// <summary>
    /// 合并 四叉树节点
    /// </summary>
    public void CombineNodes()
    {
        for (int i = maxDepth-1; i > 0; i--)
        {
            foreach (var treeNode in tree.Nodes)
            {
                if (treeNode.depth == i)
                {
                    if (treeNode.GetChildFlag())
                    {
                        treeNode.flag = 1;
                    }
                    else
                    {
                        treeNode.flag = 0;
                    }
                }
            }
        }
    }


    
    public void CreateTex()
    {
        Debug.Log(tree.Nodes.Count);
        int texSize =  Mathf.CeilToInt(Mathf.Sqrt(tree.Nodes.Count));
        Debug.Log(texSize);
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, false, true);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[texSize*texSize];
        for (int i = 0; i < tree.Nodes.Count; i++)
        {
            colors[i].r = tree.Nodes[i].x;
            colors[i].g = tree.Nodes[i].z;
            colors[i].b = tree.Nodes[i].children != null ? tree.Nodes[i].children[0].index : -1;
            
            colors[i].a = tree.Nodes[i].flag;
            // Debug.Log(tree.Nodes[i].children != null ? tree.Nodes[i].children[0].index/255.0f : -1);
            // Debug.Log(colors[i].r +"::"+colors[i].g+"::"+colors[i].b + ":::"+colors[i].a+"::::"+i);
            
        }
        tex.SetPixels(colors);
        tex.Apply();
        mat.SetTexture("_VexolTex",tex);
        mat.SetInt("_TreeTexWidth",(int)texSize);
        mat.SetInt("_Depth",maxDepth);
        mat.SetTexture("_MainTex",tex);
        
        
    }

    private void OnDrawGizmos()
    {
        if (tree==null)
        {
            return;
        }

        if (debugMode)
        {
            tree.root.GizmosBound(0.1f,-_light.transform.forward);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_bound.center,_bound.size+new Vector3(0,0.1f,0));

            if (DebugIndex!=-1)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(tree.Nodes[DebugIndex].bound.center,tree.Nodes[DebugIndex].bound.size+new Vector3(0,0.1f,0));
            }
            
        }
    }
}
