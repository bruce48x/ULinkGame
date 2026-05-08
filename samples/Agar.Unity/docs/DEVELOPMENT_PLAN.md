# 开发计划

这份文档只记录阶段计划、验收点和执行顺序。玩法规则和架构判断放在 `GAMEPLAY_DESIGN.md`，避免两个文档重复解释同一件事。

## 工作流规则

这个样例的功能开发按以下顺序推进：

1. 更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`、`ART_DIRECTION.md` 以及 `docs/features/` 下对应的功能子文档。
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
- 客户端排行榜 UI 已通过 `IPlayerService.GetLeaderboardAsync` 连接服务端真实排行榜数据。
- `InputMessage` 只包含玩家、移动方向和 tick，不再包含 dash。
- `PickupType` 当前只保留 `ScorePoint`，食物行为统一为加分和质量成长。
- `PlayerState` 只发布位置、速度、生死、分数、质量、半径和移动速度，不再发布旧强化剩余时间。
- `IUserGrain` 已持久化 `VictoryPoints`，`ILeaderboardGrain` 已提供周榜查询、周期重置和最近两周归档。
- 排行榜周期重置已按榜单当地时间周一 00:00 计算，旧 `PeriodStartUtc` 字段仅作为兼容字段保留。
- `RoomRuntime.PersistMatchEndAsync` 已按排名发放胜利积分，AI 玩家不获得积分。
- 自动化测试当前为 20 个（`ArenaSimulationRulesTests` 16 个，`MatchmakingQueuePolicyTests` 4 个）。
- `samples/Agar.Unity/CLAUDE.md` 已记录入口文件、生成命令和开发工作流。
- `samples/Agar.Unity/docs/ART_DIRECTION.md` 已定义整体美术、UI 设计、素材生成、Unity 接入和验收标准。

已经从当前计划中移除的方向：

- 把本地内存 grain 存储作为目标服务端状态模型。
- 把只限本机的 Orleans 集群作为目标部署模型。
- 把网关本地匹配队列作为目标匹配队列模型。
- 把旧的击退、冲刺、强化战斗循环作为目标玩法模型。
- 为客户端架构、生产基础设施和网关重构继续维护分散的一次性文档。

## 已知技术债务

以下问题存在于当前代码库中，需要在对应阶段处理：

### 协议与玩法变体

- `ArenaMapVariant` 和 `ArenaRuleVariant` 枚举各有多个值；当前代码已有多个单机预设，但联机仍只走默认共享规则。后续如果不会继续扩展联机规则变体，可以把联机侧字段收敛为简单常量。
- RPC 生成代码需要在共享协议变更后重新生成，Unity 客户端命名空间必须保持为 `Rpc`，否则会遮蔽手写的 `WebSocketRpcClientFactory` / `KcpRpcClientFactory`。

### 客户端结构

- `DotArenaGame.cs` 有 93 个实例字段，分散在 12 个 partial 文件中。`UiSurface`、`UiActions`、`Presentation`、`Views` 之间的职责边界不清晰。
- `DotArenaSceneUiPresenter` 仍偏大，负责运行时控件构建（`Layout.cs`、`Layout2.cs`）、刷新（`Refresh.cs`）、样式（`Styling.cs`）、大厅（`Lobby.cs`）。
- `DotArenaMetaProgression` 的拆分已完成（Models、Catalog、Persistence、Queries、Rules），本计划不再对其安排额外拆分工作。

### 测试缺口

当前已有 20 个自动化测试。仍缺少覆盖：网络会话生命周期、房间运行时行为、网关清理语义、客户端流程状态，以及 `LeaderboardGrain` 的持久化周期重置路径。

## 活跃待办

### 阶段 0A：启动界面视觉修复（当前最高优先级）

任务：

- 优先修复 `samples/Agar.Unity` 启动界面缺少正式背景图的问题：入口、匹配、大厅和结算这些非对局 UI 状态必须显示 `BG_Arena_Grid_Dark_01.png` 作为全屏背景；进入实际对局时隐藏该 UI 背景，避免遮挡游戏世界。
- 优先校正入口/模式选择面板的按钮布局：按钮宽度、高度、垂直间距、文字填充区域和面板视觉重心需要统一，避免截图中按钮偏窄、间距松散、底部贴边的问题。
- 先完成这两个启动体验问题，再继续扩大 AI 美术资产生成和大厅复杂 UI 改造范围。

验收标准：

- 启动到模式选择界面时，首屏能看到正式深色竞技场背景图，而不是只依赖相机清屏色或运行时占位网格。
- 三个模式按钮在同一中轴线上，尺寸一致，垂直间距一致，文字居中且不溢出。
- 入口面板在 1200x600 和 960x540 下视觉重心稳定，不遮挡背景关键纹理，也不出现按钮贴边。
- 进入实际对局后，全屏菜单背景不覆盖游戏世界。

### 阶段 0：AI 生成美术资产（阶段 0A 完成后继续）

任务：

- 以 [`ART_DIRECTION.md`](ART_DIRECTION.md) 作为整体美术、UI 生产和接入基准。
- 美术资产推进原则：先用小型可运行基准包验证方向，不先生成完整资产库，也不在 mockup 阶段停留过久。
- 非 UI 资产第一步只处理玩家球图片，不同时推进食物、场地和特效。
- 生成并筛选第一批玩家果冻皮肤图片，建议先做 3 张：
  - `Skin_Jelly_Cyan.png`
  - `Skin_Jelly_Crimson.png`
  - `Skin_Jelly_Sunburst.png`
- 将通过筛选的玩家球图片放入 `Client/Assets/Art/Sprites/Skins`，保持 512x512、透明背景、边缘干净、无文字水印。
- 在 Unity 客户端中接入玩家球图片，优先替换运行时生成的纯色圆形玩家表现或 `PlayerPlaceholder` 相关占位表现。
- 在单机游戏视图中验证玩家球图片的实际效果，包括：本地玩家识别度、敌方玩家区分度、不同体型缩放效果、名称和分数遮挡情况、深色背景下的边缘可读性。
- 用户看过玩家球效果并确认方向后，再进入下一步资产生成。后续候选顺序为：质量拾取物、竞技场背景、UI 基准图与最小 UI 素材包、吸收和复活特效。

当前玩家球方向已通过单机初验，已进入第一轮基础美术资产批量生成。已完成第一轮脚本侧运行时接入：玩家球皮肤、质量拾取物、竞技场背景、入口/匹配/大厅/结算按钮基准图、商店/排行榜图标、吸收环和出生波纹已经接入运行时创建的表现对象。所有资产仍需要先由 MCP for Unity 完成导入和基础运行路径验证，再交给用户手动验收：

1. 质量拾取物：
   - `Pickup_Mass_Teal_01.png`
   - `Pickup_Mass_Gold_01.png`
   - 目标：替换或辅助当前纯色拾取物表现，保证小尺寸下仍可读。
2. 竞技场背景：
   - `BG_Arena_Grid_Dark_01.png`
   - 目标：作为深色细胞网格/地面纹理基准，验证是否影响玩家球和拾取物识别。
3. UI 基础件：
   - `UI_Panel_Dark_01.png`
   - `UI_Button_Primary_Normal.png`
   - `UI_Button_Primary_Pressed.png`
   - 目标：形成大厅、匹配、结算面板和按钮的第一版风格基准。
4. 大厅图标：
   - `Icon_Leaderboard_01.png`
   - `Icon_Shop_01.png`
   - 目标：验证图标线条风格和小尺寸识别度，后续再扩展任务、记录、设置等图标。
5. 特效原型：
   - `FX_Absorb_Ring_01.png`
   - `FX_Spawn_Wave_01.png`
   - 目标：作为吸收和复活反馈的 Sprite 原型，后续再决定是否扩展为序列帧或 ParticleSystem。

UI 推进流程：

1. 先生成 1 张 UI 风格基准图，不直接进入 Unity：
   - 同一张图中包含入口/模式选择、大厅、游戏内 HUD、匹配中、结算的代表片段。
   - 目标是确认面板、按钮、图标、字体层级、青色/柔金强调色和深色竞技场背景是否统一。
   - 这张图只作为视觉对齐参考，不作为正式游戏资产。
2. 基准图通过后，生成最小 UI 素材包：
   - 面板：1 到 2 张可九宫格切片的深色半透明面板。
   - 按钮：Primary / Secondary / Danger，各包含 Normal、Hover 或 Highlighted、Pressed、Disabled。
   - 输入框：Normal、Focused、Error。
   - 图标：单机、联机、任务、商店、排行榜、设置、金币、质量、时间。
   - HUD：紧凑背板和排行榜行。
3. 先接入三个关键界面验证：
   - 入口/模式选择。
   - 匹配中。
   - 结算。
   - 目标是验证按钮状态、面板切片、字体可读性、异步状态反馈和整体风格，而不是一次性重做所有 UI。
4. 进入 Unity 实机检查：
   - 1200x600 和 960x540 下文字不溢出。
   - 九宫格拉伸不变形。
   - 按钮 Normal / Highlighted / Pressed / Disabled 状态清楚。
   - HUD 不遮挡玩法中心、玩家名和分数。
   - UI 在深色竞技场中不过亮、不杂乱。
5. 基础组件通过后，再扩展大厅、任务、商店、记录、排行榜和设置：
   - 这些界面信息密度更高，应基于已验证的基础面板、按钮、图标和列表行扩展。
   - 不允许先生成一批无法接入的完整静态界面截图作为正式产物。

验收标准：

- `samples/Agar.Unity/docs/ART_DIRECTION.md` 已作为后续资产生成、UI 设计、AI 生成提示词、Unity 接入和验收的唯一风格标准。
- 第一批玩家球图片进入 Unity 项目目录，并符合文档中的目录、命名和规格要求。
- 单机游戏视图中可以看到明显优于占位资源的玩家球表现。
- 新玩家球不会降低玩法判断效率，不遮挡玩家名、分数、HUD 和排行榜信息。
- 玩家球图片不包含文字、水印、签名、明显瑕疵或当前玩法没有定义的旧概念。
- 只有当用户确认玩家球在实际游戏画面中的方向成立后，才继续生成下一类资产。
- UI 先有一张风格基准图完成方向确认，再生成最小 UI 素材包。
- 最小 UI 素材包至少接入入口/模式选择、匹配中、结算三个关键界面，并通过 Unity 游戏视图检查。

### 阶段 1：玩法回归与协议清理（基本完成，剩余手动回归）

任务：

- 在 Unity 编辑器中验证单机启动、食物成长、玩家吞噬、死亡、复活和结算全流程。（待手动完整回归）
- 在 Silo 和 Server 运行时，验证联机登录、匹配、实时绑定、输入、世界快照和结算全流程。（待手动完整回归）
- 检查 HUD 文案中是否还残留冲刺、强化、眩晕等旧玩法语义。发现则替换为质量、排名、成长、存活等当前语义。（已完成代码扫描和 Unity 编译检查）
- 从 `InputMessage` 中移除 `Dash` 字段（序号重排为 0-3），同步更新 `IPlayerService.cs`、`DotArenaGame.Input.cs`、`DotArenaGame.Views.cs`、`DotArenaGame.SinglePlayer.cs`、`RpcConnectionTester.cs` 和 `FocusGameViewOnPlay.cs` 中所有引用。（已完成）
- 从 `PlayerLifeState` 枚举中移除 `Dash = 2` 和 `Stunned = 3`，重新编号 `Dead = 2`。（已完成）
- 从 `ArenaSimulationOptions.EnabledPickupTypes` 默认值中移除 `SpeedBoost`、`KnockbackBoost`、`Shield`、`BonusScore`，只保留 `ScorePoint`。同步清理 `PickupType` 枚举中的未使用值。（已完成）
- 从 `PlayerState` 中移除 `SpeedBoostRemainingSeconds`、`KnockbackBoostRemainingSeconds`、`ShieldRemainingSeconds` 字段，同步更新 `CreateWorldState()`、`DotArenaCallbackInbox.cs`、`DotArenaWorldSynchronizer.cs`、`DotArenaGame.Types.cs`、`DotArenaTuning.cs`、`DotArenaPresentation.cs` 和 `DotArenaSinglePlayerCatalog.cs` 中所有引用。（已完成）
- 考虑是否移除 `ArenaMapVariant` / `ArenaRuleVariant` 枚举（如果短期内不会有第二个实现），或将对应 `_currentArenaMapVariant` / `_currentArenaRuleVariant` 字段替换为简单的字符串常量。（保留：单机预设仍使用这些枚举）
- 检查 Unity 刷新项目后生成的 `.csproj` 和包引用是否能正确带上 KCP。（Unity 脚本刷新通过；KCP factory 引用已保持）
- 创建 `samples/Agar.Unity/CLAUDE.md`，记录项目入口、关键文件路径和开发工作流规则。（已完成）

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

### 阶段 4：胜利积分与排行榜（代码完成，剩余联机实机回归）

任务：

服务端：

- 在 `Orleans.Contracts/Users/IUserGrain.cs` 中增加 `AddVictoryPointsAsync(int points)` 方法，在 `UserLoginResult` 和 `UserProfileSnapshot` 中增加 `VictoryPoints` 字段。（已完成）
- 在 `Silo/Users/UserGrain.cs` 的 `UserState` 中增加 `VictoryPoints` 持久化字段（Orleans `[Id(9)]`），实现 `AddVictoryPointsAsync`。（已完成）
- 新建 `Server/Orleans.Contracts/Leaderboard/ILeaderboardGrain.cs`：定义 `GetLeaderboardAsync(int topN)` 返回排行榜列表、`ResetWeeklyIfNeededAsync()` 触发周期重置。排行榜条目包含 `PlayerId`、`VictoryPoints`、`WinCount`、`Rank`。（已完成）
- 新建 `Server/Silo/Leaderboard/LeaderboardGrain.cs`：实现 `ILeaderboardGrain`（`IGrainWithIntegerKey`，固定 key=0，单实例），负责：
  - 维护由对局结算写入的排行榜积分索引，避免当前缺少全量用户目录时做不可控全库扫描。（已完成）
  - 按积分降序、胜场降序、玩家标识升序排序，返回 top N。（已完成）
  - 记录当前周期标识（`yyyy-MM-dd` 格式的榜单当地周一日期），每次查询或写入时检查是否已过榜单当地时间周一 00:00，若是则先执行重置。（已完成）
  - 重置时：归档上周 top 100 到 `WeeklySnapshot`（只保留最近两周），将索引内用户的 `VictoryPoints` 归零。（已完成；未来如新增全量用户目录需扩展为全用户重置）
- 将 `LeaderboardGrain` 的周期计算从 UTC 周一 00:00 改为榜单当地时间周一 00:00：
  - 增加或集中定义榜单时区配置，当前样例默认使用目标玩家/部署地区的本地时区。（已完成：`LeaderboardGrain` 使用 `TimeZoneInfo.Local`）
  - 服务端可继续使用 UTC 存储绝对时间戳，但 `CurrentPeriodStart`、`SecondsUntilReset` 和归档周期标识必须按榜单当地时区计算。（已完成）
  - 如果未来支持多地区排行榜，每个地区榜独立保存时区和周期。
- 调整接口和客户端文案中 `PeriodStartUtc` 这类命名，避免把体验口径继续表达为 UTC；如需兼容旧字段，应在计划中明确迁移策略。（服务端合同已新增 `PeriodStartLocalDate`，旧 `PeriodStartUtc` 暂保留并写入相同本地日期；客户端字段迁移留到客户端允许改动的阶段）
- 在 `RoomRuntime.PersistMatchEndAsync` 中，对局结算时根据排名发放胜利积分：
  - 排名 1→10 分、2→7 分、3→5 分、4→3 分、5→1 分，其余 0 分。（已完成）
  - 过滤 AI 玩家（以 `AI` 前缀开头），不发放积分。（已完成）
- 在 `Shared/Interfaces/IPlayerService.cs` 中新增 `GetLeaderboardAsync` RPC 方法和请求/回复类型。（已完成）
- 在 `Server/Services/PlayerService.cs` 中实现排行榜查询，转发到 `ILeaderboardGrain`。（已完成）

客户端：

- 在 `DotArenaMetaProgression.Queries.cs` 中将 `GetLeaderboardSummary` 从本地 mock 替换为服务端数据缓存展示；RPC 拉取由 `DotArenaNetworkSession.GetLeaderboardAsync` 和 `DotArenaGame.RefreshLeaderboardAsync` 负责。（已完成）
- 淘汰 `DotArenaLeaderboardSummary` 中的本地假条目（`Queue Rival`、`Arena Veteran`），替换为服务端返回的真实排行榜数据。（已完成）
- 排行榜 UI（`DotArenaSceneUiPresenter.Lobby.cs` 中的排行榜面板）展示当前周期剩余时间。（已完成）

验收标准：

- 联机对局结束后，玩家按排名获得对应胜利积分（1st=10, 2nd=7, 3rd=5, 4th=3, 5th=1）。
- AI 玩家不获得胜利积分。
- 客户端排行榜展示真实的全局玩家排名。
- 日志可追踪积分发放和排行榜查询。
- 排行榜在榜单当地时间周一 00:00 后首次查询时自动重置，上周数据归档可查。
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

### 阶段 7：测试扩展（部分完成）

任务：

- 为 `RoomRuntime` 增加测试：玩家加入/离开、输入提交、对局结束结算、空房间清理。
- 为 `SessionDirectory` / `SessionRegistration` 增加测试：注册、解绑、房间查询、回调获取。
- 为 `DotArenaNetworkSession` 增加测试：连接生命周期、重连参数、实时绑定。
- 为 `ArenaSimulation` 补充测试：食物刷新边界、吞噬比例边界、AI 补位、多人同时死亡。
- 为 `DotArenaMetaProgression` 增加测试：经验升级边界、每日任务重置、首胜断言。
- 为 `LeaderboardGrain` 增加测试：积分排序、周重置触发、AI 过滤、归档保留。（已覆盖排序、当地时区周重置和归档保留；AI 过滤由 `VictoryPointAwards` 测试覆盖）

验收标准：

- 自动化测试数量从 9 个增加到至少 30 个。（当前 20 个）
- 核心业务逻辑路径（模拟、匹配、房间、会话）有可运行的测试覆盖。

### 阶段 8：验证与打包（本轮已验证）

任务：

- 构建 `Shared/Shared.csproj`。（已通过）
- 构建 `Server/Silo/Silo.csproj`。（已通过）
- 构建 `Server/Server/Server.csproj`。（已通过）
- 运行已有自动化测试和新增测试。（已通过，20/20）
- Unity 可用时，手动冒烟测试单机和联机流程。（Unity 脚本刷新和控制台错误检查已通过；完整手动游玩仍待回归）
- 当命令、端口或架构事实变化时，同步 README 和 `CLAUDE.md`。（已同步）

验收标准：

- 被触碰的项目可以编译。
- 所有自动化测试通过。
- 文档中的运行说明和当前代码一致。
- 不再引用已经删除的文档。
