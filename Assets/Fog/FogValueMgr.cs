using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

public class FogValueMgr : MonoBehaviour
{
    public static FogValueMgr Instance;

    public int Cell = 5;
    public Vector2 StartPos = new Vector2(0, 0);
    public Vector2 EndPos = new Vector2(100, 200);

    public int FadeRange = 2;
    public float FadeSpeed = 0.1f;
    private float[,] fogMap;
    public Texture2D tex;
    private Vector2[] _heroPos;
    private int2 _gridSize;
    private bool FogState = false;
    public List<int2> UpdateList = new List<int2>();
    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Prefab加载后Init 初始化迷雾参数
    /// </summary>
    /// <param name="leftDPos"></param>
    /// <param name="rightUPos"></param>
    public void Init(Vector2 leftDPos, Vector2 rightUPos)
    {
        StartPos = leftDPos;
        EndPos = rightUPos;
        Init();
    }
    

    private void Init()
    {
        //tex2D compute size 
        _gridSize = new int2(Mathf.CeilToInt((EndPos.x-StartPos.x) / Cell), Mathf.CeilToInt((EndPos.y-StartPos.y) / Cell));
        fogMap = new float[_gridSize.x, _gridSize.y];
        
   
        tex = new Texture2D(_gridSize.x, _gridSize.y, TextureFormat.R8, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(new Color[_gridSize.x*_gridSize.y]);
        tex.Apply();
        Shader.SetGlobalTexture("_MapInfo",tex);
    }

    /// <summary>
    /// 初始化英雄位置和逻辑 支持多英雄传入
    /// </summary>
    public void UpdateHero(Vector3[] heroPoss)
    {
        _heroPos = new Vector2[heroPoss.Length];
        for (int i = 0; i < heroPoss.Length; i++)
        {
            _heroPos[i] = new Vector2(heroPoss[i].x, heroPoss[i].z);
        }

        FogState = true;
    }

    /// <summary>
    /// 停止运行
    /// </summary>
    public void StopUpdate()
    {
        FogState = false;
    }

    /// <summary>
    /// 解锁全部迷雾
    /// </summary>
    public void UnlockAllFog()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                AddUpdateList(x,y);
            }
        }
    }

    /// <summary>
    /// 解锁区域迷雾
    /// </summary>
    public void UnlockAreaFog(int2[] AreaArray)
    {
        foreach (var block in AreaArray)
        {
            AddUpdateList(block.x,block.y);
        }
    }
    
    void Update()
    {
        //状态控制
        if (!FogState)
        {
            return;
        }

        ComputeHeroRange();
        UpdateValue();
    }


    private int2 grid2TexID(int X, int Y)
    {
        return new int2(_gridSize.x - X, _gridSize.y - Y);
    }

    private void AddUpdateList(int x, int y)
    {
        foreach (var id in UpdateList)
        {
            if (id.x == x && id.y == y)
            {
                return;
            }
        }
        UpdateList.Add(new int2(x, y));
    }

    private void ComputeHeroRange()
    {
        for (int index = 0; index < _heroPos.Length; index++)
        {
            float2 heroPos = _heroPos[index];
            int2 pos = new int2(Mathf.CeilToInt(heroPos.x / Cell), Mathf.CeilToInt (heroPos.y / Cell));
            for (int i = -FadeRange; i <= FadeRange; i++)
            {
                for (int j = -FadeRange; j <= FadeRange; j++)
                {
                    float dist = i * i + j * j;
                    if (dist>FadeRange*FadeRange)
                    {
                        continue;
                    }
                    int2 grid = new int2(i, j);
                    if (pos.x+grid.x >= 0 && pos.x+grid.x < _gridSize.x && pos.y+grid.y >= 0 && pos.y+grid.y < _gridSize.y)
                    {
                        AddUpdateList(pos.x+grid.x,pos.y+grid.y);
                    }
                }
            }
        }
    }

    private void UpdateValue()
    {
        for (int i = UpdateList.Count-1; i >=0 ; i--)
        {
            int2 gridId = UpdateList[i];
            //如果数值已经最大值 则从列表中移除
            if (fogMap[gridId.x,gridId.y]>=1)
            {
                UpdateList.RemoveAt(i);
            }
            else
            {
                fogMap[gridId.x,gridId.y] +=Time.deltaTime * FadeSpeed;
                int2 texID = grid2TexID(gridId.x,gridId.y);
                tex.SetPixel(texID.x,texID.y, new Color(fogMap[gridId.x,gridId.y], fogMap[gridId.x,gridId.y], fogMap[gridId.x,gridId.y]));
            }
        }
        tex.Apply();
    }
}
