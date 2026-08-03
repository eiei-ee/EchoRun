# ECHO//RUN：AI 回声竞速

[![Three-platform Tuanjie CI](https://github.com/eiei-ee/EchoRun/actions/workflows/three-platform-ci.yml/badge.svg)](https://github.com/eiei-ee/EchoRun/actions/workflows/three-platform-ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/eiei-ee/EchoRun)](https://github.com/eiei-ee/EchoRun/releases/latest)
[![Play WebGL](https://img.shields.io/badge/Play-WebGL-ff8a3d)](https://eiei-ee.github.io/EchoRun/)

> **Project status:** public alpha. The playable loop, browser demo, and
> Windows release are available. Automated WebGL, Windows, and Android
> verification runs on the self-hosted CI runner when it is online; APIs and
> data formats may still change before 1.0.

ECHO//RUN is an open-source, AI-native endless-runner reference built with
Tuanjie Engine 2022.3 and C#. It demonstrates online behavior cloning, a
contextual-bandit track director, deterministic route-safety constraints,
runtime-generated UI and geometry, object pooling, and reproducible WebGL,
Windows, and Android builds.

The repository is intended both as a playable game and as a practical example
for developers exploring Tuanjie/Unity runtime AI and cross-platform delivery.
It does not depend on a hosted inference service: learning and inference run
locally in the player.

基于团结引擎（Unity 2022.3）与 C# 开发的 AI 原生 3D 竞速跑酷游戏，提供
WebGL 浏览器试玩和 Windows x64 正式下载；Android 构建在完成对应版本测试后发布。

## 试玩

**浏览器即玩：https://eiei-ee.github.io/EchoRun/**

支持键盘操作：A/D 或 ← → 左右变道，W 或 ↑ 跳跃，S 或 ↓ 滑铲，Esc 暂停或继续。网页端和桌面端也可按住鼠标拖动，移动端使用触屏滑动并建议横屏游玩。

## 核心功能

- 三车道移动：左右切换、跳跃、滑铲
- 无限道路：直线路段与左右 90° 转弯路段持续生成
- 动态生成：金币与障碍物随道路生成
- 对象池复用：道路、金币和障碍物统一回收复用
- 障碍系统：Low / High / Barrier 三类障碍，对应滑铲、跳跃、变道处理
- 安全车道：生成逻辑保证每段道路至少存在可通行路线
- 金币引导：安全车道生成主金币线，相邻车道生成奖励金币线
- AI 跑酷导演：在线学习玩家行为，动态选择恢复、流动、施压和纪录冲刺节奏
- AI 个人影子：首局在线学习玩家动作，后续生成无碰撞的行为克隆对手
- 影子竞速：金币与成功闪避转化为竞速进度，目标从单纯刷分变为击败进化中的自己
- 游戏状态：主菜单、游戏中、暂停、死亡结算、重新开始
- UI 流程：HUD、暂停面板、设置面板、角色选择、结算面板
- 数据持久化：最高分、总金币、音量、帧率、角色配置本地保存
- 多端输入：键盘、触屏滑动、网页与桌面端鼠标拖动
- 平台适配：WebGL 响应式 16:9 画布、移动端横屏提示与安全区、切换标签页或应用时自动暂停
- 性能分档：桌面 WebGL / 桌面端保留 120 帧选项，移动 WebGL / Android 默认中等画质并限制最高 60 帧

## 技术实现

### 程序化道路生成

TrackManager 负责道路段生成、转弯路段拼接和动态物体放置。直线路段先规划安全车道，再生成障碍物和金币，避免三车道全堵或金币/障碍重叠。

### 对象池

道路段、金币和障碍物通过对象池复用，减少运行时频繁 Instantiate / Destroy 带来的性能开销。

### 安全车道与金币引导

每段道路维护一条安全车道，金币主线放在安全车道上提示前进路线，相邻车道生成短金币线作为风险奖励。

### AI 跑酷导演

AITrackDirector 使用轻量上下文多臂老虎机模型。模型读取距离、动作频率、金币、闪避、撞击和纪录压力等运行数据，输出后续道路的障碍概率、金币密度、安全道偏移、封锁车道数与转弯倾向，并根据玩家实际跑完路段后的奖励在线更新策略权重。

道路会提前生成，因此模型决策进入延迟评估队列，只有玩家抵达对应路段后才会获得反馈。安全车道连续性和“最多封锁两条车道”仍由确定性校验器强制保证；关闭 AI 后可退回原程序化生成模式。

### AI 个人影子

AIShadowRunner 使用纯 C# 在线多分类行为克隆模型。输入包含玩家当前跑道、速度、前方障碍距离、障碍相对跑道、障碍类型、跳跃与滑铲状态，输出保持、左移、右移、跳跃或滑铲动作。模型在玩家操作时实时更新，并通过 PlayerPrefs 保存权重、训练样本、目标配速和影子代数。

首局是校准局；只有总样本、有效动作数量和动作类型覆盖同时达到阈值，下一局才会出现半透明的个人 AI 影子，单纯保持直跑不能完成校准。影子不使用物理碰撞推挤玩家，但拥有独立的赛道坐标、障碍感知和逻辑碰撞：动作判断错误时会闪红、失速、损失竞速进度并累计失误。玩家通过距离、金币和有效闪避积累竞速进度，HUD 实时显示双方领先差、当前决策置信度和影子失误数。每局开始时会冻结上一代模型作为本局对手，本局新动作只训练下一代，因此影子不会即时复制当前输入。

核心循环为：`校准跑酷 → AI 学习动作模型 → 挑战个人影子 → AI 根据差距生成赛道 → 影子跨局进化`。去掉运行时 AI 后，个人对手、代际成长、专属赛道和正式胜负目标都会消失。

## 技术栈

团结引擎 (Unity 2022.3) · C# · Unity Test Framework · WebGL · Android

## 第三方资源

界面中文字体使用 Noto Sans CJK SC，按 SIL Open Font License 1.1 分发，许可证见 `Assets/Resources/Fonts/OFL.txt`。

## 本地开发

```bash
# Clone, then open the repository root with Tuanjie Engine 2022.3.62t8.
# Run tests from Window → General → Test Runner → Run All.
```

完整环境、测试和构建命令见 [docs/BUILDING.md](docs/BUILDING.md)。

## 开源协作 / Open-source collaboration

| 文档 | 内容 |
| --- | --- |
| [CONTRIBUTING.md](CONTRIBUTING.md) | 开发流程、测试要求、资源许可规则 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 运行时架构、AI 边界和安全约束 |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 已验证能力和公开路线图 |
| [SECURITY.md](SECURITY.md) | 安全边界和漏洞报告方式 |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | 字体、音乐和音效来源与许可证 |
| [MAINTAINERS.md](MAINTAINERS.md) | 维护者、职责与决策流程 |

Bug、兼容性问题和可复用性改进都欢迎通过 GitHub Issues 提交。首次贡献前请先阅读贡献指南；涉及新字体、音频、图像、模型或插件的 PR 必须同时提供来源和再分发许可。

## License / 许可证

The source code in this repository is licensed under the MIT License.
Third-party assets, including fonts, audio, models, and other media, are
excluded unless explicitly stated otherwise. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for details.

## Releases

The previous release was withdrawn during asset-provenance cleanup. The next
official download will be `v0.1.1` after its Windows package is rebuilt and
verified. The withdrawn release record is retained in
[docs/releases/v0.1.0.md](docs/releases/v0.1.0.md). Build output, caches, and
the Unity `Library` directory are intentionally excluded from the repository
and must not be uploaded as GitHub Release assets.
