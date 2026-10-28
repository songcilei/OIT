FXLod — 源 Prefab + Middle / Low 配置与导出（Unity 2022.3）
======================================================

一、约定
原 Prefab 本身就是完整版本，不再配置、预览或导出 High。
源 Prefab 必须位于名为 High 的文件夹内；名称按此拼写匹配，不区分大小写。
从 Prefab 的直接父目录逐级向上查找，遇到的第一个 High 即源等级根目录。
High 可以在 Assets 下任意深度，Prefab 也可以位于 High 内任意深度。
不会将 Hight、MyHigh 等其他目录名当作 High。

路径示例：
  源：Assets/Effects/Character/High/Skills/Fire/Fire.prefab
  输出：Assets/Effects/Character/Middle/Skills/Fire/Fire.prefab
        Assets/Effects/Character/Low/Skills/Fire/Fire.prefab

只将匹配的 High 目录替换为同级 Middle / Low；保留其后所有相对目录及文件名。
无需配置输出目录。不创建或重写完整版本，也不会自动移动源 Prefab。

二、配置
1. 在 Prefab Mode 中打开源特效，在根节点添加 Effects > FXLod。
2. 点击“同步列表”，添加新子物体并清除失效引用，已有配置保留。
3. 每个子物体仅显示 Middle / Low 两列勾选。
   - 勾选控制 activeSelf；父节点取消后，其整个子树不生效。
   - 根节点始终保留；初次添加节点沿用其原始 activeSelf。
   - 同名节点独立配置；支持搜索、折叠、仅显示等级差异。
   - 列表按当前显示的子物体行数自动展开，不设高度上限，不使用内部滚动条。
     搜索/折叠后自动收缩；超出窗口时使用 Inspector 自身滚动。
   - Middle / Low 表头、整列复选框、逐行复选框共用列坐标，随 Inspector 宽度保持对齐。
   - 表头可全选/全不选整个等级，不受搜索或折叠影响。
   - “复制”支持 Middle → Low、Low → Middle。
   - 顶部显示实际生效数量；橙色格表示勾选被父节点阻断。
4. Inspector 自动检查源 Prefab 所在目录，并显示目标路径。
   不符合 High 约定时显示警告，仍可编辑和预览，但禁止导出当前 Prefab。
5. 点击“导出当前 Prefab 的 Middle / Low…”会自动应用并保存当前实例的全部可应用修改；
   在 Prefab Mode 中则自动保存整个 Prefab。批量扫描导出仍需预先保存源资源。
6. 每个层级仅在根节点保留一个 FXLod，避免嵌套控制器冲突。

旧数据兼容：
- 原 middle / low 序列化字段名未变，已有勾选保留。
- Middle = 1、Low = 2 的枚举数值未变；不保留 High = 0。
- 旧 high 配置不再读取，也不会据此改变源物体状态。
- 没有运行时默认等级/启用时应用等级；启用组件和根节点不会自动修改子物体。
- 之前已经导出的历史文件不会自动删除或迁移。

三、预览
Inspector 点击“▶ Middle / Low”，或 Tools > FXLod > 预览窗口。
支持暂停、重播、停止、循环、速度、背景色、旋转/平移/缩放、聚焦。
预览使用隔离场景副本；勾选更新后自动重建，不改变原 Prefab。
支持 ParticleSystem、Animator 默认状态、Legacy Animation 默认动画。
禁用音频和普通自定义 MonoBehaviour；不模拟脚本驱动、Timeline 或 VFX Graph。
不包含主场景深度、外部灯光、碰撞物体和后处理；依赖这些条件的效果在 Play Mode 验证。
ExecuteAlways、StateMachineBehaviour 等行为不能保证完全沙箱隔离。
完整版本直接使用原 Prefab 在 Unity 中播放，不再提供单独完整版本 LOD 预览入口。

四、导出
菜单：Tools > FXLod > 批量导出 Middle Low
也可以从 Inspector 点击“导出当前 Prefab 的 Middle / Low…”。
默认批量扫描目录：Assets/Res/Effect/male_01；可以手动修改，修改后会记住。
如果该目录尚不存在，请选择实际资源目录或先准备资源。

当前 Prefab 导出入口会先提交并保存当前修改，再打开导出窗口。
- 场景实例：等同于 Unity Apply All，包含组件属性、LOD 勾选、新增/删除组件及子节点等可应用修改。
- Prefab Mode：保存整个当前 Prefab 后再导出。
- 根节点位置、旋转等 Unity 默认覆盖不自动写回资产。
- 只读资源、嵌套实例根节点歧义、保存或应用失败时停止流程并提示，不继续使用旧数据。
- 此按钮会真正修改源 Prefab；即使随后取消导出，已经应用保存的修改也会保留。
- “使用当前选中 Prefab”和目录批量扫描仅选择已保存资源，不批量应用其他场景实例。

1. 点击“使用当前选中 Prefab”，或者选择批量扫描目录并扫描。
   扫描目录可以是某个 High、它的子目录，也可以是包含多个 High 的更高层目录。
2. 检查每个源 Prefab 向上的 High 祖先，只把符合要求的配置 Prefab 加入导出列表。
   不符合的文件跳过并在 Console 报告；没有已保存源资源的普通场景对象不可导出。
3. 列表展示各源文件及 Middle、Low 的实际输出路径。
   支持搜索和批量选择；搜索隐藏的已选项目仍会导出，并显示提示。
4. 选择处理模式：
   - 删除物体及子树（默认）：彻底删除该等级排除的物体。
   - 仅禁用物体：保留节点引用，仅改变 activeSelf。
5. 点击“导出 Middle / Low”，确认后创建需要的目录及 Prefab。

安全行为：
- 默认跳过已有输出。允许覆盖后，仍需确认；覆盖保留 .meta GUID。
- 即使允许覆盖，也拒绝覆盖已经挂有 FXLod 的配置源资源。
- 批次内输出重名冲突或与源路径重叠时停止导出。
- 分级裁剪与导出操作只在隔离副本上执行，不改动源层级；单个当前 Prefab 入口在此之前会按上文自动保存源修改。
- 输出展开嵌套/Variant 链接，成为独立普通 Prefab；移除 FXLod 编辑配置。
- 材质、贴图、动画等依赖仍引用原资产，不复制依赖文件。
- 删除前检测存活组件的序列化引用风险，并报告到 Console。
  动画字符串路径和运行时代码依赖无法完整检测；此类效果优先使用仅禁用模式。
- 取消批量操作时保留已完成文件。单项失败记录日志后继续其余项目。
- 不自动清理历史输出或重命名后残留的旧文件。
- 设置保存在 ProjectSettings/FXLodExportSettings.asset，只记录扫描目录、模式、覆盖选项。

五、运行时 API
命名空间：FXLodTools
  var lod = effect.GetComponent<FXLod>();
  lod.ApplyLevel(FXLodLevel.Middle);
  lod.ApplyLevel(FXLodLevel.Low, true);  // 应用并重播活跃粒子
  lod.RestartParticles();
仅显式调用 ApplyLevel 才修改源实例状态。没有距离自动分级。

六、对齐检测与清理
入口：批量导出窗口中的“04 对齐检测与清理”。
1. 选择批量扫描目录（例如 Assets/Res/Effect/male_01）。
2. 点击“检测缺少 High 源文件的 Prefab”，只生成列表，不删除文件。
3. 核对待清理路径及对应的 High 路径；默认全选，可取消个别项目。
4. 点击“一键清理选中的 Prefab…”，确认后将文件移入回收站。

判定规则：
- Middle/技能/Fire.prefab 对应 High/技能/Fire.prefab，Low 同理。
- 按相对目录和文件名对齐，不按 GUID、仅文件名或组件是否存在判断。
- 源 Prefab 存在就保留产物，即使 High 源没有 FXLod。
- 如果 High 文件夹整体不存在，也会列出孤立产物，并给出额外警告；请先确认资源迁移或版本同步完整。
- 扫描一个上级资源目录时会递归检查其中所有 Middle / Low 树。
- 选择 High 或其子目录时，检测对应同级 Middle / Low 的相同相对目录。
- 选择 Middle 或 Low 时，只检测该目录内资源，不扩大到另一等级。
- 与当前导出勾选列表无关，以批量扫描目录为检测范围。

删除保护：
- 不自动清理，必须先检测并在确认弹窗中确认。
- 删除前重新检查范围、文件 GUID、对应 High 是否存在以及只读状态。
- High 已恢复、目标已替换、只读/未签出、正在 Prefab Mode 中打开、涉及目录链接的记录跳过并报告。
- 仅将已选 Prefab 及其 meta 移入回收站；不删除文件夹、材质、贴图等资源。
- 回收站操作失败时不会回退为永久删除。操作不支持 Ctrl+Z，可通过回收站或版本控制恢复。
- 引用被清理 Prefab 的场景/资源可能出现失效引用，请先检查结果。
- 支持中途取消，已完成的清理不回滚；结束后重新检测剩余文件。

七、自检
Tools > FXLod > 运行自检
使用唯一命名的 Assets/__FXLodSelfTest_<GUID> 临时目录，结束时清理。
覆盖祖先目录查找、两级输出映射、非法目录、多个 High、源资源保护、旧配置兼容、
父子裁剪、重复导出、嵌套 Prefab、Variant、引用风险、Inspector 和粒子预览等。




