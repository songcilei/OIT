# GPU Skin Animation

这套工具把 FBX 的骨骼动画烘焙进 `RGBAFloat` 纹理，运行时由顶点 Shader 完成蒙皮。

## 使用方法

1. 在 Project 窗口选择一个或多个 `.fbx`。
2. 执行 `Assets > GPU Skin > Bake Selected FBX`。
3. 设置采样帧率后点击“烘焙选中的 FBX”。
4. 工具会在 FBX 同目录创建 `<FBX名>_GPUSkin` 文件夹。
5. 将生成的 `<FBX名>_GPU.prefab` 拖入场景。

生成内容包括：

- 动画纹理 `.asset`
- 保留骨骼权重的静态 Mesh
- GPU 动画 Material
- `GPUSkinAnimationData`
- 可直接预览和播放的 Prefab

生成的 Prefab 根节点带有 `GPUSkinPrefabPlayer`。即使原 FBX 含多个
`SkinnedMeshRenderer`，也可以在根节点统一选择动画、播放、暂停、拖动时间、
CrossFade、修改速度和管理 AABB，无需逐个操作子部件。

## 纹理布局

纹理横轴是骨骼，纵轴是动画帧：

```text
width  = boneCount * 3
height = 所有动画的总帧数
```

每根骨骼每帧占三个像素：

```text
pixel 0 = 蒙皮矩阵第 0 行
pixel 1 = 蒙皮矩阵第 1 行
pixel 2 = 蒙皮矩阵第 2 行
```

仿射矩阵最后一行恒为 `(0,0,0,1)`，所以无需保存第四行。

## 运行时 API

```csharp
GPUSkinPlayer player;

player.Play("Idle");
player.Play("Run", 0.5f);           // 从动画 50% 处开始
player.CrossFade("Attack", 0.2f);   // 0.2 秒切换
player.Pause();
player.Resume();
player.SetNormalizedTime(0.75f);
```

多部件 Prefab 推荐直接控制根节点：

```csharp
GPUSkinPrefabPlayer prefabPlayer;

prefabPlayer.Play("Idle");
prefabPlayer.CrossFade("Run", 0.2f);
prefabPlayer.SetNormalizedTime(0.5f);
prefabPlayer.Pause();
prefabPlayer.Resume();
prefabPlayer.Speed = 1.5f;
```

## AABB

烘焙器会遍历所有动画帧，用 `SkinnedMeshRenderer.BakeMesh` 计算动画全过程的联合 AABB。
`GPUSkinPlayer` Inspector 中可以启用 `Override Bounds` 手动设置 Bounds；选中物体时黄色 Gizmo 会显示当前 Bounds。

## 限制

- 面向 Unity 2022.3 + URP。
- Shader 使用四骨骼权重 `BLENDWEIGHTS/BLENDINDICES`。
- 动画纹理使用 `RGBAFloat`，大规模角色需要关注显存占用。
- 当前 Shader 是基础纹理与简单光照示例；项目正式材质可复用同一套纹理采样/蒙皮函数。
- 动画不再驱动原骨架，因此依赖骨骼 Transform 的挂点、碰撞体和 IK 需要额外的 CPU 逻辑。
