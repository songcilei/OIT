
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 模块管理器:负责注册  注销 驱动所有的IManagerModule
/// </summary>
public class ModuleManager : MonoBehaviour
{
    //单例
    public static ModuleManager Instance { get; private set; }
    //储存所有已注册的模块
    private readonly Dictionary<string, IManagedModule> _moduleDict = new Dictionary<string, IManagedModule>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 注册模块
    /// </summary>
    public void Register(IManagedModule module)
    {
        if (module == null) return;

        string name = module.ModuleName;
        if (_moduleDict.ContainsKey(name))
        {
            Debug.LogWarning("Module already exists: " + name);
            return;
        }
        _moduleDict.Add(name, module);
        module.OnRegister();
    }

    /// <summary>
    /// 注销模块
    /// </summary>
    public void UnRegister(IManagedModule module)
    {
        if (module == null) return;
        string name = module.ModuleName;
        if (_moduleDict.TryGetValue(name,out var exist)&&exist == module)
        {
            _moduleDict.Remove(name);
            module.OnUnRegister();
        }
    }

    /// <summary>
    /// 根据名字获取模块
    /// </summary>
    /// <param name="moduleName"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetModule<T>(string moduleName) where T : class, IManagedModule
    {
        if (_moduleDict.TryGetValue(moduleName,out var module))
        {
            return module as T;
        }
        return null;
    }

    /// <summary>
    /// 泛型快速查找（推荐，模块名和类型一一对应）
    /// </summary>
    public T GetModule<T>() where T : class, IManagedModule
    {
        foreach (var pair in _moduleDict)
        {
            if (pair.Value is T target)
            {
                return target;
            }
        }
        return null;
    }
    
    private void Update()
    {
        //float dt = Time.deltaTime;
        // 遍历驱动所有模块更新
        // foreach (var module in _moduleDict.Values)
        // {
        //     module.ModuleUpdate(dt);
        // }
    }

    // 销毁时全部注销
    private void OnDestroy()
    {
        var list = new List<IManagedModule>(_moduleDict.Values);
        foreach (var m in list)
        {
            UnRegister(m);
        }
        _moduleDict.Clear();
        Instance = null;
    }
}
