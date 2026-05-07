# 开发计划

这份文档只记录阶段计划、验收点和执行顺序。玩法规则和架构判断放在 `GAMEPLAY_DESIGN.md`，避免两个文档重复解释同一件事。

## 工作流规则

这个样例的功能开发按以下顺序推进：

1. 更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`。
2. 更新本开发计划。
3. 实现代码改动。
4. 验证被影响的运行路径。

小改动可以让设计说明和计划说明保持简短，但只要行为或架构发生变化，就应该同步更新这两个核心文档。

## 当前基线

仓库已经不再是最早的单进程场地样例，当前基线包括：

- `Shared/Gameplay/ArenaSimulation.cs` 中的共享小球吞噬玩法模拟。
- 单机和联机共用同一套模拟逻辑。
- 质量、半径、速度之间的成长循环。
- 食物刷新、玩家吞噬、死亡、复活、单局计时和排名。
- Unity 场景和脚本作为工作流来源，不再恢复编辑器侧自动烘焙玩法场景的做法。
- 控制面的 WebSocket RPC。
- 实时面的 KCP RPC。
- 用于实时会话绑定的 `AttachRealtimeAsync`。
- 基于 PostgreSQL 的 Orleans 集群成员表和 grain 持久化。
- 基于 Orleans 的匹配队列。
- 持久化的房间和会话分配，包含运行时网关端点信息。
- 支持运行时网关绑定的仅实时连接本地会话注册。
- 包含 PostgreSQL 和 Redis 的本地 compose 基线。
- `DisconnectedSessionCleanupHostedService` 用于后台清理过期会话。
- `DotArenaMetaProgression` 已按关注点拆分为 Models、Catalog、Persistence、Queries、Rules 五个 partial 文件。
- 客户端已有本地排行榜 UI（`GetLeaderboardSummary`），但数据为本地 mock 假条目，未连接服务端。

已经从当前计划中移除的方向：

- 把本地内存 grain 存储作为目标服务端状态模型。
- 把只限本机的 Orleans 集群作为目标部署模型。
- 把网关本地匹配队列作为目标匹配队列模型。
- 把旧的击退、冲刺、强化战斗循环作为目标玩法模型。
- 为客户端架构、生产基础设施和网关重构继续维护分散的一次性文档。

## 已知技术债务

以下问题存在于当前代码库中，需要在对应阶段处理：

### 协议残留

- `InputMessage.Dash` 字段在协议中定义、客户端填充、服务端传输，但 `ArenaSimulation.SubmitInput` 从不读取它。客户端 `_dashQueued` 始终为 `false`（无按键绑定），`PlayerLifeState.Dash = 2` 枚举值从未被 `GetLifeState()` 返回。`FocusGameViewOnPlay.cs` 中有仅编辑器使用的 dash 桥接代码。
- `PickupType.SpeedBoost`、`KnockbackBoost`、`Shield`、`BonusScore` 在 `ArenaSimulationOptions.EnabledPickupTypes` 默认值中全部启用，食物会生成这些类型，但 `ConsumeFood` 对所有类型执行相同逻辑（统一加 1 分）。`PlayerState` 中的 `SpeedBoostRemainingSeconds`、`KnockbackBoostRemainingSeconds`、`ShieldRemainingSeconds` 字段始终发布为 `0`。客户端 `DotArenaPresentation.GetPickupDisplayName` 对所有类型返回 "Mass"。
- `PlayerLifeState.Stunned = 3` 枚举值定义但从未被使用。
- `ArenaMapVariant` 和 `ArenaRuleVariant` 枚举各有多个值，但实际只实现了 `ClassicSquare` / `ClassicElimination` 一条路径。

### 客户端结构

- `DotArenaGame.cs` 有 93 个实例字段，分散在 12 个 partial 文件中。`UiSurface`、`UiActions`、`Presentation`、`Views` 之间的职责边界不清晰。
- `DotArenaSceneUiPresenter` 仍偏大，负责运行时控件构建（`Layout.cs`、`Layout2.cs`）、刷新（`Refresh.cs`）、样式（`Styling.cs`）、大厅（`Lobby.cs`）。
- `DotArenaMetaProgression` 的拆分已完成（Models、Catalog、Persistence、Queries、Rules），本计划不再对其安排额外拆分工作。

### 测试缺口

当前仅有 9 个自动化测试（`ArenaSimulationRulesTests` 5 个，`MatchmakingQueuePolicyTests` 4 个）。缺少覆盖：网络会话生命周期、房间运行时行为、网关清理语义、客户端流程状态。

## 活跃待办

### 阶段 1：玩法回归与协议清理

任务：

- 在 Unity 编辑器中验证单机启动、食物成长、玩家吞噬、死亡、复活和结算全流程。
- 在 Silo 和 Server 运行时，验证联机登录、匹配、实时绑定、输入、世界快照和结算全流程。
- 检查 HUD 文案中是否还残留冲刺、强化、眩晕等旧玩法语义。发现则替换为质量、排名、成长、存活等当前语义。
- 从 `InputMessage` 中移除 `Dash` 字段（序号重排为 0-3），同步更新 `IPlayerService.cs`、`DotArenaGame.Input.cs`、`DotArenaGame.Views.cs`、`DotArenaGame.SinglePlayer.cs`、`RpcConnectionTester.cs` 和 `FocusGameViewOnPlay.cs` 中所有引用。
- 从 `PlayerLifeState` 枚举中移除 `Dash = 2` 和 `Stunned = 3`，重新编号 `Dead = 2`。
- 从 `ArenaSimulationOptions.EnabledPickupTypes` 默认值中移除 `SpeedBoost`、`KnockbackBoost`、`Shield`、`BonusScore`，只保留 `ScorePoint`。同步清理 `PickupType` 枚举中的未使用值。
- 从 `PlayerState` 中移除 `SpeedBoostRemainingSeconds`、`KnockbackBoostRemainingSeconds`、`ShieldRemainingSeconds` 字段，同步更新 `CreateWorldState()`、`DotArenaCallbackInbox.cs`、`DotArenaWorldSynchronizer.cs`、`DotArenaGame.Types.cs`、`DotArenaTuning.cs`、`DotArenaPresentation.cs` 和 `DotArenaSinglePlayerCatalog.cs` 中所有引用。
- 考虑是否移除 `ArenaMapVariant` / `ArenaRuleVariant` 枚举（如果短期内不会有第二个实现），或将对应 `_currentArenaMapVariant` / `_currentArenaRuleVariant` 字段替换为简单的字符串常量。
- 检查 Unity 刷新项目后生成的 `.csproj` 和包引用是否能正确带上 KCP。
- 创建 `samples/Agar.Unity/CLAUDE.md`，记录项目入口、关键文件路径和开发工作流规则。

验收标准：

- 本地单机对局可以完整游玩到结束。
- 联机对局可以通过匹配开始，并持续收到实时世界快照。
- 可见 UI 路径不再依赖已移除的旧玩法概念。
- `InputMessage` 协议不含 `Dash` 字段，客户端和服务端代码均无 dash 相关逻辑。
- 食物拾取类型只有 `ScorePoint`，所有拾取行为统一为加分。
- 生成的 RPC 代码与协议变更一致（重新生成后编译通过）。
- `CLAUDE.md` 文件存在且内容与当前代码一致。

### 阶段 2：客户端拆分

任务：

- 从 `DotArenaGame` 中拆出对局流程职责，将 `UiSurface` / `UiActions` 中的状态切换逻辑收敛到显式的状态机或流程编排方法中。
- 保持 RPC 生命周期由 `DotArenaNetworkSession` 管理。
- 保持回调缓存和主线程消化由 `DotArenaCallbackInbox` 管理。
- 将 `DotArenaSceneUiPresenter` 中重复的 UI 构建逻辑（`Layout.cs`、`Layout2.cs`、`Styling.cs`）移动到更小的辅助类或 Unity 预制件/资源中。
- `DotArenaMetaProgression` 的 Models / Catalog / Persistence / Queries / Rules 拆分已完成，本轮仅对方法签名做必要的收尾调整，不再安排大范围重写。

验收标准：

- 模式入口、匹配、对局中和结算状态切换更容易推理。
- 不引入 `SampleClient.Rpc` 和 `SampleClient.Gameplay` 之间的循环依赖。
- 生成的 RPC 代码保持不被手写修改。
- `DotArenaSceneUiPresenter` 的运行时控件构建逻辑减少至少 30%（以行数计）。

### 阶段 3：网关清理语义

任务：

- 审核 `PlayerService.cs`、`SessionRegistration.cs`、`SessionDirectory.cs`、`DisconnectedSessionCleanupHostedService.cs` 中的登出、断线、取消匹配、离开房间和对局结束清理路径。
- 确认控制连接和实时连接可以独立解绑（解绑实时连接不意外断开控制连接，反之亦然）。
- 确认 `IPlayerSessionGrain`、`IRoomGrain` 和 `SessionDirectory` 本地回调状态在失败后能收敛（重复登出不残留、匹配取消后票据不堆积）。
- 确认 `RoomRuntime.RemovePlayerAsync` 返回 `remaining == 0` 后，`RoomRuntimeHost` 或调用方正确释放房间资源。
- 为登出、断线和重复匹配流程增加聚焦测试，或者留下可追踪的手动验证步骤文档。

验收标准：

- 过期匹配票据会被清理。
- 过期实时回调会被移除。
- 房间运行时会在断线或离房后移除玩家。
- 重复登录、登出、匹配和取消匹配不会留下重复的本地注册。
- `DisconnectedSessionCleanupHostedService` 的后台清理周期性运行且日志可追踪。

### 阶段 4：胜利积分与排行榜

任务：

服务端：

- 在 `Orleans.Contracts/Users/IUserGrain.cs` 中增加 `AddVictoryPointsAsync(int points)` 方法，在 `UserLoginResult` 和 `UserProfileSnapshot` 中增加 `VictoryPoints` 字段。
- 在 `Silo/Users/UserGrain.cs` 的 `UserState` 中增加 `VictoryPoints` 持久化字段（Orleans `[Id(9)]`），实现 `AddVictoryPointsAsync`。
- 新建 `Server/Orleans.Contracts/Leaderboard/ILeaderboardGrain.cs`：定义 `GetLeaderboardAsync(int topN)` 返回排行榜列表、`ResetWeeklyIfNeededAsync()` 触发周期重置。排行榜条目包含 `PlayerId`、`VictoryPoints`、`WinCount`、`Rank`。
- 新建 `Server/Silo/Leaderboard/LeaderboardGrain.cs`：实现 `ILeaderboardGrain`（`IGrainWithIntegerKey`，固定 key=0，单实例），负责：
  - 聚合全部用户 grain 的胜利积分（分页读取避免一次性加载过多用户）。
  - 按积分降序、胜场降序、玩家标识升序排序，返回 top N。
  - 记录当前周期标识（`yyyy-MM-dd` 格式的周一日期），每次查询时检查是否已过周一 UTC 00:00，若是则先执行重置再返回新周期空排行榜。
  - 重置时：归档上周 top 100 到 `WeeklySnapshot`（只保留最近两周），将所有用户的 `VictoryPoints` 归零。
- 在 `RoomRuntime.PersistMatchEndAsync` 中，对局结算时根据 `RoomSettlementEntry.Rank` 发放胜利积分：
  - 排名 1→10 分、2→7 分、3→5 分、4→3 分、5→1 分，其余 0 分。
  - 过滤 AI 玩家（以 `BotPrefix` 开头），不发放积分。
- 在 `Shared/Interfaces/IPlayerService.cs` 中新增 `GetLeaderboardAsync` RPC 方法和请求/回复类型。
- 在 `Server/Services/PlayerService.cs` 中实现排行榜查询，转发到 `ILeaderboardGrain`。

客户端：

- 在 `DotArenaMetaProgression.Queries.cs` 中将 `GetLeaderboardSummary` 从本地 mock 替换为 RPC 调用。
- 淘汰 `DotArenaLeaderboardSummary` 中的本地假条目（`Queue Rival`、`Arena Veteran`），替换为服务端返回的真实排行榜数据。
- 排行榜 UI（`DotArenaSceneUiPresenter.Lobby.cs` 中的排行榜面板）展示当前周期剩余时间。

验收标准：

- 联机对局结束后，玩家按排名获得对应胜利积分（1st=10, 2nd=7, 3rd=5, 4th=3, 5th=1）。
- AI 玩家不获得胜利积分。
- 客户端排行榜展示真实的全局玩家排名。
- 日志可追踪积分发放和排行榜查询。
- 排行榜在周一 UTC 00:00 后首次查询时自动重置，上周数据归档可查。
- 单机模式不发放胜利积分。

### 阶段 5：跨网关实时路由设计

任务：

- 选择网关到网关的事件机制：Orleans stream、Orleans observer 或 Redis 发布订阅。
- 定义输入转发、世界状态扇出、断线事件和背压的所有权。
- 定义顺序、重试和运行时所有者过期时的行为。
- 实现前先更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`。

验收标准：

- 控制连接在网关 A 的玩家，可以把输入送到网关 B 上的房间运行时。
- 对局事件可以送回正确的在线客户端连接。
- 失败行为足够明确，可以编写测试或手动验证。

### 阶段 6：跨网关实时路由实现

任务：

- 实现阶段 5 选定的路由层。
- 保持房间模拟在同一时刻只由一个运行时所有者权威推进。
- 不把实时回调对象序列化到 Redis 或 Orleans 状态中。
- 增加路由决策和投递失败日志。

验收标准：

- 多网关部署可以处理控制连接、实时连接和房间运行时位于不同网关的情况。
- 世界状态广播可以到达连接在不同网关节点上的玩家。
- 断线、登出和离房清理在跨网关所有权下仍然正确。

### 阶段 7：测试扩展

任务：

- 为 `RoomRuntime` 增加测试：玩家加入/离开、输入提交、对局结束结算、空房间清理。
- 为 `SessionDirectory` / `SessionRegistration` 增加测试：注册、解绑、房间查询、回调获取。
- 为 `DotArenaNetworkSession` 增加测试：连接生命周期、重连参数、实时绑定。
- 为 `ArenaSimulation` 补充测试：食物刷新边界、吞噬比例边界、AI 补位、多人同时死亡。
- 为 `DotArenaMetaProgression` 增加测试：经验升级边界、每日任务重置、首胜断言。
- 为 `LeaderboardGrain` 增加测试：积分排序、周重置触发、AI 过滤、归档保留。

验收标准：

- 自动化测试数量从 9 个增加到至少 30 个。
- 核心业务逻辑路径（模拟、匹配、房间、会话）有可运行的测试覆盖。

### 阶段 8：验证与打包

任务：

- 构建 `Shared/Shared.csproj`。
- 构建 `Server/Silo/Silo.csproj`。
- 构建 `Server/Server/Server.csproj`。
- 运行已有自动化测试和新增测试。
- Unity 可用时，手动冒烟测试单机和联机流程。
- 当命令、端口或架构事实变化时，同步 README 和 `CLAUDE.md`。

验收标准：

- 被触碰的项目可以编译。
- 所有自动化测试通过。
- 文档中的运行说明和当前代码一致。
- 不再引用已经删除的文档。
