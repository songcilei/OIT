using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DynamicBRGManager 的最小使用示例。
/// 将本脚本与 DynamicBRGManager 挂在场景对象上，指定若干 Mesh/Material 后运行。
/// 本脚本只演示调用方式，不创建普通 Renderer/GameObject 实例。
/// </summary>
public sealed class DynamicBRGExample : MonoBehaviour
{
    public DynamicBRGManager manager;

    [Header("第一种模型")]
    public Mesh firstMesh;
    public Material firstMaterial;
    [Min(0)] public int firstCount = 100;

    [Header("第二种模型")]
    public Mesh secondMesh;
    public Material secondMaterial;
    [Min(0)] public int secondCount = 100;

    [Header("随机范围")]
    public Vector3 randomMin = new Vector3(-50.0f, 0.0f, -50.0f);
    public Vector3 randomMax = new Vector3(50.0f, 20.0f, 50.0f);

    private readonly List<BRGInstanceHandle> _handles =
        new List<BRGInstanceHandle>();

    private void Start()
    {
        if (manager == null)
            manager = GetComponent<DynamicBRGManager>();

        RegisterMany(firstMesh, firstMaterial, firstCount);
        RegisterMany(secondMesh, secondMaterial, secondCount);
    }

    private void RegisterMany(Mesh mesh, Material material, int count)
    {
        if (manager == null || mesh == null || material == null)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(randomMin.x, randomMax.x),
                Random.Range(randomMin.y, randomMax.y),
                Random.Range(randomMin.z, randomMax.z));

            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                Random.rotation,
                Vector3.one);

            BRGInstanceHandle handle = manager.RegisterInstance(mesh, material, matrix);
            _handles.Add(handle);
        }
    }

    /// <summary>示例：注销前 count 个仍然有效的实例。</summary>
    public void RemoveInstances(int count)
    {
        int removed = 0;
        for (int i = _handles.Count - 1; i >= 0 && removed < count; i--)
        {
            if (manager.UnregisterInstance(_handles[i]))
                removed++;

            _handles.RemoveAt(i);
        }
    }
}
