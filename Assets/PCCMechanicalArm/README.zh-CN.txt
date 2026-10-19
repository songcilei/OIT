PCC 可伸缩机械臂 — Unity 原型
========================================

这是一个可运行的“章鱼博士风格”机械臂原型：多段常曲率模型（PCC）、
末端位置 IK、分段伸缩、连续追踪目标，以及一节一节的程序化金属外观。
不是 Rigidbody 物理机械臂，也不是将骨骼链做 FABRIK 后再称为 PCC。

一、最快运行
----------------------------------------
1. 将 PCCMechanicalArm.unitypackage 导入 Unity 项目。
   或将 ZIP 中的 PCCMechanicalArm、PCCMechanicalArmDemo 两个文件夹放到 Assets 下。
2. Built-in 渲染管线项目可直接打开：
   Assets/PCCMechanicalArmDemo/PCCArmDemo.unity
3. URP 项目请使用菜单：Tools > PCC Mechanical Arm > Create Demo Scene。
   菜单会按当前渲染管线生成新材质和演示场景，不会覆盖已有同名资产。
4. 点击 Play，会自动出现机械节，并追踪绿色小球。
5. 想手动控制：选中 IK Target，关闭 PCCDemoTarget 的 Animate，
   然后在 Scene 视图中用移动工具拖动小球。
6. 选中 PCC Arm，可实时查看 Tip Error、Solved Pose Error、Total Length。

注意：演示几何体在 Play 时生成，编辑状态看不到机械臂网格是正常的。
预制演示场景采用 Built-in 材质；URP 必须生成/替换匹配材质。
HDRP 不在本原型菜单的支持范围，可自行提供兼容材质。
所有生成材质均为真实 .mat 资产并由场景引用，不依赖运行时寻找 Shader。

二、接入已有场景
----------------------------------------
1. 创建一个空物体作为机械臂基座，挂 PCCArm。
2. 本地 +Z 是机械臂伸直时的方向。旋转基座即可改变安装方向。
3. 创建独立的目标物体，拖入 Target。不要把目标放在 Tip Attachment 下。
4. 给 Shell Material 和 Core Material 分别赋金属外壳材质与内芯材质。
5. 根节点以及祖先节点使用正的均匀缩放，最好全部 (1,1,1)。
   非均匀缩放会把圆弧拉成椭圆，不再符合此处的 PCC 几何模型。
6. 如果希望挂爪子/武器/特效，创建空物体并拖给 Tip Marker，模型放在它下面。
   工具的本地 +Z 沿末端切线；其几何偏移在子物体中调整。

运行时控制（需要 using PCCMechanicalArm）：

    arm.target = targetTransform;       // 切换追踪对象
    arm.allowExtension = false;         // 冻结当前长度，只弯曲
    arm.allowExtension = true;          // 恢复自动伸缩
    arm.solveIK = false;                // 暂停 IK；仍可向已求解姿态过渡
    arm.ResetPose();                    // 重置初始姿态

    // solveIK=false 时可手动给各段姿态，弯曲参数单位为弧度。
    arm.Solver.SetSectionPose(0, new Vector2(0.4f, 0.1f), 1.2f, false);

    // 获得第 1 段中点的位置与朝向，用于挂自定义模型。
    arm.GetWorldPose(1, 0.5f, out Vector3 position, out Quaternion rotation);

Solver 在 OnEnable/Play 时创建。禁用/销毁组件会释放运行时 Mesh。
开关组件会重建并回到初始姿态，而不是保留之前的求解状态。

三、参数说明
----------------------------------------
Sections                       PCC 求解段；默认 3 段，最多 8 段。
每段 Min Length / Max Length   该段弧长的最小/最大值，伸缩就在此范围内。
每段 Initial Length            初始长度。
每段 Initial Bend Degrees      初始弯曲向量（X/Y 两个分量），用于姿态偏好。
每段 Max Bend Degrees          总弯曲角的上限，而不是每个金属节的转角。
Links Per Section              每个 PCC 段离散成多少个可见连接间隔；默认 10。
Radial Sides                   金属环截面的边数，默认 8。
Shell Radius                   金属环外半径。
Shell Thickness Fraction       环厚度相对于最短状态节距的比例。
Tip Radius Scale               末端相对基座的粗细，默认 0.7。
Iterations Per Frame           每帧最多求解多少次，默认 8。
Position Tolerance             IK 停止误差，机械臂本地单位，默认 0.01。
Damping                        DLS 阻尼，默认 0.035。更大通常更稳但可能更慢。
Max Parameter Step             每次迭代的归一化参数最大步长，默认 0.18。
Bend Speed Degrees             每段弯曲向量变化速度上限，度/秒。
Extension Speed                每段弧长变化速度上限，本地单位/秒。
Cast Shadows                   默认关闭，减少阴影开销。

改变 Sections 配置、段数、Links Per Section、Radial Sides 或
Shell Thickness Fraction 后，重新 Play，或在组件菜单点 Rebuild Arm (Play Mode)。
目标、IK 预算、速度、外壳半径等字段可在运行中直接调节。
替换材质也需要 Rebuild 才会重新赋给渲染器。

默认 3 段：每段 0.55~1.65，最大总弧长 4.95 本地单位。
请勿把 1.65（最小总弧长）当作末端到基座的最小距离：弯曲可以缩短该距离。
也不要把最大总弧长包围球内的所有点都当作可达：还存在弯曲限位与局部解。

四、实现原理
----------------------------------------
每个 PCC 段使用：
    bendX = kappaX * L
    bendY = kappaY * L
    L     = 当前弧长

bendX/bendY 是积分曲率，避免直接使用弯曲平面角 phi 时的直线奇异性。
令 theta = sqrt(bendX^2 + bendY^2)，归一化弧位置 t 在 [0,1]：

    x = L * bendX * t^2 * cosc(theta*t)
    y = L * bendY * t^2 * cosc(theta*t)
    z = L * t * sinc(theta*t)

    sinc(a) = sin(a)/a
    cosc(a) = (1-cos(a))/a^2

当 a 接近 0，使用泰勒展开，直线状态得到 (0,0,L*t)，不发生除零。
每段的局部位置和姿态通过前一段末端的坐标系递推，因此段与段位置、切线连续。
段与段之间曲率可以跳变；这正是“分段常曲率”，不是全程同一条圆弧。

IK：有限差分位置雅可比 + 阻尼最小二乘（DLS）。
    dq = J^T * inverse(J*J^T + lambda^2*I) * positionError

不同量纲归一化后求解，候选姿态投影到长度/弯曲角范围内，并通过回溯线搜索
拒绝让末端误差增加的步骤。上一帧解作为下一帧初值，最终以速度上限追赶求解姿态。
直线且无法继续缩短时，有限曲率扰动用于尝试摆脱零一阶梯度。
该扰动不是全局优化，不保证任意目标都能找到可达解。

伸缩并非直接拉长外壳模型：
    - 调整各段弧长 L，重算整个 PCC 中心线；
    - 金属环厚度固定，节距随长度变化；
    - 中间的内芯柱长度变化，形成伸缩视觉。
这是视觉/运动学模型，不包含真实套筒的行程组合、齿轮与传动设计。

五、文件职责
----------------------------------------
Runtime/PCCKinematics.cs       圆弧 FK、PCCSection 配置、有限差分 DLS 求解器。
Runtime/PCCArm.cs              Unity 主组件、目标追踪、姿态采样、生命周期。
Runtime/PCCArmMesh.cs          单个动态 Mesh，外壳/内芯两个子网格。
Runtime/PCCDemoTarget.cs       演示目标运动，关闭 Animate 后手动拖动。
Editor/PCCArmDemoMenu.cs       一键创建演示场景和材质；不进入运行时构建。

六、微信小游戏性能与边界
----------------------------------------
- 未使用 Compute Shader、Jobs、Burst、线程、GraphicsBuffer 或每节 Rigidbody。
- 热路径复用求解参数、雅可比、顶点和法线数组；没有每帧 new 数组或 LINQ。
- 默认一条机械臂：30 个连接间隔 / 31 个金属环、2464 顶点、2 个材质子网格。
- 这不是“整个场景只有两次 Draw Call”的保证；材质 pass、光源、阴影仍影响绘制。
- 首次创建/重建时会分配资源；此处并不声称整帧/引擎内部零 GC。
- 多条机械臂时先尝试：3 PCC 段、每段 6~8 节、6~8 边、4~6 次 IK 迭代。
- 金属节数影响绘制成本，PCC 段数影响 IK 自由度；不要为了更多金属节而增加 PCC 段数。
- DLS 使用数值雅可比，段数增多会提升 CPU 成本；默认针对少量段的游戏效果。
- 发布前必须用微信真机验证，不承诺任何固定机型的 60 FPS。
- 运行时兜底材质使用 Shader.Find，仅方便原型；正式包必须显式引用材质资产，
  避免打包时着色器被裁剪。

不包含：
1. 障碍物规避、自碰撞、与角色身体碰撞。
2. Rigidbody 力学、抓取物体和受力反馈。
3. 末端朝向 IK、独立扭转自由度。
4. 自动把现有骨骼/模型蒙皮转换为 PCC。
5. 任意目标的全局最优解或不可达性严格判定。

Tip Error 是当前显示姿态到目标的世界距离；Solved Pose Error 是内部目标姿态
的本地求解误差。它们因速度限制、缩放而可能不同。
目标移动过快、速度上限过低、目标不可达或陷入局部解时，Tip Error 会持续偏大。
此时先降低目标速度或移动到机械臂正前方，必要时 Reset Pose；不是无限加迭代。

七、验证
----------------------------------------
实际 Unity 编译、数值与网格测试、Play Mode 运行结果见随包 VALIDATION.txt。
微信真机和你的渲染管线/项目配置尚未验证。

数学参考（说明模型来源，不代表代码由该项目提供）：
Open Continuum Robotics — Constant Curvature Kinematics Framework
https://www.opencontinuumrobotics.com/101/2022/12/02/cc-kinematics.html

本包代码是针对本次需求新写的原型；没有使用外部模型、纹理或商用插件。

可在 Unity 菜单 Tools > PCC Mechanical Arm > Run Self Tests 重新运行数值/网格检查。
此菜单不修改场景，也不会退出编辑器。

