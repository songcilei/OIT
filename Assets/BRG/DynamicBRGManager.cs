using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 动态、多模型 BatchRendererGroup 管理器。
///
/// 核心结构：
/// 一个 DynamicBRGManager
///   -> 一个 BatchRendererGroup
///   -> 多个 RenderGroup（按 Mesh + Material + SubMesh 分类）
///   -> 每个 RenderGroup 拥有多个固定容量 Chunk
///   -> 每个 Chunk 拥有一个 GraphicsBuffer 和一个 BatchID
///
/// Chunk 满后只创建新 Chunk，不搬迁旧实例、不重建旧 GraphicsBuffer。
/// </summary>
public unsafe sealed class DynamicBRGManager : MonoBehaviour
{
    [Header("分块设置")]
    [Tooltip("每个 Chunk 可以存放的最大实例数。满了以后自动创建另一个 Chunk。")]
    [Min(1)] public int chunkCapacity = 256;

    [Tooltip("BRG 整体 Bounds。它应覆盖所有可能生成实例的区域。")]
    public Bounds globalBounds = new Bounds(Vector3.zero, Vector3.one * 10000.0f);

    private const int BufferHeaderSize = 64;
    private const int PackedMatrixIntCount = 12;
    private const int PackedMatrixByteSize = 12 * sizeof(float);
    private const uint PerInstanceData = 0x80000000;

    private BatchRendererGroup _brg;
    private int _nextChunkId = 1;

    // Unity Object.GetInstanceID + SubMesh 可以作为运行期分组键。
    private readonly Dictionary<RenderGroupKey, RenderGroup> _groups =
        new Dictionary<RenderGroupKey, RenderGroup>();

    // 平铺 Chunk 列表，让剔除回调可以顺序生成 DrawCommand。
    private readonly List<RenderChunk> _chunks = new List<RenderChunk>();
    private readonly Dictionary<int, RenderChunk> _chunksById =
        new Dictionary<int, RenderChunk>();

    // 避免同一个 Mesh/Material 重复调用 Register。
    private readonly Dictionary<int, BatchMeshID> _registeredMeshes =
        new Dictionary<int, BatchMeshID>();
    private readonly Dictionary<int, BatchMaterialID> _registeredMaterials =
        new Dictionary<int, BatchMaterialID>();

    private void OnEnable()
    {
        chunkCapacity = Mathf.Max(1, chunkCapacity);
        _brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        // Unity 会先用这个 Bounds 判断整个 BRG 是否可能可见；实例级剔除在回调内完成。
        _brg.SetGlobalBounds(globalBounds);
    }

    /// <summary>
    /// 注册一个实例。返回的句柄用于修改 Transform 或注销实例。
    /// Material 的 Shader 必须支持 DOTS Instancing。
    /// </summary>
    public BRGInstanceHandle RegisterInstance(
        Mesh mesh,
        Material material,
        Matrix4x4 objectToWorld,
        int subMeshIndex = 0)
    {
        if (_brg == null)
            throw new InvalidOperationException("DynamicBRGManager 尚未启用。");
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));
        if (material == null)
            throw new ArgumentNullException(nameof(material));
        if (subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
            throw new ArgumentOutOfRangeException(nameof(subMeshIndex));

        RenderGroupKey key = new RenderGroupKey(
            mesh.GetInstanceID(), material.GetInstanceID(), subMeshIndex);

        if (!_groups.TryGetValue(key, out RenderGroup group))
        {
            group = CreateRenderGroup(key, mesh, material, subMeshIndex);
            _groups.Add(key, group);
        }

        RenderChunk chunk = group.FindChunkWithFreeSlot();
        if (chunk == null)
            chunk = CreateChunk(group);

        int slot = chunk.AllocateSlot();
        chunk.WriteTransform(slot, objectToWorld);

        // 单个动态注册时只上传这个槽位的矩阵，不重传整个 Chunk。
        chunk.UploadSlot(slot);

        return new BRGInstanceHandle(
            GetInstanceID(), chunk.Id, slot, chunk.SlotVersions[slot]);
    }

    /// <summary>修改一个实例的世界矩阵，并局部上传该槽位。</summary>
    public bool SetInstanceTransform(BRGInstanceHandle handle, Matrix4x4 objectToWorld)
    {
        if (!TryGetValidChunk(handle, out RenderChunk chunk))
            return false;

        chunk.WriteTransform(handle.Slot, objectToWorld);
        chunk.UploadSlot(handle.Slot);
        return true;
    }

    /// <summary>
    /// 注销实例只会释放槽位，不会重建 GraphicsBuffer。
    /// 剔除回调不再把这个槽位写入 visibleInstances，因此 GPU 不会绘制它。
    /// </summary>
    public bool UnregisterInstance(BRGInstanceHandle handle)
    {
        if (!TryGetValidChunk(handle, out RenderChunk chunk))
            return false;

        chunk.ReleaseSlot(handle.Slot);
        return true;
    }

    public bool IsHandleValid(BRGInstanceHandle handle)
    {
        return TryGetValidChunk(handle, out _);
    }

    private bool TryGetValidChunk(BRGInstanceHandle handle, out RenderChunk chunk)
    {
        chunk = null;
        if (handle.ManagerId != GetInstanceID())
            return false;
        if (!_chunksById.TryGetValue(handle.ChunkId, out chunk))
            return false;

        return handle.Slot >= 0
            && handle.Slot < chunk.Capacity
            && chunk.Alive[handle.Slot]
            && chunk.SlotVersions[handle.Slot] == handle.Version;
    }

    private RenderGroup CreateRenderGroup(
        RenderGroupKey key,
        Mesh mesh,
        Material material,
        int subMeshIndex)
    {
        BatchMeshID meshId = GetOrRegisterMesh(mesh);
        BatchMaterialID materialId = GetOrRegisterMaterial(material);
        return new RenderGroup(key, mesh, material, meshId, materialId, subMeshIndex);
    }

    private RenderChunk CreateChunk(RenderGroup group)
    {
        var chunk = new RenderChunk(this, _nextChunkId++, group, chunkCapacity);
        group.Chunks.Add(chunk);
        _chunks.Add(chunk);
        _chunksById.Add(chunk.Id, chunk);
        return chunk;
    }

    private BatchMeshID GetOrRegisterMesh(Mesh mesh)
    {
        int key = mesh.GetInstanceID();
        if (!_registeredMeshes.TryGetValue(key, out BatchMeshID id))
        {
            id = _brg.RegisterMesh(mesh);
            _registeredMeshes.Add(key, id);
        }
        return id;
    }

    private BatchMaterialID GetOrRegisterMaterial(Material material)
    {
        int key = material.GetInstanceID();
        if (!_registeredMaterials.TryGetValue(key, out BatchMaterialID id))
        {
            id = _brg.RegisterMaterial(material);
            _registeredMaterials.Add(key, id);
        }
        return id;
    }

    /// <summary>
    /// DrawCommand 是逐次剔除回调生成的临时数据，不需要“注册”。
    /// 当前每个 Chunk 生成一条 DrawCommand。
    /// </summary>
    private JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        int commandCount = _chunks.Count;
        int maximumVisibleCount = 0;
        for (int i = 0; i < _chunks.Count; i++)
            maximumVisibleCount += _chunks[i].ActiveCount;

        var output = new BatchCullingOutputDrawCommands
        {
            drawCommandCount = commandCount,
            drawRangeCount = commandCount > 0 ? 1 : 0,
            visibleInstanceCount = 0,

            // Malloc 不会清零，因此下面必须初始化每一条真正使用的数据。
            drawCommands = Allocate<BatchDrawCommand>(Mathf.Max(1, commandCount)),
            drawRanges = Allocate<BatchDrawRange>(1),
            visibleInstances = Allocate<int>(Mathf.Max(1, maximumVisibleCount)),

            drawCommandPickingInstanceIDs = null,
            instanceSortingPositions = null,
            instanceSortingPositionFloatCount = 0
        };

        int visibleOffset = 0;

        for (int commandIndex = 0; commandIndex < _chunks.Count; commandIndex++)
        {
            RenderChunk chunk = _chunks[commandIndex];
            int chunkVisibleCount = 0;

            for (int slot = 0; slot < chunk.Capacity; slot++)
            {
                if (!chunk.Alive[slot])
                    continue;
                if (!IsVisible(chunk.WorldBounds[slot], cullingContext.cullingPlanes))
                    continue;

                // visibleInstances 中写的是 Batch 内部的实例槽位索引。
                output.visibleInstances[visibleOffset + chunkVisibleCount] = slot;
                chunkVisibleCount++;
            }

            output.drawCommands[commandIndex] = new BatchDrawCommand
            {
                visibleOffset = (uint)visibleOffset,
                visibleCount = (uint)chunkVisibleCount,
                batchID = chunk.BatchId,
                meshID = chunk.Group.MeshId,
                materialID = chunk.Group.MaterialId,
                submeshIndex = (ushort)chunk.Group.SubMeshIndex,
                splitVisibilityMask = 0xff,
                flags = BatchDrawCommandFlags.None,
                sortingPosition = 0
            };

            visibleOffset += chunkVisibleCount;
        }

        output.visibleInstanceCount = visibleOffset;

        if (commandCount > 0)
        {
            output.drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0,
                drawCommandsCount = (uint)commandCount,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = uint.MaxValue,
                    layer = (byte)gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };
        }

        cullingOutput.drawCommands[0] = output;
        return default;
    }

    private static bool IsVisible(Bounds bounds, NativeArray<Plane> planes)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int i = 0; i < planes.Length; i++)
        {
            Plane plane = planes[i];
            Vector3 normal = plane.normal;
            float projectedRadius = Mathf.Abs(normal.x) * extents.x
                                  + Mathf.Abs(normal.y) * extents.y
                                  + Mathf.Abs(normal.z) * extents.z;

            if (plane.GetDistanceToPoint(center) + projectedRadius < 0.0f)
                return false;
        }
        return true;
    }

    private static MetadataValue CreateMetadata(string propertyName, int byteOffset)
    {
        return new MetadataValue
        {
            NameID = Shader.PropertyToID(propertyName),
            Value = PerInstanceData | (uint)byteOffset
        };
    }

    private static void WritePackedMatrix(int[] data, int offset, Matrix4x4 matrix)
    {
        WriteFloat(data, offset + 0, matrix.m00);
        WriteFloat(data, offset + 1, matrix.m10);
        WriteFloat(data, offset + 2, matrix.m20);
        WriteFloat(data, offset + 3, matrix.m01);
        WriteFloat(data, offset + 4, matrix.m11);
        WriteFloat(data, offset + 5, matrix.m21);
        WriteFloat(data, offset + 6, matrix.m02);
        WriteFloat(data, offset + 7, matrix.m12);
        WriteFloat(data, offset + 8, matrix.m22);
        WriteFloat(data, offset + 9, matrix.m03);
        WriteFloat(data, offset + 10, matrix.m13);
        WriteFloat(data, offset + 11, matrix.m23);
    }

    private static void WriteFloat(int[] data, int index, float value)
    {
        data[index] = BitConverter.SingleToInt32Bits(value);
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 e = localBounds.extents;
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = new Vector3(
            Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
            Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
            Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);
        return new Bounds(center, extents * 2.0f);
    }

    private static T* Allocate<T>(int count) where T : unmanaged
    {
        return (T*)UnsafeUtility.Malloc(
            UnsafeUtility.SizeOf<T>() * count,
            UnsafeUtility.AlignOf<T>(),
            Allocator.TempJob);
    }

    private void OnDisable()
    {
        // 先 Dispose BRG，确保不再有剔除回调访问 Chunk，然后释放 Buffer。
        _brg?.Dispose();
        _brg = null;

        for (int i = 0; i < _chunks.Count; i++)
            _chunks[i].Dispose();

        _chunks.Clear();
        _chunksById.Clear();
        _groups.Clear();
        _registeredMeshes.Clear();
        _registeredMaterials.Clear();

    }

    private readonly struct RenderGroupKey : IEquatable<RenderGroupKey>
    {
        private readonly int _meshInstanceId;
        private readonly int _materialInstanceId;
        private readonly int _subMeshIndex;

        public RenderGroupKey(int meshInstanceId, int materialInstanceId, int subMeshIndex)
        {
            _meshInstanceId = meshInstanceId;
            _materialInstanceId = materialInstanceId;
            _subMeshIndex = subMeshIndex;
        }

        public bool Equals(RenderGroupKey other)
        {
            return _meshInstanceId == other._meshInstanceId
                && _materialInstanceId == other._materialInstanceId
                && _subMeshIndex == other._subMeshIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderGroupKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _meshInstanceId;
                hash = hash * 397 ^ _materialInstanceId;
                hash = hash * 397 ^ _subMeshIndex;
                return hash;
            }
        }
    }

    private sealed class RenderGroup
    {
        public readonly RenderGroupKey Key;
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly BatchMeshID MeshId;
        public readonly BatchMaterialID MaterialId;
        public readonly int SubMeshIndex;
        public readonly List<RenderChunk> Chunks = new List<RenderChunk>();

        public RenderGroup(
            RenderGroupKey key,
            Mesh mesh,
            Material material,
            BatchMeshID meshId,
            BatchMaterialID materialId,
            int subMeshIndex)
        {
            Key = key;
            Mesh = mesh;
            Material = material;
            MeshId = meshId;
            MaterialId = materialId;
            SubMeshIndex = subMeshIndex;
        }

        public RenderChunk FindChunkWithFreeSlot()
        {
            for (int i = 0; i < Chunks.Count; i++)
            {
                if (Chunks[i].HasFreeSlot)
                    return Chunks[i];
            }
            return null;
        }
    }

    private sealed class RenderChunk
    {
        private readonly DynamicBRGManager _owner;
        private readonly int[] _rawData;
        private readonly Stack<int> _freeSlots;
        private readonly int _objectToWorldStart;
        private readonly int _worldToObjectStart;

        public readonly int Id;
        public readonly RenderGroup Group;
        public readonly int Capacity;
        public readonly bool[] Alive;
        public readonly int[] SlotVersions;
        public readonly Bounds[] WorldBounds;
        public readonly GraphicsBuffer Buffer;
        public readonly BatchID BatchId;

        public int ActiveCount { get; private set; }
        public bool HasFreeSlot => _freeSlots.Count > 0;

        public RenderChunk(
            DynamicBRGManager owner,
            int id,
            RenderGroup group,
            int capacity)
        {
            _owner = owner;
            Id = id;
            Group = group;
            Capacity = capacity;

            _objectToWorldStart = BufferHeaderSize / sizeof(int);
            _worldToObjectStart = _objectToWorldStart + capacity * PackedMatrixIntCount;
            int totalIntCount = _worldToObjectStart + capacity * PackedMatrixIntCount;

            _rawData = new int[totalIntCount];
            Alive = new bool[capacity];
            SlotVersions = new int[capacity];
            WorldBounds = new Bounds[capacity];
            _freeSlots = new Stack<int>(capacity);

            // 反向压栈，使第一次 Pop 得到槽位 0，便于调试观察。
            for (int slot = capacity - 1; slot >= 0; slot--)
            {
                SlotVersions[slot] = 1;
                _freeSlots.Push(slot);
            }

            Buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                totalIntCount,
                sizeof(int));
            Buffer.name = $"Dynamic BRG Chunk {id}";

            // 初始化完整 Buffer，尤其确保前 64 字节安全区为 0。
            Buffer.SetData(_rawData);

            var metadata = new NativeArray<MetadataValue>(2, Allocator.Temp);
            metadata[0] = CreateMetadata("unity_ObjectToWorld", BufferHeaderSize);
            metadata[1] = CreateMetadata(
                "unity_WorldToObject",
                BufferHeaderSize + capacity * PackedMatrixByteSize);

            BatchId = owner._brg.AddBatch(metadata, Buffer.bufferHandle);
            metadata.Dispose();
        }

        public int AllocateSlot()
        {
            int slot = _freeSlots.Pop();
            Alive[slot] = true;
            ActiveCount++;
            return slot;
        }

        public void ReleaseSlot(int slot)
        {
            Alive[slot] = false;
            ActiveCount--;

            // version 变化后，旧句柄即使指向同一个 slot 也会失效。
            SlotVersions[slot]++;
            if (SlotVersions[slot] == 0)
                SlotVersions[slot] = 1;

            _freeSlots.Push(slot);
        }

        public void WriteTransform(int slot, Matrix4x4 objectToWorld)
        {
            WritePackedMatrix(
                _rawData,
                _objectToWorldStart + slot * PackedMatrixIntCount,
                objectToWorld);
            WritePackedMatrix(
                _rawData,
                _worldToObjectStart + slot * PackedMatrixIntCount,
                objectToWorld.inverse);

            WorldBounds[slot] = TransformBounds(Group.Mesh.bounds, objectToWorld);
        }

        public void UploadSlot(int slot)
        {
            int objectOffset = _objectToWorldStart + slot * PackedMatrixIntCount;
            int inverseOffset = _worldToObjectStart + slot * PackedMatrixIntCount;

            Buffer.SetData(
                _rawData, objectOffset, objectOffset, PackedMatrixIntCount);
            Buffer.SetData(
                _rawData, inverseOffset, inverseOffset, PackedMatrixIntCount);
        }

        public void Dispose()
        {
            // Dispose BRG 本身也会清理 Batch；这里仅释放我们自己创建的 Buffer。
            Buffer.Dispose();
        }
    }
}

/// <summary>
/// 外部持有的轻量实例句柄。它不包含对象引用，可安全存入组件或普通容器。
/// ManagerId + ChunkId + Slot 找到实例，Version 防止槽位复用后旧句柄误操作新实例。
/// </summary>
[Serializable]
public readonly struct BRGInstanceHandle
{
    public readonly int ManagerId;
    public readonly int ChunkId;
    public readonly int Slot;
    public readonly int Version;

    internal BRGInstanceHandle(int managerId, int chunkId, int slot, int version)
    {
        ManagerId = managerId;
        ChunkId = chunkId;
        Slot = slot;
        Version = version;
    }

    public bool IsCreated => ManagerId != 0 && ChunkId != 0;
}
