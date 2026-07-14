using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteInEditMode]
public class MultiSegmentBezierCurve : MonoBehaviour
{
    public List<CurveSegment> segments = new List<CurveSegment>();
    public bool isLoop = false;

    public List<Vector3> Points = new List<Vector3>();
    private void Start()
    {
        PointUpdated();
    }

    public void AddSegment()
    {
        var lastPoint = segments.Count > 0 ? segments[^1].point : transform.position;
        var newSegment = new CurveSegment()
        {
            point = lastPoint + Vector3.right*2,
            controlPoint1 = lastPoint + Vector3.right*0.5f,
        };
        newSegment.controlPoint2 = newSegment.point-Vector3.right*0.5f;
        segments.Add(newSegment);
        
    }

    public void RemoveSegment()
    {
        if (segments.Count>1)
        {
            segments.RemoveAt(segments.Count-1);
        }
        //PointUpdated();
    }
    
    
    [SerializeField] private int resolution = 10;//采样率
    public void PointUpdated()
    {
        float length = 0;     //记录此路径的长度
        var list = new List<Vector3>(); //采样后的点List
        var lastPoint = Vector3.zero;
        //逐一遍历所有线段
        for (var i = 0; i < segments.Count; i++)
        {
            CurveSegment prevSegment;
            if (i == 0) //如果是第一个
            {
                list.Add(segments[0].point);    //添加点
                lastPoint = segments[0].point;
                continue;                       //直接进入下一段线段
            }
            else
                prevSegment = segments[i - 1];  //更新prevSegment
        
            var segment = segments[i];
            if (segment.isBezier)
            {
                //对贝塞尔采样 采样精度为resolution
                for (var step = 1; step <= resolution; step++)
                {
                    var t = step / (float)resolution;
                    var point = Mathf.Pow(1 - t, 3) * prevSegment.point +
                                3 * Mathf.Pow(1 - t, 2) * t * segment.controlPoint1 +
                                3 * (1 - t) * Mathf.Pow(t, 2) * segment.controlPoint2 +
                                Mathf.Pow(t, 3) * segment.point;
                    list.Add(point);
                    length += (point - lastPoint).magnitude;//取得此点到上一个点的距离，并累加到路径长度
                    lastPoint = point;
                }
            }
            else
            {
                //对直线则直接取两点
                // list.Add(prevSegment.point);//无需添加直线开始点，因为开始点已经被加入
                var len = (segment.point - prevSegment.point).magnitude;//量直线长度，并累加到路径长度
                length += len;
                list.Add(segment.point);        //添加直线结束点
                lastPoint = segment.point;      
            }

            Points = list;
        }
        //采样完毕，此时可以根据list开始进行生成网格
    }

    
    public Vector3 GetPosition(float  perc,out Vector3 Dir)
    {
        int count = Points.Count;
        int currentCount = Mathf.FloorToInt(count*perc);
        Dir = currentCount < count ? Vector3.Normalize(Points[currentCount] - Points[currentCount - 1]): Vector3.zero;
        return Points[currentCount];
    }
}
