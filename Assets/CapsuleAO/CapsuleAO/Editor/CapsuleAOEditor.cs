using CapsuleAOTool;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CapsuleAO))]
public sealed class CapsuleAOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6f);
        CapsuleAO capsuleAO = (CapsuleAO)target;
        using (new EditorGUI.DisabledScope(capsuleAO.GetComponent<CapsuleCollider>() == null))
        {
            if (GUILayout.Button("Copy From CapsuleCollider"))
            {
                Undo.RecordObject(capsuleAO, "Copy Capsule Collider To Capsule AO");
                capsuleAO.CopyFromCapsuleCollider();
                EditorUtility.SetDirty(capsuleAO);
                CapsuleAOShaderGlobals.Upload();
            }
        }
    }
}
