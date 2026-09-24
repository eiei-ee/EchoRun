# 城市记忆信使

与层叠记忆街区配套的贴身跑者服装。用柠檬黄主体、靛蓝活动区、淡紫折领和嵌片建立与场景的联系，背部使用圆弧软包形记忆匣。肩袖和躯干连成一张曲面，裤装采用连续的胯部、膝部与小腿轮廓。已去掉方形胸袋、裤袋和膝部色块，保留短帽檐、窄袖口、斜肩带、少量缝制标识与鞋带。

## 源与绑定

- `Tools/Blender/build_memory_courier.py`：Blender 离线建模脚本。
- `MemoryCourier.blend`：保留 38 个可编辑部件；导出时才合并副本，不把建模部件直接堆进游戏。
- `Assets/Art/LayeredMemory/Models/MemoryCourier.fbx`：一张带权重的服装网格与原始 Mixamo 骨架。
- `Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing.asset`：Unity 离线转入场景空间后的运行时网格。
- `LayeredMemoryRunnerInstaller.Install`（菜单 `Tools → Echo Runner → Layered Memory → Install Courier`）将网格绑定到场景原来的骨骼，不复制骨架。

场景中的 `OrangeEchoOutfit/OE_OrangeEchoClothing` 名称是已有的换色和回声克隆接口，因此继续保留。它不是当前美术方向的名字。六个 `OE_*` 材质沿用现有层叠记忆配色；材质名中的历史 Orange 命名也不意味着角色仍使用橙色。

原头部、面部、手部、Avatar、Animator Controller 和动作文件继续使用项目内的 Exo Gray / HumanMotion 资产。此次没有重建人物脸型。来源与授权说明沿用项目的 `THIRD_PARTY_NOTICES.md`。服装和装备为本项目离线制作，没有下载新的第三方模型或贴图。

## 约束

- 不更换 player 根对象、CharacterModel、碰撞体、根运动设置或控制器引用。
- 一张服装网格、六个已有材质槽，网格预算不超过 11,000 三角面。没有额外材质、贴图、光源、布料模拟或运行时造型脚本。
- 可编辑源骨架有 112 根骨骼；每次导出比较原始骨骼矩阵，导入后再映射到场景骨骼。头饰绑定 Head，记忆匣绑定 Spine2，衣裤跟随原有躯干和四肢权重。
- 原有回声通过克隆 CharacterModel 获得相同造型，颜色及学习规则仍由原流程控制。
- 轮廓修订直接调整衣服截面和附件尺寸，不整体缩放角色或移动骨骼。袜口在原 Leg / Foot 骨骼间过渡，覆盖脚踝弯折时的衣鞋接缝；保留原脚长和接地位置。
- 上衣与裤装的体素合并、表面松弛和减面只在 Blender 离线执行。导出前检查两张主体网格均为一个连通块、零非流形边；实际运行不再合并或细分网格。肩部和胯部权重沿用原骨骼，并在新连续表面上平滑过渡。
- `courier-manifest.json` 记录源网格规模、连通性和实际截面尺寸。`silhouette_measurements()` 用网格边与截面的交点测量，不能用减面后稀疏顶点的邻域代替截面，也不通过镜头变化宣称轮廓改善。
- 生成器不覆盖旧 OrangeEcho 源模型。需要重新生成源模型时运行本脚本，再执行 Install Courier。重建旧角色绑定后同样最后执行 Install Courier。

## 验证

`LayeredMemoryRunnerReview.CaptureAfter` 用实际 Animator 状态求值和强制更新蒙皮矩阵采集跑步、跳跃、滑铲三个时间点的侧后视图。这些是静态动作采样，不能代替游戏中的输入与动作衔接。

`LayeredMemoryRunnerReview.BuildWindows` 当前构建独立存档产品 `EchoRun-LayeredMemory-RunnerFormReview`。本次连续曲面修订见 `docs/2026-09-24-memory-courier-form.md` 和 `TestResults/LayeredMemoryRunnerForm-20260924/`。前两轮记录保留在对应的 `memory-courier.md`、`memory-courier-slim.md` 与 TestResults 目录，不能混用不同轮次的构建和截图。
