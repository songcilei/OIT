using Sirenix.OdinInspector;
using UnityEngine;
/// <summary>
/// 增量缓存方案   这东西还蛮好玩的 只要想明白了  原理上很简单  全是技巧！ 主要应用求余法 把世界坐标映射为真实物理坐标
/// 因为只要保证数据长度不变的情况下 新增量肯定等于减少量 所以只需要把新增量重新赋值 并将数据移动到正确的位置 就可以了
/// </summary>
public class TestRingBuffer : MonoBehaviour
{
    public int[] value = { 0, 1, 2, 3 };

    private int oldPos;
    private int currentPos;

    private void Start()
    {
        oldPos = Mathf.FloorToInt(transform.position.x);
        currentPos = oldPos;

        // 根据初始位置初始化缓存。
        for (int i = 0; i < value.Length; i++)
        {
            int worldX = oldPos + i;
            int physicalIndex = GetPhysicalIndex(worldX);
            value[physicalIndex] = worldX;
        }
    }

    private void Update()
    {
        currentPos = Mathf.FloorToInt(transform.position.x);

        if (currentPos == oldPos)
        {
            return;
        }

        if (currentPos > oldPos)
        {
            // 向右移动，逐格更新右侧新进入的方块。
            for (int newPos = oldPos + 1; newPos <= currentPos; newPos++)
            {
                int newWorldX = newPos + value.Length - 1;
                WriteWorldValue(newWorldX);
            }
        }
        else
        {
            // 向左移动，逐格更新左侧新进入的方块。
            for (int newPos = oldPos - 1; newPos >= currentPos; newPos--)
            {
                int newWorldX = newPos;
                WriteWorldValue(newWorldX);
            }
        }

        oldPos = currentPos;
    }

    private void WriteWorldValue(int worldX)
    {
        int physicalIndex = GetPhysicalIndex(worldX);
        value[physicalIndex] = worldX ;

        Debug.Log(
            $"世界方块={worldX}，" +
            $"物理索引={physicalIndex}，" +
            $"写入值={value[physicalIndex]}");
    }

    [Button]
    private void DebugBuffer()
    {
        currentPos = Mathf.FloorToInt(transform.position.x);

        for (int logicalIndex = 0; logicalIndex < value.Length; logicalIndex++)
        {
            int worldX = currentPos + logicalIndex;
            int physicalIndex = GetPhysicalIndex(worldX);

            Debug.Log(
                $"逻辑位置={logicalIndex}，" +
                $"世界坐标={worldX}，" +
                $"物理索引={physicalIndex}，" +
                $"值={value[physicalIndex]}");
        }
    }

    private int GetPhysicalIndex(int worldX)
    {
        int remainder = worldX % value.Length;
        return remainder < 0
            ? remainder + value.Length
            : remainder;
    }
}