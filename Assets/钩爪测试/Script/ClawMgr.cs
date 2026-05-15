using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClawMgr : MonoBehaviour
{

    public GameObject HeadNode;
    public GameObject bodyNode;
    public GameObject endNode;
    
    public List<GameObject> bodyList = new List<GameObject>();

    private Vector2 headNodeSize;//头 宽高
    private Vector2 bodyNodeSize;//身子 宽高
    
    void Start()
    {
        Init();

    }
    
    // Update is called once per frame
    void Update()
    {
        DesntoryBodyNode();
        float totalDist = ComputeDistToBodyList();
        int bodyCount = Mathf.FloorToInt(totalDist / bodyNodeSize.y);
        for (int i = 0; i < bodyCount; i++)
        {
            Vector3 bodyPos = FromPrecToBodyNodePosition(i);
            var bodyObj =GameObject.Instantiate(bodyNode, bodyPos, Quaternion.identity);
            bodyList.Add(bodyObj);
        }
        
    }

    public void Init()
    {
        
        if (HeadNode==null || bodyNode == null || endNode == null)
        {
            return;
        }
        
        Renderer rd_body = bodyNode.GetComponent<Renderer>();
        Vector3 extent_body = rd_body.bounds.extents;
        bodyNodeSize = new Vector2(extent_body.x * 2, extent_body.z * 2);
        
        Renderer rd_head = bodyNode.GetComponent<Renderer>();
        Vector3 extent_head = rd_head.bounds.extents;
        headNodeSize = new Vector2(extent_head.x * 2, extent_head.z * 2);
        
    }

    public void DesntoryBodyNode()
    {
        foreach (var node in bodyList)
        {
            GameObject.DestroyImmediate(node);
        }
        bodyList.Clear();
    }
    

    /// <summary>
    /// 计算 head =>  end 长度  线长 or 弧长
    /// </summary>
    /// <returns></returns>
    public float ComputeDistToBodyList()
    {
        float dist = Vector3.Distance(HeadNode.transform.position, endNode.transform.position);
        return dist;
    }


    public Vector3 FromPrecToBodyNodePosition(int index)
    {
        var dir = Vector3.Normalize(endNode.transform.position-HeadNode.transform.position);
        var pos = HeadNode.transform.position + dir * index * bodyNodeSize.y;
        return pos;
    }
}
