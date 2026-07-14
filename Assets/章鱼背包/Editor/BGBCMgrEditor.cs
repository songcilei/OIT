using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(BGBCMgr))]
public class BGBCMgrEditor : Editor
{
    private void OnSceneGUI()
    {
        var curve = (BGBCMgr)target;
        if (curve.CurveNodeList.Count == 0)
        {
            return;
        }
        
        for (int index = 0; index < curve.CurveNodeList.Count; index++)
        {
            var isLast = index == curve.CurveNodeList.Count - 1;
            BGCurveNode lastSegment;
            BGCurveNode nextSetment;
            if (index == 0) continue;

            if (isLast)
            {
                lastSegment = curve.CurveNodeList[index-1];
                nextSetment = null;
            }
            else
            {
                lastSegment = curve.CurveNodeList[index-1];
                nextSetment = curve.CurveNodeList[index+1];
            }

            var segment = curve.CurveNodeList[index];

            var lastPoint = lastSegment.point;
            var thisPoint = segment.point;
            var nextPoint = nextSetment?.point;
            var cp1 = segment.controlPoint1;
            var cp2 = segment.controlPoint2;

            Handles.color = Color.blue;
            Handles.DrawLine(thisPoint,cp1,4);
            Handles.DrawLine(thisPoint,cp2,4);
            Handles.color = Color.green;
            Handles.DrawBezier(lastPoint,thisPoint,cp1,cp2,Color.green,null,8f);
            
            
            //Controll  Point
            Handles.color = Color.red;
            Handles.DrawWireDisc(segment.point, Vector3.up, 1f);

            // var controlPoint1 = segment.controlPoint1;
            // var controlPoint2 = segment.controlPoint2;
            
            EditorGUI.BeginChangeCheck();

            var point = Handles.PositionHandle(segment.point, Quaternion.identity);
            segment.point = point;
            EditorGUI.EndChangeCheck();

        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var curve = (BGBCMgr)target;

        //检测按钮是否被按下，只需要检查其返回值即可
        if (GUILayout.Button("添加曲线段"))
            curve.AddSegment();
        if (GUILayout.Button("删除最后一段曲线"))
            curve.RemoveSegment();
    }
}
