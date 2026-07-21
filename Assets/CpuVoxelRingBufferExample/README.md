# Unity CPU 3D 环形缓冲区教学示例

这个示例用一个固定大小的 `int[,,]`，演示体素 Clip Map 如何跟随相机移动，同时复用旧的 3D 存储空间。

它暂时没有实现真正的场景体素化和 GI。缓存里的整数由世界体素坐标生成，只是为了让我们能够检查每个位置的数据是否正确。

## 在 Unity 中运行

1. 使用 Unity 2022.3 或更高版本打开本目录。
2. 新建一个场景，并创建一个空 GameObject。
3. 给 GameObject 添加 `CpuVoxelRingBufferDemo` 组件。
4. 把 Main Camera 拖到组件的 `Target` 字段；不拖时也会自动寻找 Main Camera。
5. 进入 Play Mode，并在 Hierarchy 中选中这个 GameObject。
6. 打开 Scene 窗口的 Gizmos，移动相机观察体素盒子整格移动。

默认缓存为 `8 x 4 x 8`，总共 256 个体素。相机沿 X 移动一个体素时，Console 应显示只更新了：

```text
1 x 4 x 8 = 32 个体素
```

如果沿 X 瞬移 8 格或更多，新旧范围已经不再重叠，因此会完整重建 256 个体素。

## 两套坐标

示例中必须区分两套坐标。

### 世界体素坐标

世界体素坐标表示体素在无限世界里的固定位置。例如：

```text
(-1, 2, 5)
```

相机移动不会改变这个坐标。真实体素系统会用它确定该位置对应哪些建筑、材质和光照。

### 缓冲区索引

缓冲区索引表示数据当前存储在固定数组的哪个槽位。每个轴都使用正数取模：

```text
index = ((worldCoordinate % size) + size) % size
```

代码使用了一个等价但少做一次取模的版本：

```csharp
int remainder = value % modulus;
return remainder < 0 ? remainder + modulus : remainder;
```

例如 X 轴缓存宽度为 8：

```text
世界 X =  0  -> 数组 X = 0
世界 X =  7  -> 数组 X = 7
世界 X =  8  -> 数组 X = 0  （复用槽位）
世界 X = -1  -> 数组 X = 7  （负数也合法）
```

世界坐标 `0` 和 `8` 会映射到同一个数组槽位，但它们不会同时处于宽度为 8 的当前覆盖范围中。因此新进入的体素可以安全覆盖已经离开的体素。

## 移动一格时发生什么

设缓存的 X 范围最初是：

```text
[0, 1, 2, 3, 4, 5, 6, 7]
```

相机向右移动一格后，范围变成：

```text
[1, 2, 3, 4, 5, 6, 7, 8]
```

其中 `1..7` 仍然有效，不需要重写。只有世界 X 为 `8` 的新切片需要生成数据。因为 `8 % 8 = 0`，它正好覆盖世界 X 为 `0` 曾经使用的数组槽位。

这里没有移动数组，也没有复制重叠区域。发生变化的只有：

- 缓存覆盖范围的最小世界坐标从 0 变为 1。
- 新进入范围的 X=8 切片覆盖旧的 X=0 槽位。

## 代码阅读顺序

建议按下面的顺序阅读：

1. `CpuVoxelRingBuffer.WorldToBufferIndex()`：理解正数取模。
2. `CpuVoxelRingBuffer.MoveTo()`：理解如何识别新进入的体素。
3. `CpuVoxelRingBuffer.WriteWorldVoxel()`：理解新世界坐标如何覆盖旧槽位。
4. `CpuVoxelRingBufferDemo.Update()`：理解相机跨过体素边界后如何移动缓存。
5. `CpuVoxelRingBufferDemo.OnDrawGizmosSelected()`：观察缓存当前覆盖的空间。

## 教学版的性能选择

`MoveTo()` 会遍历新的缓存范围，再判断每个体素是否属于旧范围。它只重写新切片，但仍会执行一次小缓存范围的判断循环。

这样写是为了让算法更容易读懂。GPU 版本通常会根据 X、Y、Z 的移动量，直接派发对应的新切片，不再遍历重叠区域。两种写法使用的环形索引原理相同。

## 测试

在 Unity 中打开：

```text
Window > General > Test Runner > EditMode > Run All
```

测试覆盖初始化、负坐标、越界读取、单切片更新、两轴移动、多格移动、不移动、瞬移完整重建和演示坐标换算。
