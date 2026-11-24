# PaiPai Unity Bridge · Blender / Maya

本包面向静态网格。它延续 Max 端的输出目录、FBX + JSON 清单、材质和 Prefab 工作流；只需手动复制脚本，不需要运行安装器。

## Unity：两端共用，只装一次
用包内 Unity/MaxToUnityBridge.cs **覆盖原工程中的同名 Editor 脚本**。如果之前没安装，放到 Assets/MaxToUnityBridge/Editor/MaxToUnityBridge.cs。不要同时保留两份相同类。新版仍识别旧 Max 清单；Max 插件不用换。项目中须已有可编译的 PaiPai/BigLand/Lit_BigLand_OptimizePBR 和它依赖的渲染管线。不要移动 Shader。

## Blender
在编辑 → 偏好设置 → 插件中，通过 Install / Install from Disk 安装 **Blender_PaiPaiUnity.zip**（不要安装外层总包或解压内层 ZIP），启用 PaiPai · Unity 资源导出。在 3D 视图按 N，打开 Unity 标签。物体模式选中网格，浏览现有 Assets 子目录，选择模式并导出。目录、模式等跟随当前 .blend 的场景设置，保存 .blend 后保留。支持对选中网格求值后导出，包括静态修改器结果。

## Maya（Python 3）
将 **Maya_PaiPaiUnity.zip** 中的整个 paipai_maya 文件夹解压到 Maya 用户 scripts 目录。可在 Script Editor 的 Python 页执行以下代码查询目录：

```python
from maya import cmds
print(cmds.internalVar(userScriptDir=True))
```

再在 Python 页运行：

```python
import paipai_maya
paipai_maya.show()
```

可以把这两行保存为 Shelf 按钮。支持选中 polygon transform 或组，自动加载 fbxmaya（必须已安装）。如一个 transform 含多个非中间 mesh shape，请先拆分。窗口仅在导出成功后保存设置。

## 三种模式
| 模式 | 输出 |
|---|---|
|合并网格|一个合并的静态网格、一个 FBX 和一个 Prefab|
|分别导出|按物体名逐个生成 FBX 和独立 Prefab，忽略整体名称；同批输出重名会在写入前拒绝|
|保留子物体|一个 FBX、一个父 Prefab，各模型成为可独立选中的直接子物体；不复制源场景的多层分组|

相对摆放以首个网格的变换原点为参考（Blender 优先活动网格）。单位和额外缩放在首次导出后用已知尺寸检查。不会合并或重命名源模型。导出结果位于所选文件夹的 Incoming、Textures、Materials、Generated、Prefabs 子目录。相同目录与同名资源会更新；希望并存请换名称或目录。若 Unity 关闭，清单会在下次打开时处理。手动重试：Tools → PaiPai → Max To Unity → 重新处理 Incoming。

## 材质和范围
- Blender：以活动 Material Output 直接连接的 Principled BSDF 为主，识别基础色、切线法线（Normal Map）、粗糙度、金属度、自发光。支持普通外部图片和带受支持扩展名的内嵌位图。
- Maya：按面读取 shading group；识别 Standard Surface / aiStandardSurface 的常见属性及 Lambert 的 color / incandescence、file 节点和常见的切线法线连接。
- 底色绑定 _BaseMap；法线、粗糙度和金属度按 Shader 布局 RG / B / A 生成数据贴图；自发光绑定 _EmissionMap。支持 Blender 的 Separate Color / Separate RGB 和 Maya file 的显式 RGBA 输出，用作粗糙度、金属度通道。不是把任意带 ORM 名称的图片当作法线。
- 不烘焙程序纹理、复杂节点、位移、动画、骨骼或 UDIM；请预烘焙为单张图片、使用网格 UV0。不同应用的材质外观和自定义渲染管线不保证完全相同。缺失文件及不支持的贴图会给出提示。

## 验证
Blender 4.0.2 和本机 5.3 RC 已实际导出三种模式；4.0 导出的文件还在 Unity 2022.3.62f3 临时工程中通过 Prefab 数量、合并网格材质槽、子物体名称/相对间距、底图及通道打包纹理引用检查。测试使用同属性的简化 Shader，未验证实际项目的渲染外观。Maya 端已检查 Python 语法，未在本机 Maya 内进行实机导出。开发目录 Tests/maya_smoke.py 可用 mayapy 运行，在临时目录测试三种模式。
