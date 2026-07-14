using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class BGBCMgr : MonoBehaviour
{
    public List<BGCurveNode> CurveNodeList = new List<BGCurveNode>();

    public List<Vector3> Points = new List<Vector3>();




    public void AddSegment()
    {
        var lastPoint = CurveNodeList.Count>0?CurveNodeList[^1].point:transform.position;
        var newPoint = new BGCurveNode()
        {
            point = lastPoint + Vector3.right * 2,
            controlPoint1 = lastPoint + Vector3.right * 0.5f,
        };
        newPoint.controlPoint2 = newPoint.point-Vector3.right*0.5f;
        CurveNodeList.Add(newPoint);
    }
    
    
    public void RemoveSegment()
    {
        if (CurveNodeList.Count>1)
        {
            CurveNodeList.RemoveAt(CurveNodeList.Count-1);
        }
    }
    
    void Update()
    {
        PointUpdated();
    }

    [SerializeField] private int resolution = 10; //采样率
    public void PointUpdated()
    { 
        float length = 0;
        var list = new List<Vector3>();
        var lastPoint = Vector3.zero;//最后的curve点
        for (var i = 0; i < CurveNodeList.Count; i++)
        {
            BGCurveNode prevNode;//上一个curve
            BGCurveNode nextPoint;//下一个curve
            BGCurveNode currentNode;//当前curve
            if (i == 0)
            {
                list.Add(CurveNodeList[0].point);
                lastPoint = CurveNodeList[0].point;
                continue;
            }
            else
            {
                prevNode = CurveNodeList[i - 1];
                nextPoint = CurveNodeList[i+1];
                // CurveNodeList[i].controlPoint1 = (prevNode.point + CurveNodeList[i].point) / 2;
                // CurveNodeList[i].controlPoint2 = CurveNodeList[i].point-(CurveNodeList[i].controlPoint1-CurveNodeList[i].point);
            }
            var node = CurveNodeList[i];




     
            
            //对贝塞尔采样 采样精度为resolution
            for (var step = 1; step <= resolution; step++)
            {
                var t = step / (float)resolution;
                var point = Mathf.Pow(1 - t, 3) * prevNode.point +
                            3 * Mathf.Pow(1 - t, 2) * t * node.controlPoint1 +
                            3 * (1 - t) * Mathf.Pow(t, 2) * node.controlPoint2 +
                            Mathf.Pow(t, 3) * node.point;
                list.Add(point);
                length += (point - lastPoint).magnitude;//取得此点到上一个点的距离，并累加到路径长度
                lastPoint = point;
            }

            Points = list;
        }
        
    }
    
}
