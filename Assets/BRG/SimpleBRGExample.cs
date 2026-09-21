using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 最小 BRG（BatchRendererGroup）学习示例：
/// 把同一个方盒子沿 X / Y 平面排成网格，一次提交并绘制 1000 个实例。
/// </summary>
public unsafe sealed class SimpleBRGExample : MonoBehaviour
{
    [Header("网格设置")]
    [Min(1)] public int columns = 40;
    [Min(0.01f)] public float spacing = 1.2f;

    [Header("可选资源（不填会自动创建）")]
    [Tooltip("不指定时会自动使用 Unity 内置 Cube 网格。")]
    public Mesh mesh;

    [Tooltip("自定义材质的 Shader 必须支持 DOTS Instancing；不指定时会自动创建配套材质。")]
    public Material material;

    // 本例固定绘制 1000 个方盒子，便于把注意力放在 BRG 的核心流程上。
    private const int InstanceCount = 1000;

    // BRG 中每个矩阵使用 float3x4，而不是普通的 float4x4：12 个 float，共 48 字节。
    private const int PackedMatrixSize = 12 * sizeof(float);
    private const int ColorSize = 4 * sizeof(float);

    // Buffer 开头留 64 字节并保持为 0，作为未提供属性时的安全默认数据。
    private const int BufferHeaderSize = 64;

    // MetadataValue 最高位为 1，表示该属性是“每实例一份”的数组。
    private const uint PerInstanceData = 0x80000000;

    private BatchRendererGroup _brg;
    private GraphicsBuffer _instanceBuffer;
    private BatchID _batchId;
    private BatchMeshID _meshId;
    private BatchMaterialID _materialId;

    // 只有在 Inspector 没有指定资源时，才会创建并最终销毁这两个运行时资源。
    private Mesh _runtimeMesh;
    private Material _runtimeMaterial;
    private byte _gameObjectLayer;

    private void OnEnable()
    {
        // 不在编辑状态创建 GPU 资源，只在进入 Play Mode 后运行示例。
        if (!Application.isPlaying)
            return;

        columns = Mathf.Max(1, columns);
        spacing = Mathf.Max(0.01f, spacing);
        _gameObjectLayer = (byte)gameObject.layer;

        CreateFallbackResources();

        // 1. 创建 BRG，并提供剔除回调。Unity 每帧需要绘制时会调用 OnPerformCulling。
        _brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        // 2. 网格和材质必须先注册，绘制命令里使用的是注册后得到的 ID。
        _meshId = _brg.RegisterMesh(mesh);
        _materialId = _brg.RegisterMaterial(material);
        
        //这里都是静态资源  大概意思就是准备好所有的静态资源  塞到一个大缓冲区中  然后一次性上传到 GPU
        //这里还会提交MetaData  这个类似于VAO的 偏移标记 但更方便 直接通过属性名就可以访问
        CreateInstanceBufferAndBatch();
    }

    private void CreateInstanceBufferAndBatch()
    {
        // 本例采用 SoA（Structure of Arrays）布局：
        // [64 字节空白]
        // [1000 个 ObjectToWorld]
        // [1000 个 WorldToObject]
        // [1000 个 BaseColor]
        int objectToWorldOffset = BufferHeaderSize;//64 安全字节
        int worldToObjectOffset = objectToWorldOffset + InstanceCount * PackedMatrixSize;//PackedMatrixSize = 3*4 矩阵 48个字节
        int colorOffset = worldToObjectOffset + InstanceCount * PackedMatrixSize;
        int totalBufferSize = colorOffset + InstanceCount * ColorSize;

        // Raw GraphicsBuffer 的 stride 固定为 4 字节；Metadata 中记录的是“字节偏移”。
        _instanceBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Raw,
            totalBufferSize / sizeof(int),
            sizeof(int));
        _instanceBuffer.name = "Simple BRG - 1000 Cubes";

        // 先在 CPU 端组成一块连续数据，再一次上传到 GPU，代码和数据布局都更直观。
        var rawData = new int[totalBufferSize / sizeof(int)];
        int rows = Mathf.CeilToInt(InstanceCount / (float)columns);

        for (int i = 0; i < InstanceCount; i++)
        {
            int x = i % columns;
            int y = i / columns;

            // 让整个 X/Y 网格以当前 GameObject 为中心，所有盒子的 Z 坐标都为 0。
            Vector3 localPosition = new Vector3(
                (x - (columns - 1) * 0.5f) * spacing,
                (y - (rows - 1) * 0.5f) * spacing,
                0.0f);

            // 把进入 Play Mode 时宿主物体的变换也乘进去，方便摆放整片网格。
            Matrix4x4 objectToWorld = transform.localToWorldMatrix
                                      * Matrix4x4.TRS(localPosition, Quaternion.identity, Vector3.one);

            WritePackedMatrix(rawData, objectToWorldOffset / sizeof(int) + i * 12, objectToWorld);
            WritePackedMatrix(rawData, worldToObjectOffset / sizeof(int) + i * 12, objectToWorld.inverse);

            // 颜色不是 BRG 必需数据，只是用渐变色帮助观察 1000 个实例确实各自独立。
            Color color = Color.HSVToRGB(i / (float)InstanceCount, 0.65f, 1.0f);
            WriteFloat4(rawData, colorOffset / sizeof(int) + i * 4, color.r, color.g, color.b, 1.0f);
        }

        _instanceBuffer.SetData(rawData);//将 数据上传 GPU

        // 3. Metadata 把 Shader 属性名映射到 Raw Buffer 中对应数组的起始位置。
        // 注意：这里只保存起始字节地址，Shader 会根据当前实例 ID 找到自己的那一份数据。
        var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
        metadata[0] = CreateMetadata("unity_ObjectToWorld", objectToWorldOffset);
        metadata[1] = CreateMetadata("unity_WorldToObject", worldToObjectOffset);
        metadata[2] = CreateMetadata("_BaseColor", colorOffset);

        // 4. 一个 Batch 绑定“一块实例 Buffer + 一组 Metadata”。

        _batchId = _brg.AddBatch(metadata, _instanceBuffer.bufferHandle);
        metadata.Dispose();
    }

    private static MetadataValue CreateMetadata(string propertyName, int byteOffset)
    {
        return new MetadataValue
        {
            NameID = Shader.PropertyToID(propertyName),
            Value = PerInstanceData | (uint)byteOffset
        };
    }

    /// <summary>
    /// 把 Unity 的 4x4 矩阵按列压缩成 BRG Shader 所需的 3x4 格式。
    /// 最后一行恒为 (0, 0, 0, 1)，所以不需要上传。
    /// 这里是存一行  所以要一列列的存
    /// </summary>
    private static void WritePackedMatrix(int[] data, int index, Matrix4x4 m)
    {
        WriteFloat(data, index + 0, m.m00);
        WriteFloat(data, index + 1, m.m10);
        WriteFloat(data, index + 2, m.m20);
        WriteFloat(data, index + 3, m.m01);
        WriteFloat(data, index + 4, m.m11);
        WriteFloat(data, index + 5, m.m21);
        WriteFloat(data, index + 6, m.m02);
        WriteFloat(data, index + 7, m.m12);
        WriteFloat(data, index + 8, m.m22);
        WriteFloat(data, index + 9, m.m03);
        WriteFloat(data, index + 10, m.m13);
        WriteFloat(data, index + 11, m.m23);
    }

    private static void WriteFloat4(int[] data, int index, float x, float y, float z, float w)
    {
        WriteFloat(data, index + 0, x);
        WriteFloat(data, index + 1, y);
        WriteFloat(data, index + 2, z);
        WriteFloat(data, index + 3, w);
    }

    private static void WriteFloat(int[] data, int index, float value)
    {
        // Raw Buffer 用 int[] 承载原始位；这里保留 float 的二进制位，而不是做数值取整。
        data[index] = BitConverter.SingleToInt32Bits(value);
    }

    /// <summary>
    /// Unity 调用的剔除回调。为了保持示例最简单，这里不做视锥剔除，直接报告 1000 个实例都可见。
    /// </summary>
    private JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        var output = new BatchCullingOutputDrawCommands
        {
            drawCommandCount = 1,
            drawRangeCount = 1,
            visibleInstanceCount = InstanceCount,
            drawCommands = Allocate<BatchDrawCommand>(1),
            drawRanges = Allocate<BatchDrawRange>(1),
            visibleInstances = Allocate<int>(InstanceCount),
            drawCommandPickingInstanceIDs = null,
            instanceSortingPositions = null,
            instanceSortingPositionFloatCount = 0
        };

        // 5. 一条 DrawCommand 就可以让同一个 Mesh + Material 绘制 1000 个实例。
        output.drawCommands[0] = new BatchDrawCommand
        {
            visibleOffset = 0,
            visibleCount = InstanceCount,
            batchID = _batchId,
            materialID = _materialId,
            meshID = _meshId,
            submeshIndex = 0,
            splitVisibilityMask = 0xff,
            flags = BatchDrawCommandFlags.None,
            sortingPosition = 0
        };

        output.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsBegin = 0,
            drawCommandsCount = 1,
            filterSettings = new BatchFilterSettings
            {
                renderingLayerMask = uint.MaxValue,
                layer = _gameObjectLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                staticShadowCaster = false,
                allDepthSorted = false
            }
        };

        // visibleInstances 存的是“可见实例索引”。实际项目通常在这里写入视锥剔除后的结果。
        for (int i = 0; i < InstanceCount; i++)
            output.visibleInstances[i] = i;

        cullingOutput.drawCommands[0] = output;

        // 本例没有调度 Job，所以返回空 JobHandle。
        return default;
    }

    /// <summary>
    /// BRG 要求回调输出数组使用 Allocator.TempJob 分配；渲染完成后 Unity 会负责释放。
    /// </summary>
    private static T* Allocate<T>(int count) where T : unmanaged
    {
        return (T*)UnsafeUtility.Malloc(
            UnsafeUtility.SizeOf<T>() * count,
            UnsafeUtility.AlignOf<T>(),
            Allocator.TempJob);
    }

    private void CreateFallbackResources()
    {
        if (mesh == null)
        {
            // 利用 Unity 内置 Primitive 取得标准 Cube 网格，然后立刻删除临时 GameObject。
            GameObject temporaryCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temporaryCube.SetActive(false);
            _runtimeMesh = Instantiate(temporaryCube.GetComponent<MeshFilter>().sharedMesh);
            _runtimeMesh.name = "Simple BRG Runtime Cube";
            Destroy(temporaryCube);
            mesh = _runtimeMesh;
        }

        if (material == null)
        {
            Shader shader = Shader.Find("Learning/Simple BRG Unlit");
            if (shader == null)
                throw new InvalidOperationException(
                    "找不到 Shader 'Learning/Simple BRG Unlit'，请确认 SimpleBRGExample.shader 已导入。");

            _runtimeMaterial = new Material(shader) { name = "Simple BRG Runtime Material" };
            material = _runtimeMaterial;
        }
    }

    private void OnDisable()
    {
        // GraphicsBuffer 和 BatchRendererGroup 都是原生资源，必须手动 Dispose，避免显存泄漏。
        _brg?.Dispose();
        _brg = null;
        
        _instanceBuffer?.Dispose();
        _instanceBuffer = null;
        
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
        if (_runtimeMesh != null)
            Destroy(_runtimeMesh);
        
        _runtimeMaterial = null;
        _runtimeMesh = null;
    }
}
