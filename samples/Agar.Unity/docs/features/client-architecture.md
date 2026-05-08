# 客户端架构

这份文档描述 Unity 客户端的职责边界、关键代码位置、依赖方向、已知重构目标和表现原则。核心玩法见 [`gameplay-rules.md`](gameplay-rules.md)，服务端架构见 [`server-architecture.md`](server-architecture.md)。

## 客户端边界

Unity 客户端负责：

- 启动菜单和模式切换。
- 本地单机模拟。
- 联机输入发送。
- 世界状态接收和渲染。
- HUD、玩家表现、食物表现和结算界面。
- 控制连接、实时连接、断线处理和可靠推送确认。
- 本地元进度展示和存储。
- 排行榜拉取和展示。

重要代码位置：

- `Client/Assets/Scenes/Gameplay.unity`：场景来源。
- `Client/Assets/Scripts/Rpc`：传输创建和生成的 RPC 访问入口。
- `Client/Assets/Scripts/Gameplay`：主流程、同步、视图、HUD 和本地进度。

依赖方向：

```txt
Shared -> ULinkRPC.Generated -> SampleClient.Rpc -> SampleClient.Gameplay
```

`Gameplay` 可以依赖 RPC 辅助代码；RPC 辅助代码不能反向依赖玩法界面代码。

## Prefab 使用策略

当前 Unity 客户端刻意少用 prefab。`Client/Assets/Prefabs` 里只保留少量 placeholder prefab；实际玩家视图、食物视图、HUD、启动菜单、匹配面板、大厅和结算界面主要由 `Client/Assets/Scripts/Gameplay` 下的运行时代码创建和刷新。

对当前样例阶段，这个选择有利于开发效率：

- 运行时对象结构和网络状态流集中在代码中，便于 review、diff 和复制到其他样例。
- 单机和联机都依赖动态世界状态，玩家、食物、结算和排行榜界面会随会话状态频繁重建或刷新，用代码工厂更容易保持确定性。
- 示例项目对跨机器打开、CI 构建和协议演示的要求高，少量编辑器侧手工引用可以降低场景、prefab 或 Inspector 引用损坏的概率。
- 玩法表现仍可接入正式 Sprite、材质和字体资源；少 prefab 不等于少资源，而是把对象装配和状态绑定放在代码侧。

长期维护上，完全依赖运行时 `new GameObject` / `AddComponent` 也会累积风险：

- UI 尺寸、位置、字体、颜色和层级散落在 C# 中，视觉调试效率低。
- 美术、设计或非程序成员难以直接在 Unity 编辑器中调整稳定结构。
- 稳定组件的层级在运行前不可见，复杂界面增长后不利于理解和回归。
- 按钮、面板、玩家视图、拾取物视图和常用特效这类稳定结构如果长期全部写在代码里，会增加重复和改动面。

因此后续维护应采用混合策略：运行时变化和网络逻辑继续留在代码里，稳定视觉结构逐步沉淀为 prefab 或可复用资源。

适合继续由代码创建的内容：

- 服务端世界状态驱动的玩家、食物、临时特效和对象池。
- 网络会话状态机、输入发送、世界同步和 UI 状态刷新。
- 纯逻辑 GameObject、一次性占位对象和由数据表驱动的轻量实体。

适合逐步引入 prefab 的内容：

- 稳定 UI 面板、按钮模板、输入框模板、HUD/Overlay 模板。
- 玩家 View 根节点、Pickup View 根节点和固定子节点组合。
- 常用 FX 组合、九宫格 UI 基础件和需要编辑器调参的视觉结构。

阶段 2 客户端拆分时，可以优先从 `DotArenaSceneUiPresenter` 中抽出重复 UI 构建逻辑，先把稳定控件模板资源化，再保留现有代码作为状态绑定和刷新入口。目标不是把客户端改成全 prefab，而是减少运行时控件构建代码，让 prefab 承担稳定视觉结构，代码承担动态状态和网络行为。

## 已知重构目标

- `DotArenaGame.cs` 有 93 个实例字段，分散在 12 个 partial 文件中。`UiSurface`、`UiActions`、`Presentation`、`Views` 之间的职责边界不清晰。
- `DotArenaSceneUiPresenter` 仍偏大，负责运行时控件构建（`Layout.cs`、`Layout2.cs`）、刷新（`Refresh.cs`）、样式（`Styling.cs`）、大厅（`Lobby.cs`）。
- `DotArenaMetaProgression` 已拆分完成（Models、Catalog、Persistence、Queries、Rules）。本地 mock 排行榜已淘汰，`GetLeaderboardSummary` 现在展示服务端 RPC 刷新后的缓存数据。
- 计划中的拆分工作见 `DEVELOPMENT_PLAN.md` 阶段 2。

## 表现原则

- 玩家显示大小跟随 `Radius`。
- HUD 强调名字、质量、排名和存活状态。
- 食物要小且数量多。
- 界面文案使用质量、排名、成长和存活语义，不使用冲刺或技能强化作为核心表达。
- 玩家碰撞和成长可以有果冻感表现，但眩晕、击退不应被当作当前核心玩法。
- 排行榜界面展示当前周期剩余时间、玩家排名、胜利积分和胜场数。
- 当前美术接入优先使用 `Client/Assets/Art` 下已导入 Sprite：玩家皮肤、质量拾取物、竞技场背景、UI 面板/按钮、排行榜图标和吸收/出生 FX 都由 Gameplay 运行时视图按需挂载，缺失资产时回退到脚本生成的占位 sprite。未上线的任务、商店和记录页面不属于当前玩家 UI；相关后台模型暂时保留，等待后续主线程评估。
