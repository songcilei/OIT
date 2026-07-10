using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(BlockSplitMgr))]
public class BlockSplitMgrEditor : Editor
{
    private BlockSplitMgr mgr;
    private bool[] states;
    private int oldResolution;
    public override void OnInspectorGUI()
    {

        mgr = target as BlockSplitMgr;
        if (states==null||oldResolution != mgr.resolution)
        {
            // if (GUILayout.Button("初始化"))
            // {
                states = new bool[mgr.resolution * mgr.resolution];
                oldResolution = mgr.resolution;
            // }

            return;
        }
        for (int i = 0; i < mgr.resolution; i++)
        {
            GUILayout.BeginHorizontal();
            
            for (int j = 0; j < mgr.resolution; j++)
            {
                int index = i * mgr.resolution + j;

                GUILayout.Label(index.ToString(), GUILayout.Width(20));
                states[index] = GUILayout.Toggle(states[index], "", GUILayout.Width(20), GUILayout.Height(20));
                GUILayout.Space(5);
            }
            GUILayout.EndHorizontal();
            
        }

        if (GUILayout.Button("填入测试数据",GUILayout.Height(100)))
        {
            for (int i = 0; i < 20; i++)
            {
                states[i] = true;
            }
        }


        if (GUILayout.Button("刷新数据",GUILayout.Height(100)))
        {
            int[] indexs = new int[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                indexs[i] = states[i]==true?1:0;
            }
            mgr.ChangeBlockState(indexs);
            
        }

        if (GUILayout.Button("清空所有属性", GUILayout.Height(100)))
        {
            int[] indexs = new int[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = false;
                indexs[i] = 0;
            }
            mgr.ChangeBlockState(indexs);
        }
        
        // if (GUILayout.Button("渲染", GUILayout.Height(100)))
        // {
        //     mgr.RenderMaskTex();
        // }
        base.OnInspectorGUI();
    }

}
