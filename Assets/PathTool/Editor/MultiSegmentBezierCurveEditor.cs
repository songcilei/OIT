using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MultiSegmentBezierCurve))]
public class MultiSegmentBezierCurveEditor : Editor
{
    private void OnSceneGUI()
    {
        var curve = (MultiSegmentBezierCurve)target;

        if (curve.segments.Count == 1) 
        {
            return;
        }

        for (int index = 0; index < curve.segments.Count; index++)
        {
            var isLast = index == curve.segments.Count - 1;//标记当前是否是末尾元素
            CurveSegment lastSegment;
            CurveSegment nextSetment;

            if (index == 0)
            {
                continue;
            }

            if (isLast)
            {//如果是最后一根曲线
                lastSegment = curve.segments[index - 1];
                nextSetment = null;
            }
            else
            {//一般情况
                lastSegment = curve.segments[index - 1];
                nextSetment = curve.segments[index + 1];
            }

            var segment = curve.segments[index];
            
            //Draw segment
            var lastPoint = lastSegment.point;
            var thisPoint = segment.point;
            var cp1 = segment.controlPoint1;
            var cp2 = segment.controlPoint2;

            if (segment.isBezier)
            {
                Handles.color = Color.blue;
                Handles.DrawLine(lastPoint,cp1,4);
                Handles.DrawLine(thisPoint,cp2,4);

                Handles.color = Color.green;
                //绘制贝塞尔
                Handles.DrawBezier(lastPoint,thisPoint,cp1,cp2,Color.green,null,8f);
            }
            else
            {
                Handles.color = Color.green;
                Handles.DrawLine(lastPoint,thisPoint,4f);
            }
            
            
            //-----------------------------------------------------------------
            //首先绘制一个红色圆圈，用于显示point的位置
            Handles.color = Color.red;
            Handles.DrawWireDisc(segment.point, Vector3.up, 1f);

            var controlPoint1 = segment.controlPoint1;
            var controlPoint2 = segment.controlPoint2;

            
            //在绘制可交互的Handles之前，使用BeginChangeCheck
            //提醒Unity开始监听以下的Handles是否正在与用户交互
            EditorGUI.BeginChangeCheck();
        
            //在point的位置上，绘制一个位置Handles（一个小的，可操控的三维坐标）
            var point = Handles.PositionHandle(segment.point, Quaternion.identity);
            if (segment.isBezier)
            {
                //如果是贝塞尔，则绘制控制柄 颜色为蓝色
                Handles.color = Color.blue;
                //在controlPoint1的位置上，绘制一个滑块Handles，Handles.SphereHandleCap表明它的形状是一个球体
                //这个滑块所处平面是 Vector3.up, Vector3.forward所形成平面（也就是主观上的地面），它只能在此平面移动
                controlPoint1 = Handles.Slider2D(cp1, Vector3.up, Vector3.forward, Vector3.right, 0.4f, Handles.SphereHandleCap,Vector2.zero);
                controlPoint2 = Handles.Slider2D(cp2, Vector3.up, Vector3.forward, Vector3.right, 0.4f, Handles.SphereHandleCap,Vector2.zero);
            }
            
            //如果Handles 正在被用户交互，则EndChangeCheck返回true 否则返回false
            //如果用户没有任何Handles的交互 则跳过下面的代码
            if (!EditorGUI.EndChangeCheck()) 
            {
                continue;
            }
            
            //使用RecordObject记录当前的操作。
            //记录操作后，用户可以在操作后使用撤销恢复本次所做的更改
            //这是官方用法，无需担心性能问题。
            Undo.RecordObject(target, "Changed Position");

            //请记住：当用户修改Handles后，相应的Handles会返回修改后的数值
            //我们进行恒等判断，区分用户到底修改了哪个Handles
            //用户每一帧只可能修改一个Handles（因为鼠标只有一个）因此使用elif分支判断即可
            if (segment.point != point)
            {
                //用户更新了点 则应同时移动两个段的控制柄
                var diff = point - segment.point;
                segment.point = point;//segment.point 记录的是第二个点
                segment.controlPoint2 += diff;//这里因为是移动了点  所以第二个控制杆(自身) 和 下一个点的第一个控制杆(自身重叠点) 一起移动
                if (!isLast || curve.isLoop)
                {
                    nextSetment.controlPoint1 += diff;
                }
            }else if (segment.controlPoint1 != controlPoint1)
            {
                //用户更新了控制柄1 则也应当一并更新上一个段的控制柄2
                segment.controlPoint1 = controlPoint1;
                lastSegment.controlPoint2 = lastSegment.point + (lastSegment.point - controlPoint1);
            }else if (segment.controlPoint2 != controlPoint2)
            {
                //用户更新了控制柄2 则应更新下一个段的控制柄1
                segment.controlPoint2 = controlPoint2;
                if (!isLast || curve.isLoop)
                {
                    nextSetment.controlPoint1 = point + (point - controlPoint2);
                }
            }
            

        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var curve = (MultiSegmentBezierCurve)target;

        //检测按钮是否被按下，只需要检查其返回值即可
        if (GUILayout.Button("添加曲线段"))
            curve.AddSegment();
        if (GUILayout.Button("删除最后一段曲线"))
            curve.RemoveSegment();

        //显示一个Label标签
        GUILayout.Label("线段类型");
        //为每个线段添加一个按钮，用于切换线段类型
        for (var index = 1; index < curve.segments.Count; index++)
        {
            var segment = curve.segments[index];
            CurveSegment nextSegment;
            CurveSegment lastSegment;

            lastSegment = curve.segments[index - 1];
            nextSegment = index == curve.segments.Count - 1 ? null : curve.segments[index + 1];
        
            if (GUILayout.Button("切换为" + (segment.isBezier ? "直线" : "贝塞尔") + "类型"))
            {
                segment.isBezier = !segment.isBezier;
                if (segment.isBezier)   //切换回贝塞尔，只需要更新本曲线的控制柄位置即可
                {
                    if (lastSegment != null)
                        segment.controlPoint1 = lastSegment.point + (lastSegment.point - lastSegment.controlPoint2);
                    if (nextSegment != null)
                        segment.controlPoint2 = segment.point + (segment.point - nextSegment.controlPoint1);
                }
                else    //切换到直线 稍微复杂，将前后段的贝塞尔控制柄自动对其到本直线
                {
                    var step = (segment.point - lastSegment.point) / 4;
                    segment.controlPoint1 = lastSegment.point + step;
                    segment.controlPoint2 = segment.point - step;
                    if (nextSegment != null)
                    {   // 更新下一个段的控制柄1
                        DoUpdateCurveSegment(index + 1);
                    }
                    {   // 更新上一个段的控制柄2
                        DoUpdateCurveSegment(index - 1);
                    }
                }
                //注意：在这里通知Unity对视窗进行重绘，即触发OnSceneGUI
                //否则更改无法被实时显示出来
                SceneView.RepaintAll();
            }
        }

        
    }
    
    //工具函数，更新控制柄的位置使对齐前后曲线的控制柄
    private void DoUpdateCurveSegment(int index)
    {
        var curve = (MultiSegmentBezierCurve)target;
        var segment = curve.segments[index];
        if (!segment.isBezier)return;
        CurveSegment nextSegment;
        CurveSegment lastSegment;
        if (index == 0)
        {
            if(curve.segments.Count <= 1)return;
            lastSegment = null;
            nextSegment = curve.segments[1];
        }
        else
        {
            lastSegment = curve.segments[index - 1];
            nextSegment = index == curve.segments.Count - 1 ? null : curve.segments[index + 1];
        }

        if (lastSegment != null)
        {
            segment.controlPoint1 = lastSegment.point + (lastSegment.point - lastSegment.controlPoint2);
        }
        if (nextSegment != null)
        {
            segment.controlPoint2 = segment.point + (segment.point - nextSegment.controlPoint1);
        }
    }
}
