using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FabrikIKConstraint))]
public class FabrikIKConstraintEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var constraint = (FabrikIKConstraint)target;

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("记录当前骨骼姿态"))
            {
                Undo.RecordObject(constraint, "Capture FABRIK Rest Pose");
                constraint.CaptureRestPose();
                EditorUtility.SetDirty(constraint);
            }

            if (GUILayout.Button("复位骨骼"))
            {
                Undo.RecordObjects(GetUndoTargets(constraint), "Reset FABRIK Bones");
                constraint.ResetToRestPose();
                EditorUtility.SetDirty(constraint);
            }
        }
    }

    private static Object[] GetUndoTargets(FabrikIKConstraint constraint)
    {
        var objects = new List<Object> { constraint };
        IReadOnlyList<Transform> joints = constraint.Joints;

        for (int i = 0; i < joints.Count; i++)
        {
            if (joints[i] != null)
            {
                objects.Add(joints[i]);
            }
        }

        return objects.ToArray();
    }
}
