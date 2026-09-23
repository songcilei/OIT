using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 可注册模块的统一接口
/// </summary>
public interface IManagedModule
{
    /// <summary>
    /// 模块唯一ID
    /// </summary>
    string ModuleName { get; }
    //注册回调
    void OnRegister();
    //注销回调
    void OnUnRegister();
    //每帧更新  
    void ModuleUpdate(float deltaTime);
}
