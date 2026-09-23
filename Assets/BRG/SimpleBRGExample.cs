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

    [Header("移动设置")]
    [Tooltip("每次移动时，每个 Cube 被随机选中的概率。")]
    [Range(0.0f, 1.0f)] public float moveProbability = 0.25f;

    [Tooltip("每次被选中的 Cube 朝相机移动的距离。")]
    [Min(0.0f)] public float moveDistance = 0.1f;

    [Tooltip("移动所朝向的相机；不指定时使用 Main Camera。")]
    public Camera targetCamera;

    [Header("Sphere 设置")]
    [Tooltip("每 60 帧，每个 Sphere 被销毁并在新位置生成的概率。")]
    [Range(0.0f, 1.0f)] public float sphereRespawnProbability = 0.25f;

    [Tooltip("不指定时会自动使用 Unity 内置 Sphere 网格。")]
    public Mesh sphereMesh;

    [Tooltip("Sphere Buffer 的预留容量；按钮会在此容量内增加 Sphere。")]
    [Min(1)] public int sphereCapacity = 256;

    [Tooltip("进入 Play Mode 时先创建的 Sphere 数量。")]
    [Min(0)] public int initialSphereCount = 100;

    [Header("可选资源（不填会自动创建）")]
    [Tooltip("不指定时会自动使用 Unity 内置 Cube 网格。")]
    public Mesh mesh;

    [Tooltip("自定义材质的 Shader 必须支持 DOTS Instancing；不指定时会自动创建配套材质。")]
    public Material material;

    // 本例固定绘制 1000 个方盒子，便于把注意力放在 BRG 的核心流程上。
    private const int InstanceCount = 1000;
    private const float SpherePositionMax = 100.0f;

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
    private BatchID _sphereBatchId;
    private BatchMeshID _meshId;
    private BatchMeshID _sphereMeshId;
    private BatchMaterialID _materialId;

    // 只有在 Inspector 没有指定资源时，才会创建并最终销毁这两个运行时资源。
    private Mesh _runtimeMesh;
    private Mesh _runtimeSphereMesh;
    private Material _runtimeMaterial;
    private byte _gameObjectLayer;
    private int[] _rawData;
    private Matrix4x4[] _objectToWorldMatrices;
    private NativeArray<Bounds> _worldBounds;
    private GraphicsBuffer _sphereInstanceBuffer;
    private int[] _sphereRawData;
    private NativeArray<Bounds> _sphereWorldBounds;
    private int _sphereActiveCount;
    private int _framesSinceMove;
    private int _framesSinceSphereRespawn;

    private void OnEnable()
    {
        // 不在编辑状态创建 GPU 资源，只在进入 Play Mode 后运行示例。
        if (!Application.isPlaying)
            return;

        columns = Mathf.Max(1, columns);
        spacing = Mathf.Max(0.01f, spacing);
        moveProbability = Mathf.Clamp01(moveProbability);
        moveDistance = Mathf.Max(0.0f, moveDistance);
        sphereRespawnProbability = Mathf.Clamp01(sphereRespawnProbability);
        sphereCapacity = Mathf.Max(1, sphereCapacity);
        initialSphereCount = Mathf.Clamp(initialSphereCount, 0, sphereCapacity);
        _gameObjectLayer = (byte)gameObject.layer;

        CreateFallbackResources();

        // 1. 创建 BRG，并提供剔除回调。Unity 每帧需要绘制时会调用 OnPerformCulling。
        _brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        // 2. 网格和材质必须先注册，绘制命令里使用的是注册后得到的 ID。
        _meshId = _brg.RegisterMesh(mesh);
        _sphereMeshId = _brg.RegisterMesh(sphereMesh);
        _materialId = _brg.RegisterMaterial(material);
        
        //这里都是静态资源  大概意思就是准备好所有的静态资源  塞到一个大缓冲区中  然后一次性上传到 GPU
        //这里还会提交MetaData  这个类似于VAO的 偏移标记 但更方便 直接通过属性名就可以访问
        CreateInstanceBufferAndBatch();
        CreateSphereBufferAndBatch();
    }

    private void CreateSphereBufferAndBatch()
    {
        int objectToWorldOffset = BufferHeaderSize;
        int worldToObjectOffset = objectToWorldOffset + sphereCapacity * PackedMatrixSize;
        int colorOffset = worldToObjectOffset + sphereCapacity * PackedMatrixSize;
        int totalBufferSize = colorOffset + sphereCapacity * ColorSize;

        _sphereInstanceBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Raw,
            totalBufferSize / sizeof(int),
            sizeof(int));
        _sphereInstanceBuffer.name = $"Simple BRG - Sphere Capacity {sphereCapacity}";

        _sphereRawData = new int[totalBufferSize / sizeof(int)];
        _sphereWorldBounds = new NativeArray<Bounds>(sphereCapacity, Allocator.Persistent);
        _sphereActiveCount = initialSphereCount;

        for (int i = 0; i < _sphereActiveCount; i++)
        {
            SetSphereTransform(i, Matrix4x4.TRS(RandomSpherePosition(), Quaternion.identity, Vector3.one));
            Color color = Color.HSVToRGB(i / (float)sphereCapacity, 0.35f, 1.0f);
            WriteFloat4(_sphereRawData, colorOffset / sizeof(int) + i * 4, color.r, color.g, color.b, 1.0f);
        }

        _sphereInstanceBuffer.SetData(_sphereRawData);

        var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
        metadata[0] = CreateMetadata("unity_ObjectToWorld", objectToWorldOffset);
        metadata[1] = CreateMetadata("unity_WorldToObject", worldToObjectOffset);
        metadata[2] = CreateMetadata("_BaseColor", colorOffset);
        _sphereBatchId = _brg.AddBatch(metadata, _sphereInstanceBuffer.bufferHandle);
        metadata.Dispose();
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
        _rawData = new int[totalBufferSize / sizeof(int)];
        _objectToWorldMatrices = new Matrix4x4[InstanceCount];
        _worldBounds = new NativeArray<Bounds>(InstanceCount, Allocator.Persistent);
        InitializeInstanceTransforms();

        for (int i = 0; i < InstanceCount; i++)
        {
            // 颜色不是 BRG 必需数据，只是用渐变色帮助观察 1000 个实例确实各自独立。
            Color color = Color.HSVToRGB(i / (float)InstanceCount, 0.65f, 1.0f);
            WriteFloat4(_rawData, colorOffset / sizeof(int) + i * 4, color.r, color.g, color.b, 1.0f);
        }

        _instanceBuffer.SetData(_rawData);//将 数据上传 GPU

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

    private void Update()
    {
        if (_instanceBuffer == null || _sphereInstanceBuffer == null)
            return;

        _framesSinceMove++;
        _framesSinceSphereRespawn++;

        if (_framesSinceMove >= 30)
        {
            _framesSinceMove = 0;
            Camera movementCamera = targetCamera != null ? targetCamera : Camera.main;
            if (movementCamera != null)
            {
                MoveRandomInstancesToward(movementCamera.transform.position);
                UploadMatrixData(_instanceBuffer, _rawData, InstanceCount);
            }
        }

        if (_framesSinceSphereRespawn >= 60)
        {
            _framesSinceSphereRespawn = 0;
            RespawnRandomSpheres();
            UploadMatrixData(_sphereInstanceBuffer, _sphereRawData, sphereCapacity);
        }
    }

    /// <summary>
    /// 每次点击按钮时启用一个预留 Sphere 槽位。
    /// 这里不重建 GraphicsBuffer、Batch，也不重新注册 Mesh/Material。
    /// </summary>
    private void AddOneSphere()
    {
        if (_sphereInstanceBuffer == null || _sphereActiveCount >= sphereCapacity)
            return;

        int newIndex = _sphereActiveCount++;
        SetSphereTransform(newIndex, Matrix4x4.TRS(RandomSpherePosition(), Quaternion.identity, Vector3.one));

        int colorOffset = BufferHeaderSize / sizeof(int) + sphereCapacity * PackedMatrixSize * 2 / sizeof(int);
        Color color = Color.HSVToRGB(newIndex / (float)sphereCapacity, 0.35f, 1.0f);
        WriteFloat4(_sphereRawData, colorOffset + newIndex * 4, color.r, color.g, color.b, 1.0f);
        UploadSphereSlot(newIndex);
    }

    private void UploadSphereSlot(int index)
    {
        int objectStart = BufferHeaderSize / sizeof(int) + index * 12;
        int worldStart = BufferHeaderSize / sizeof(int) + sphereCapacity * 12 + index * 12;
        int colorStart = BufferHeaderSize / sizeof(int) + sphereCapacity * 24 + index * 4;

        _sphereInstanceBuffer.SetData(_sphereRawData, objectStart, objectStart, 12);
        _sphereInstanceBuffer.SetData(_sphereRawData, worldStart, worldStart, 12);
        _sphereInstanceBuffer.SetData(_sphereRawData, colorStart, colorStart, 4);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || _sphereInstanceBuffer == null)
            return;

        GUILayout.BeginArea(new Rect(20.0f, 20.0f, 260.0f, 90.0f), GUI.skin.box);
        GUILayout.Label($"Sphere: {_sphereActiveCount} / {sphereCapacity}");
        GUI.enabled = _sphereActiveCount < sphereCapacity;
        if (GUILayout.Button("Add one Sphere"))
            AddOneSphere();
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private static void UploadMatrixData(GraphicsBuffer buffer, int[] rawData, int instanceCount)
    {
        // 只重新上传两个矩阵数组，颜色数据保持不变。
        int matrixIntCount = instanceCount * PackedMatrixSize / sizeof(int);
        int objectToWorldStart = BufferHeaderSize / sizeof(int);
        int worldToObjectStart = objectToWorldStart + matrixIntCount;
        buffer.SetData(rawData, objectToWorldStart, objectToWorldStart, matrixIntCount);
        buffer.SetData(rawData, worldToObjectStart, worldToObjectStart, matrixIntCount);
    }

    private void InitializeInstanceTransforms()
    {
        int rows = Mathf.CeilToInt(InstanceCount / (float)columns);

        for (int i = 0; i < InstanceCount; i++)
        {
            int x = i % columns;
            int y = i / columns;

            Vector3 localPosition = new Vector3(
                (x - (columns - 1) * 0.5f) * spacing,
                (y - (rows - 1) * 0.5f) * spacing,
                0.0f);
            Matrix4x4 objectToWorld = transform.localToWorldMatrix
                                      * Matrix4x4.TRS(localPosition, Quaternion.identity, Vector3.one);
            SetInstanceTransform(i, objectToWorld);
        }
    }

    private void MoveRandomInstancesToward(Vector3 targetPosition)
    {
        for (int i = 0; i < InstanceCount; i++)
        {
            if (UnityEngine.Random.value > moveProbability)
                continue;

            Matrix4x4 objectToWorld = _objectToWorldMatrices[i];
            Vector3 position = objectToWorld.GetColumn(3);
            Vector3 direction = targetPosition - position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                continue;

            Vector3 movement = direction.normalized * moveDistance;
            objectToWorld.m03 += movement.x;
            objectToWorld.m13 += movement.y;
            objectToWorld.m23 += movement.z;
            SetInstanceTransform(i, objectToWorld);
        }
    }

    private void SetInstanceTransform(int instanceIndex, Matrix4x4 objectToWorld)
    {
        int objectToWorldOffset = BufferHeaderSize / sizeof(int);
        int worldToObjectOffset = objectToWorldOffset + InstanceCount * 12;

        _objectToWorldMatrices[instanceIndex] = objectToWorld;
        WritePackedMatrix(_rawData, objectToWorldOffset + instanceIndex * 12, objectToWorld);
        WritePackedMatrix(_rawData, worldToObjectOffset + instanceIndex * 12, objectToWorld.inverse);

        _worldBounds[instanceIndex] = TransformBounds(mesh.bounds, objectToWorld);
    }

    private void RespawnRandomSpheres()
    {
        for (int i = 0; i < _sphereActiveCount; i++)
        {
            if (UnityEngine.Random.value > sphereRespawnProbability)
                continue;

            // BRG 实例不是 GameObject；覆盖这个槽位的矩阵就等价于销毁旧实例并在新位置创建。
            SetSphereTransform(i, Matrix4x4.TRS(RandomSpherePosition(), Quaternion.identity, Vector3.one));
        }
    }

    private static Vector3 RandomSpherePosition()
    {
        return new Vector3(
            UnityEngine.Random.Range(0.0f, SpherePositionMax),
            UnityEngine.Random.Range(0.0f, SpherePositionMax),
            UnityEngine.Random.Range(0.0f, SpherePositionMax));
    }

    private void SetSphereTransform(int instanceIndex, Matrix4x4 objectToWorld)
    {
        int objectToWorldOffset = BufferHeaderSize / sizeof(int);
        int worldToObjectOffset = objectToWorldOffset + sphereCapacity * 12;

        WritePackedMatrix(_sphereRawData, objectToWorldOffset + instanceIndex * 12, objectToWorld);
        WritePackedMatrix(_sphereRawData, worldToObjectOffset + instanceIndex * 12, objectToWorld.inverse);
        _sphereWorldBounds[instanceIndex] = TransformBounds(sphereMesh.bounds, objectToWorld);
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 objectToWorld)
    {
        Vector3 localExtents = localBounds.extents;
        Vector3 worldCenter = objectToWorld.MultiplyPoint3x4(localBounds.center);
        Vector3 worldExtents = new Vector3(
            Mathf.Abs(objectToWorld.m00) * localExtents.x
            + Mathf.Abs(objectToWorld.m01) * localExtents.y
            + Mathf.Abs(objectToWorld.m02) * localExtents.z,
            Mathf.Abs(objectToWorld.m10) * localExtents.x
            + Mathf.Abs(objectToWorld.m11) * localExtents.y
            + Mathf.Abs(objectToWorld.m12) * localExtents.z,
            Mathf.Abs(objectToWorld.m20) * localExtents.x
            + Mathf.Abs(objectToWorld.m21) * localExtents.y
            + Mathf.Abs(objectToWorld.m22) * localExtents.z);
        return new Bounds(worldCenter, worldExtents * 2.0f);
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
    /// Unity 调用的剔除回调，只把与当前相机视锥相交的实例报告为可见。
    /// </summary>
    private JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        var output = new BatchCullingOutputDrawCommands
        {
            drawCommandCount = 2,
            drawRangeCount = 1,
            visibleInstanceCount = 0,
            drawCommands = Allocate<BatchDrawCommand>(2),
            drawRanges = Allocate<BatchDrawRange>(1),
            visibleInstances = Allocate<int>(InstanceCount + _sphereActiveCount),
            drawCommandPickingInstanceIDs = null,
            instanceSortingPositions = null,
            instanceSortingPositionFloatCount = 0
        };

        // Cube 和 Sphere 使用不同 Mesh / Batch，所以各自需要一条 DrawCommand。
        output.drawCommands[0] = new BatchDrawCommand
        {
            visibleOffset = 0,
            visibleCount = 0,
            batchID = _batchId,
            materialID = _materialId,
            meshID = _meshId,
            submeshIndex = 0,
            splitVisibilityMask = 0xff,
            flags = BatchDrawCommandFlags.None,
            sortingPosition = 0
        };

        output.drawCommands[1] = new BatchDrawCommand
        {
            visibleOffset = 0,
            visibleCount = 0,
            batchID = _sphereBatchId,
            materialID = _materialId,
            meshID = _sphereMeshId,
            submeshIndex = 0,
            splitVisibilityMask = 0xff,
            flags = BatchDrawCommandFlags.None,
            sortingPosition = 0
        };

        output.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsBegin = 0,
            drawCommandsCount = 2,
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

        // visibleInstances 只写入通过所有视锥平面测试的实例索引。
        int cubeVisibleCount = 0;
        for (int i = 0; i < InstanceCount; i++)
        {
            if (IsVisible(_worldBounds[i], cullingContext.cullingPlanes))
                output.visibleInstances[cubeVisibleCount++] = i;
        }

        int sphereVisibleOffset = cubeVisibleCount;
        int sphereVisibleCount = 0;
        for (int i = 0; i < _sphereActiveCount; i++)
        {
            if (IsVisible(_sphereWorldBounds[i], cullingContext.cullingPlanes))
                output.visibleInstances[sphereVisibleOffset + sphereVisibleCount++] = i;
        }

        output.visibleInstanceCount = cubeVisibleCount + sphereVisibleCount;
        output.drawCommands[0].visibleCount = (uint)cubeVisibleCount;
        output.drawCommands[1].visibleOffset = (uint)sphereVisibleOffset;
        output.drawCommands[1].visibleCount = (uint)sphereVisibleCount;

        cullingOutput.drawCommands[0] = output;

        // 本例没有调度 Job，所以返回空 JobHandle。
        return default;
    }

    private static bool IsVisible(Bounds bounds, NativeArray<Plane> cullingPlanes)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int i = 0; i < cullingPlanes.Length; i++)
        {
            Plane plane = cullingPlanes[i];
            Vector3 normal = plane.normal;
            float projectedRadius = Mathf.Abs(normal.x) * extents.x
                                    + Mathf.Abs(normal.y) * extents.y
                                    + Mathf.Abs(normal.z) * extents.z;

            // 包围盒完全位于任一平面的外侧时，即可立即剔除。
            if (plane.GetDistanceToPoint(center) + projectedRadius < 0.0f)
                return false;
        }

        return true;
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

        if (sphereMesh == null)
        {
            GameObject temporarySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temporarySphere.SetActive(false);
            _runtimeSphereMesh = Instantiate(temporarySphere.GetComponent<MeshFilter>().sharedMesh);
            _runtimeSphereMesh.name = "Simple BRG Runtime Sphere";
            Destroy(temporarySphere);
            sphereMesh = _runtimeSphereMesh;
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
        _sphereInstanceBuffer?.Dispose();
        _sphereInstanceBuffer = null;
        _rawData = null;
        _sphereRawData = null;
        _sphereActiveCount = 0;
        _objectToWorldMatrices = null;
        if (_worldBounds.IsCreated)
            _worldBounds.Dispose();
        if (_sphereWorldBounds.IsCreated)
            _sphereWorldBounds.Dispose();
        _framesSinceMove = 0;
        _framesSinceSphereRespawn = 0;
        
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
        if (_runtimeMesh != null)
            Destroy(_runtimeMesh);
        if (_runtimeSphereMesh != null)
            Destroy(_runtimeSphereMesh);
        
        _runtimeMaterial = null;
        _runtimeMesh = null;
        _runtimeSphereMesh = null;
    }
}
