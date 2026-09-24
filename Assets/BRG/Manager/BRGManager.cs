using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class BatchBufferInfo
{
    public Mesh mesh;
    public Material material;
    public BatchMaterialID batchMaterialID;
    public BatchMeshID BatchMeshID;
    public BatchID BatchID;
    public int batchCount;
    public int object2worldOffset;
    public int world2objectOffset;
    public List<InstanceAttr> Attrs;
    
    public GraphicsBuffer buffer;
    public int[] rawData;

    public int maxCount
    {
        get
        {
            return Attrs.Count;
        }
    }

    public int AddElement(BrgObjInfo objInfo)
    {
        if (Attrs == null)
        {
            Attrs = new List<InstanceAttr>();
        }

        objInfo.attrIndex = maxCount;
        Attrs.Add(new InstanceAttr()
        {
            trans = objInfo.trans,
            index = maxCount
        });
        return maxCount;
    }

    public void removeElement(int index)
    {
        for (int i = 0; i < maxCount; i++)
        {
            if (index == Attrs[i].index)
            {
                Attrs.RemoveAt(i);
                return;
            }
        }
    }
}

public class InstanceAttr
{
    public Matrix4x4 object2World;
    public Transform trans;
    public int index;
}


public unsafe class BRGManager : MonoBehaviour
{
   
    public static BRGManager Instance;
    
    void Awake()
    {
        Instance = this;
        
        _bufferInfoList = new List<BatchBufferInfo>();
        
        _brg = new BatchRendererGroup(OnPerformCulling,IntPtr.Zero);
        
        _brg.SetGlobalBounds(new Bounds(Vector3.zero,Vector3.one*100000));
    }
    private BatchRendererGroup _brg;
    List<BatchBufferInfo> _bufferInfoList;
    private int maxCount = 256;
    private void Start()
    {

    }

    public void InitBuffer(BrgObjInfo objInfo)
    {
        //计算  数据
        int packedMatrix = sizeof(float) * 12;
        int object2WorldOffset = 64;
        int world2ObjectOffset = 64 + maxCount * packedMatrix;
        int totalOffset = world2ObjectOffset + maxCount * packedMatrix;

        //已经有了
        if (_bufferInfoList!=null)
        {
            
            for (int i = 0; i < _bufferInfoList.Count; i++)
            {
                //越界判断 最大支持256
                if (_bufferInfoList[i].maxCount >= maxCount)
                {
                    Debug.LogError("当前 Batch 已达到容量上限");
                    return;
                }
                if (objInfo.material == _bufferInfoList[i].material && objInfo.mesh == _bufferInfoList[i].mesh)
                {
                    objInfo._batchInfo = _bufferInfoList[i];
                    objInfo.bufferInfoIndex = i;
                    _bufferInfoList[i].AddElement(objInfo);
                    return;
                }
            }
        }

        
        //没有 创建新的
        
        BatchBufferInfo batchInfo = new BatchBufferInfo();
        batchInfo.rawData = new int[totalOffset / sizeof(int)];

        objInfo._batchInfo = batchInfo;

        batchInfo.mesh = objInfo.mesh;
        batchInfo.material = objInfo.material;
        batchInfo.object2worldOffset = object2WorldOffset;
        batchInfo.world2objectOffset = world2ObjectOffset;
        
        //batch 
        batchInfo.batchMaterialID = _brg.RegisterMaterial(batchInfo.material);
        batchInfo.BatchMeshID =  _brg.RegisterMesh(batchInfo.mesh);
        batchInfo.AddElement(objInfo);
        //创建buffer 相关 并绑定到 brg
        batchInfo.buffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, totalOffset / sizeof(int), sizeof(int));
 
        //创建MetaData
        var metaData = new NativeArray<MetadataValue>(2, Allocator.Temp);
        metaData[0] = CreateMetadata("unity_ObjectToWorld", object2WorldOffset);
        metaData[1] = CreateMetadata("unity_WorldToObject", world2ObjectOffset);
            
        batchInfo.buffer.SetData(batchInfo.rawData);
        batchInfo.BatchID = _brg.AddBatch(metaData, batchInfo.buffer.bufferHandle);//绑定到brg
        _bufferInfoList.Add(batchInfo);
        metaData.Dispose();
    }

    private void Update()
    {
        //更新

        for (int i = 0; i < _bufferInfoList.Count; i++)
        {
            for (int j = 0; j < _bufferInfoList[i].maxCount; j++)
            {
                //如果transform没有改变 则不进行重新赋值上传
                if (!_bufferInfoList[i].Attrs[j].trans.hasChanged)
                {
                    continue;
                }
                
                Matrix4x4 matrix = _bufferInfoList[i].Attrs[j].trans.localToWorldMatrix;
                int offset = j *12+_bufferInfoList[i].object2worldOffset/sizeof(int) ;
                ZwriteMatrix2Raw(_bufferInfoList[i].rawData,matrix,offset);
                
                
                matrix = _bufferInfoList[i].Attrs[j].trans.worldToLocalMatrix;
                offset = j *12+_bufferInfoList[i].world2objectOffset/sizeof(int) ;
                ZwriteMatrix2Raw(_bufferInfoList[i].rawData,matrix,offset);
            }
            _bufferInfoList[i].buffer.SetData(_bufferInfoList[i].rawData);
        }
    }

    private void ZwriteMatrix2Raw(int[] rawData,Matrix4x4 matrix,int offset)
    {
        rawData[offset + 0] = BitConverter.SingleToInt32Bits(matrix.m00);
        rawData[offset + 1] = BitConverter.SingleToInt32Bits(matrix.m10);
        rawData[offset + 2] = BitConverter.SingleToInt32Bits(matrix.m20);

        rawData[offset + 3] = BitConverter.SingleToInt32Bits(matrix.m01);
        rawData[offset + 4] = BitConverter.SingleToInt32Bits(matrix.m11);
        rawData[offset + 5] = BitConverter.SingleToInt32Bits(matrix.m21);

        rawData[offset + 6] = BitConverter.SingleToInt32Bits(matrix.m02);
        rawData[offset + 7] = BitConverter.SingleToInt32Bits(matrix.m12);
        rawData[offset + 8] = BitConverter.SingleToInt32Bits(matrix.m22);

        rawData[offset + 9] = BitConverter.SingleToInt32Bits(matrix.m03);
        rawData[offset + 10] = BitConverter.SingleToInt32Bits(matrix.m13);
        rawData[offset + 11] = BitConverter.SingleToInt32Bits(matrix.m23);
    }


    // MetadataValue 最高位为 1，表示该属性是“每实例一份”的数组。
    private const uint PerInstanceData = 0x80000000;
    private static MetadataValue CreateMetadata(string propertyName, int byteOffset)
    {
        return new MetadataValue
        {
            NameID = Shader.PropertyToID(propertyName),
            Value = PerInstanceData | (uint)byteOffset
        };
    }

    private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext
        )
    {
        if (_bufferInfoList.Count == 0)
        {
            return default;
        }
        
        int totalCount = 0;

        for (int i = 0; i < _bufferInfoList.Count; i++)
        {
            totalCount += _bufferInfoList[i].maxCount;
        }

        int commandCount = _bufferInfoList.Count;
        var output = new BatchCullingOutputDrawCommands
        {
            drawCommandCount = commandCount,
            drawRangeCount = 1,
            visibleInstanceCount = totalCount,
            drawCommands =  Allocate<BatchDrawCommand>(commandCount),
            drawRanges = Allocate<BatchDrawRange>(1),
            visibleInstances = Allocate<int>(totalCount),
            drawCommandPickingInstanceIDs = null,
            instanceSortingPositions = null,
            instanceSortingPositionFloatCount = 0
        };

        output.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsBegin = 0,
            drawCommandsCount = (uint)commandCount,
            filterSettings = new BatchFilterSettings()
            {
                renderingLayerMask = uint.MaxValue,
                layer = (byte)gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                staticShadowCaster = false,
                allDepthSorted = false
            }
        };
        
        int visibleOffset = 0;
        
        for (int i = 0; i < _bufferInfoList.Count; i++)
        {
            output.drawCommands[i] = new BatchDrawCommand
            {
                visibleOffset = (uint)visibleOffset,
                visibleCount = (uint)_bufferInfoList[i].maxCount,
                batchID = _bufferInfoList[i].BatchID,
                materialID = _bufferInfoList[i].batchMaterialID,
                meshID = _bufferInfoList[i].BatchMeshID,
                submeshIndex = 0,
                splitVisibilityMask = 0xff,
                flags = BatchDrawCommandFlags.None,
                sortingPosition = 0
            };
            
            //这里其实是要写剔除的主函数
            for (int j = 0; j < _bufferInfoList[i].maxCount; j++)
            {
                output.visibleInstances[j+visibleOffset] = j;
            }

            visibleOffset += _bufferInfoList[i].maxCount;
        }
        cullingOutput.drawCommands[0] = output;


        //本列没有调度Job 所以返回空JobHandle
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
            Allocator.TempJob
        );
    }

    public void Remove(int bufferIndex,int AttrIndex)
    {
        _bufferInfoList[bufferIndex].removeElement(AttrIndex);
    }

    private void OnDisable()
    {
        _brg?.Dispose();
        foreach (var bufferInfo in _bufferInfoList)
        {
            bufferInfo.buffer.Dispose();
        }
        _bufferInfoList.Clear();
    }
    
    
    private static bool IsVisible(
        Bounds bounds,
        NativeArray<Plane> planes)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int i = 0; i < planes.Length; i++)
        {
            Plane plane = planes[i];
            Vector3 normal = plane.normal;

            float projectedRadius =
                Mathf.Abs(normal.x) * extents.x +
                Mathf.Abs(normal.y) * extents.y +
                Mathf.Abs(normal.z) * extents.z;

            if (plane.GetDistanceToPoint(center) + projectedRadius < 0.0f)
            {
                return false;
            }
        }

        return true;
    }
}
