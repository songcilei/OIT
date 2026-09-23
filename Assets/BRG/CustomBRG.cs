using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public unsafe class CustomBRG : MonoBehaviour
{
    private Material mat;
    private Mesh mesh;
    private BatchRendererGroup _brg;
    private GraphicsBuffer _instanceBuffer;
    private BatchMaterialID _matId;
    private BatchMeshID _meshId;
    private BatchID _batchID;

    private int[] raw;
//这边算的是多少个 bytes  所以需要× sizeof(float)
    private const int BufferHeanderSize = 64;
    private int PackedMatrixSize = 12 * sizeof(float);
    public int InstanceCount = 100;
    // MetadataValue 最高位为 1，表示该属性是“每实例一份”的数组。
    private const uint PerInstanceData = 0x80000000;
    
    //用来缓存matrix 数量的数组
    private Matrix4x4[] objet2WorldMatrixs;
    
    
    void Start()
    {

        CreateObjct();
        _brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        _matId = _brg.RegisterMaterial(mat);
        _meshId = _brg.RegisterMesh(mesh);
        
        //unity 靠这个bound 判断是否触发OnPerformCulling  所以必须要设置    
        _brg.SetGlobalBounds(new Bounds(Vector3.zero, Vector3.one * 1000));
        CreateCubeBuffer();
    }

    void CreateObjct()
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh = obj.GetComponent<MeshFilter>().sharedMesh;
        GameObject.Destroy(obj);
        mat = new Material(Shader.Find("Learning/Simple BRG Unlit"));
    }

    void CreateCubeBuffer()
    {
        //初始化一些缓存用的格式
        objet2WorldMatrixs = new Matrix4x4[InstanceCount];
        
        //统计总数据量 (bytes)  因为
        int objectToWorldOffset = BufferHeanderSize;
        int worldToObjectOffset = objectToWorldOffset + PackedMatrixSize * InstanceCount;
        int totalBufferSize = worldToObjectOffset + PackedMatrixSize * InstanceCount;
        
        //声明创建graphics Buffer
        _instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw,totalBufferSize/sizeof(int),sizeof(int));
        _instanceBuffer.name = "test batch";
        //创建一个int数组来存储数据
        raw = new int[totalBufferSize/sizeof(int)];
   
        //充填数据内容
        for (int i = 0; i < InstanceCount; i++)
        {
            //创建矩阵信息  object2world  world2object 
            FillRawData_Int(i, raw);
        }
        _instanceBuffer.SetData(raw);
        //创建metaData 用来绑定属性和缓冲区的对应信息
        //Metadata 把 Shader 属性名映射到 Raw Buffer 中对应数组的起始位置。
        // 注意：这里只保存起始字节地址，Shader 会根据当前实例 ID 找到自己的那一份数据。
        var metaData = new NativeArray<MetadataValue>(2,Allocator.Temp);
        metaData[0] = new MetadataValue()
        {
            NameID = Shader.PropertyToID("unity_ObjectToWorld"), 
            Value = PerInstanceData | (uint)objectToWorldOffset
        };
        metaData[1] = new MetadataValue()
        {
            NameID = Shader.PropertyToID("unity_WorldToObject"),
            Value = PerInstanceData | (uint)worldToObjectOffset
        };
        _batchID = _brg.AddBatch(metaData,_instanceBuffer.bufferHandle);
        metaData.Dispose();
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }

    [Button(ButtonSizes.Gigantic)]
    private void Changevalue()
    {
        int packedMatrixSize_int = PackedMatrixSize / sizeof(int);
        int objectToWorldOffset_Int = BufferHeanderSize/sizeof(int);
        for (int i = 0; i < InstanceCount; i++)
        {
            Matrix4x4 moveUp = Matrix4x4.TRS(Vector3.up, Quaternion.identity, Vector3.one);
            objet2WorldMatrixs[i] *= moveUp;
            FillMatrix(raw, objectToWorldOffset_Int +i * packedMatrixSize_int, objet2WorldMatrixs[i]);
        }
        _instanceBuffer.SetData(raw);
    }
    
    private void OnDisable()
    {
        _brg?.Dispose();
    }

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
            drawCommands =  Allocate<BatchDrawCommand>(1),
            drawRanges = Allocate<BatchDrawRange>(1),
            visibleInstances = Allocate<int>(InstanceCount),
            drawCommandPickingInstanceIDs = null,
            instanceSortingPositions = null,
            instanceSortingPositionFloatCount = 0
        };

        output.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsBegin = 0,
            drawCommandsCount = 1,
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

        output.drawCommands[0] = new BatchDrawCommand
        {
            visibleOffset = 0,
            visibleCount = (uint)InstanceCount,
            batchID = _batchID,
            materialID = _matId,
            meshID = _meshId,
            submeshIndex = 0,
            splitVisibilityMask = 0xff,
            flags = BatchDrawCommandFlags.None,
            sortingPosition = 0
        };
        for (int i = 0; i < InstanceCount; i++)
        {
            output.visibleInstances[i] = i;
        }
        
        cullingOutput.drawCommands[0] = output;
        //本列没有调度Job 所以返回空JobHandle
        return default;
    }

    private void FillRawData_Int(int index,int[] raw)
    {
        //首先把bytes 数量转换为int数量
        int packedMatrixSize_int = PackedMatrixSize / sizeof(int);
        int objectToWorldOffset_Int = BufferHeanderSize/sizeof(int);
        int worldToObjectOffset_Int = objectToWorldOffset_Int + packedMatrixSize_int * InstanceCount;
        FillRawData(index,raw,objectToWorldOffset_Int,worldToObjectOffset_Int,packedMatrixSize_int);
    }

    //充填raw 数据
    private void FillRawData(int index,int[] raw,int objectToWorldOffset,int worldToObjectOffset,int packedMatrixSize)
    {
        int randX = Random.Range(0, 100);
        int randy = Random.Range(0, 100);
        Vector3 pos = new Vector3(randX, 0, randy);

        Matrix4x4 trs = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);

        Matrix4x4 object2WorldMat = this.transform.localToWorldMatrix * trs;
        objet2WorldMatrixs[index] = object2WorldMat;
        Matrix4x4 world2ObjectMat = object2WorldMat.inverse;

        //通过偏移量保存数据
        FillMatrix(raw,objectToWorldOffset+index*packedMatrixSize,object2WorldMat);
        FillMatrix(raw,worldToObjectOffset+index*packedMatrixSize,world2ObjectMat);
    }
//填充矩阵数据
    private void FillMatrix(int[] raw,int offset,Matrix4x4 mat)
    {
        //竖着保存数据
        raw[offset+0] = GetBitInt(mat.m00);
        raw[offset+1] = GetBitInt(mat.m10);
        raw[offset+2] = GetBitInt(mat.m20);
        raw[offset+3] = GetBitInt(mat.m01);
        raw[offset+4] = GetBitInt(mat.m11);
        raw[offset+5] = GetBitInt(mat.m21);
        raw[offset+6] = GetBitInt(mat.m02);
        raw[offset+7] = GetBitInt(mat.m12);
        raw[offset+8] = GetBitInt(mat.m22);
        raw[offset+9] = GetBitInt(mat.m03);
        raw[offset+10] = GetBitInt(mat.m13);
        raw[offset+11] = GetBitInt(mat.m23);
    }
//充填向量信息
    private void FillVector4(int[] raw,int offset,Vector4 vec)
    {
        raw[offset+0] = GetBitInt(vec.x);
    }
    //获取int数据 之所以要保存singleInt32  是因为float在不同平台下可能不同 所以为了ssbo能拿到相同的数据
    //因为ssbo是int类型 所以需要将float类型的数据转换为int类型
    private int GetBitInt(float value)
    {
        return BitConverter.SingleToInt32Bits(value);
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
}
