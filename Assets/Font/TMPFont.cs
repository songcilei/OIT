using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

[ExecuteAlways]
public sealed class TMPFont : MonoBehaviour
{
    private void OnEnable()
    {
        //防止编辑器重复订阅
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        ScheduleRefreshAll();
        Debug.Log("22222222222");
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshAll;
#endif
        
    }

    private void ScheduleRefreshAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            //等待当前编辑器的反序列化和组件初始化完成
            EditorApplication.delayCall -= RefreshAll;
            EditorApplication.delayCall += RefreshAll;
        }  
#endif
        RefreshAll();
    }


    [ContextMenu("刷新所有UV0")]
    private void RefreshAll()
    {
        Debug.Log("refresh");
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);

        foreach (var tmpText in texts)
        {
            if (tmpText!=null)
            {
                tmpText.ForceMeshUpdate(ignoreActiveState:true);
            }
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }


    private void OnTextChanged(Object changedObject)
    {
   
        TMP_Text tmpText =  changedObject as TMP_Text;
        if (tmpText == null )
        {
            return;
        }
        try
        {
            ModifyUV(tmpText);
            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Uv0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, tmpText);
        }
    }

    private static void ModifyUV(TMP_Text tmpText)
    {
        TMP_TextInfo textInfo = tmpText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible ||
                characterInfo.elementType != TMP_TextElementType.Character ||
                characterInfo.textElement == null)
            {
                continue;
            }

            int materialIndex = characterInfo.materialReferenceIndex;
            int vertexIndex = characterInfo.vertexIndex;
            Vector2[] uv0 = textInfo.meshInfo[materialIndex].uvs0;

            if (uv0 == null || vertexIndex < 0 || vertexIndex + 3 >= uv0.Length)
            {
                continue;
            }

            int atlasIndex = characterInfo.textElement.glyph.atlasIndex;

            for (int v = 0; v < 4; v++)
            {
                int index = vertexIndex + v;
                Vector2 uv = uv0[index];
                uv.x = uv.x - Mathf.Floor(uv.x) + atlasIndex;
                uv0[index] = uv;
            }
        }
    }
    
}
