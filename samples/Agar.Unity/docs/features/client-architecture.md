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
