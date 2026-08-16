# ECHO//RUN：AI 回声竞速

[![Three-platform Tuanjie CI](https://github.com/eiei-ee/EchoRun/actions/workflows/three-platform-ci.yml/badge.svg)](https://github.com/eiei-ee/EchoRun/actions/workflows/three-platform-ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/eiei-ee/EchoRun)](https://github.com/eiei-ee/EchoRun/releases/latest)
[![Play WebGL](https://img.shields.io/badge/Play-WebGL-ff8a3d)](https://eiei-ee.github.io/EchoRun/)

> **Project status:** public alpha. Source and the currently deployed browser
> demo are public. `v0.2.0-alpha.1` is a locally verified release candidate;
> it is not yet the deployed WebGL build or a published GitHub Release.
> Automated WebGL, Windows, and Android verification runs on the self-hosted
> CI runner when it is online; APIs and data formats may still change before
> 1.0.

ECHO//RUN is an open-source, AI-native adaptive runner duel built with Tuanjie
Engine 2022.3 and C#. It demonstrates online behavior cloning, a
contextual-bandit track director, deterministic route-safety constraints,
runtime-generated UI and geometry, object pooling, and reproducible WebGL,
Windows, and Android builds.

The repository is intended both as a playable game and as a practical example
for developers exploring Tuanjie/Unity runtime AI and cross-platform delivery.
It does not depend on a hosted inference service: learning and inference run
locally in the player.

基于团结引擎（Unity 2022.3）与 C# 开发的 AI 原生 3D 竞速跑酷游戏。当前提供
WebGL 浏览器试玩；Windows 与 Android 只有在对应候选包完成验证并附加到 GitHub
Release 后才会标记为正式下载。

## 试玩

**浏览器即玩：https://eiei-ee.github.io/EchoRun/**

当前线上试玩可能仍早于本地 `v0.2.0-alpha.1` 候选版；发布状态以
[版本说明](docs/releases/v0.2.0-alpha.1.md)为准。

支持键盘操作：A/D 或 ← → 左右变道，W 或 ↑ 跳跃，S 或 ↓ 滑铲，Esc 暂停或继续。网页端和桌面端也可按住鼠标拖动，移动端使用触屏滑动并建议横屏游玩。

## 核心功能

- 三车道移动：左右切换、跳跃、滑铲
- 程序化赛道：直线路段与左右 90° 转弯按需生成，校准局与挑战局拥有明确终点
- 动态生成：金币与障碍物随道路生成
- 对象池复用：道路、金币和障碍物统一回收复用
- 障碍系统：Low / High / Barrier 三类障碍，对应滑铲、跳跃、变道处理
- 安全车道：生成逻辑保证每段道路至少存在可通行路线
- 金币引导：安全车道生成主金币线，相邻车道生成奖励金币线
- AI 跑酷导演：在线学习玩家行为，动态选择恢复、流动、施压和纪录冲刺节奏
- AI 个人影子：首局在线学习玩家动作，后续生成无碰撞的行为克隆对手
- 六阶段回声对决：侦测、暴露、反抗、反扑、重写、决胜逐步展开，不再用三次动作快速结算整局
- 回声契约：AI 将路线偏好、跳滑倾向或固定节奏转化为每代不同的赛道规则、稳定度与反制目标
- 影子竞速：必须“破解契约 + 领先回声”才能获胜，分数、金币和闪避仅作为过程指标
- 两段式碰撞：首次失误减速恢复，第二次碰撞才结束比赛
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

道路会提前生成，因此模型决策进入延迟评估队列，只有玩家抵达对应路段后才会获得反馈。安全车道连续性和“最多封锁两条车道”仍由确定性校验器强制保证。程序化生成器可以作为诊断回退运行，但没有 AI 风格模型就不会产生个人回声、回声契约或正式挑战胜负，核心循环因此无法成立。

### AI 个人影子

AIShadowRunner 使用纯 C# 在线多分类行为克隆模型。输入包含玩家当前跑道、速度、前方障碍距离、障碍相对跑道、障碍类型、跳跃与滑铲状态，输出保持、左移、右移、跳跃或滑铲动作。模型在玩家操作时实时更新，并通过 PlayerPrefs 保存权重、训练样本、目标配速和影子代数。

首局是约 75 秒的校准局。满足总样本、有效动作数量、动作类型、跳跃和滑铲覆盖时会生成清晰回声；中断但已积累最低有效样本时会生成低清晰度的“模糊回声”，而不是让体验直接断档。单纯保持直跑不能完成完整校准。

每代由一个版本化的不可变快照定义，包含行为权重、动作序列、玩家风格、配速与清晰度。挑战期间的新动作只写入“下一代候选”，当前回声、契约与配速保持冻结；失败、碰撞、放弃或同代重试都不会暗中改写对手。只有玩家抵达终点、完成契约并领先回声，候选模型才晋升为下一代。

挑战局目标时长约 190 秒，并按 `侦测 → 暴露 → 反抗 → 反扑 → 重写 → 决胜` 展开。契约先要求稳定度达到 100%，随后进入回声反扑并回落到 55%，玩家必须再次稳定到 100% 才能真正破解；错误行为会扣除稳定度。重写阶段前 32 秒提高下一代学习权重，最后约 25 秒才进入决胜，避免“两分钟内破解并领先后立刻结束”。契约专用金币有独立标记，普通金币不会误算作路线反制动作。

影子不使用物理碰撞推挤玩家，但拥有独立赛道坐标、障碍感知和逻辑碰撞。赛道导演的观察窗口按玩家实际经过的路段距离结算；契约改写过的路线不反向污染导演的策略奖励。转弯仍保持无障碍，压力只降低转弯概率，不会把障碍塞入转弯段。

核心循环为：`校准自己 → AI 冻结本代回声与契约 → 侦测并识别旧习惯 → 两轮稳定契约 → 决胜跑赢回声 → 晋升下一代`。赛前界面说明 AI 学到的特征、本代规则和目标；HUD 显示当前阶段、预测、稳定度与领先关系；赛后报告展示本代结果与下一代学习方向。距离领先但未完成契约仍判定失败。去掉运行时 AI 后，个人对手、契约规则、代际成长和正式胜负都会消失。

## 技术栈

团结引擎 (Unity 2022.3) · C# · Unity Test Framework · WebGL · Android

## 第三方资源

界面中文字体使用项目专用的 EchoRun Sans SC 子集；它派生自 Noto Sans CJK SC 2.004，并按 SIL Open Font License 1.1 分发。许可证和可复现来源见 `Assets/Resources/Fonts/OFL.txt`、`THIRD_PARTY_NOTICES.md` 与 `Tools/Fonts/`。

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
| [v0.2.0-alpha.1 repository audit](docs/releases/v0.2.0-alpha.1-audit.md) | 候选版源码、历史、凭据与第三方许可审计 |
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

`v0.2.0-alpha.1` is currently a local release candidate, not a published
GitHub Release. Its EditMode, PlayMode, clean WebGL build, and local-browser
evidence are recorded in
[docs/releases/v0.2.0-alpha.1.md](docs/releases/v0.2.0-alpha.1.md). The live
WebGL badge continues to point to the previously deployed build until this
candidate is deliberately published.

The previous `v0.1.0` release was withdrawn during asset-provenance cleanup;
its historical record remains in
[docs/releases/v0.1.0.md](docs/releases/v0.1.0.md). Generated builds, caches,
test-result XML, and the Unity `Library` directory are intentionally excluded
from source control. Only tested user-facing packages and their checksums
belong on GitHub Releases.
