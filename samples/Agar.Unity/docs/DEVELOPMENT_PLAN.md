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

## 待办

这一节只记录尚未完成、后续执行计划时真正需要处理的事项。已经完成的工作放在后面的“已完成记录”中。

### 当前优先级

1. 阶段 0A：完成启动/登录/匹配/结算 UI 的人工视觉回归，确认裁切后的 UI sprite 和运行时布局在 1200x600、960x540 下不再出现视觉边界错位。
2. 阶段 0：完成第一轮基础美术资产的 Unity 实机验收，确认玩家球、拾取物、背景、UI 基础件、图标、吸收环和出生波纹是否继续沿用或需要重做。
3. 阶段 1：完成单机和联机全流程手动回归。
4. 后续进入阶段 2 及之后的结构拆分、清理语义、跨网关路由和测试扩展。

### 阶段 0A：启动界面视觉修复

待办：

- 在 Unity 游戏视图手动检查入口/模式选择、联机登录、匹配中、大厅和结算界面。
- 覆盖 1200x600 和 960x540，重点看文本、按钮、输入框与可见面板边框是否对齐。
- 确认裁切后的 `UI_Panel_Dark_01.png`、`UI_Button_Primary_Normal.png`、`UI_Button_Primary_Pressed.png` 不再造成 `RectTransform` 逻辑尺寸和视觉边界错位。
- 如果现有 UI 面板或按钮风格仍不达标，按 `ART_DIRECTION.md` 的透明边缘规范重新生成或重做最小 UI 基础件。

验收标准：

- 启动到模式选择界面时，首屏能看到正式深色竞技场背景图。
- 三个模式按钮在同一中轴线上，尺寸一致，垂直间距一致，文字居中且不溢出。
- 联机登录界面的账号/密码标签与输入框形成居中窄列，登录/返回按钮等宽对齐，游客登录按钮居中且不压到底部边框。
- 非对局 UI 背景不覆盖实际对局画面。
- UI sprite 的 alpha 包围盒贴合肉眼可见边框，不再用大透明 padding 充当布局留白。

### 阶段 0：AI 生成美术资产

待办：

- 对第一轮基础美术资产做 Unity 实机验收：
  - `Skin_Jelly_Cyan.png`
  - `Skin_Jelly_Crimson.png`
  - `Skin_Jelly_Sunburst.png`
  - `Pickup_Mass_Teal_01.png`
  - `Pickup_Mass_Gold_01.png`
  - `BG_Arena_Grid_Dark_01.png`
  - `UI_Panel_Dark_01.png`
  - `UI_Button_Primary_Normal.png`
  - `UI_Button_Primary_Pressed.png`
  - `Icon_Leaderboard_01.png`
  - `Icon_Shop_01.png`
  - `FX_Absorb_Ring_01.png`
  - `FX_Spawn_Wave_01.png`
- 验证玩家球、拾取物、背景和特效不会降低玩法判断效率，不遮挡玩家名、分数、HUD 和排行榜信息。
- 如用户确认第一轮资产方向成立，再生成下一批 UI 基础件：Secondary/Danger 按钮、输入框状态、任务/记录/设置等图标、HUD 背板和列表行。
- 先接入入口/模式选择、匹配中、结算三个关键界面；通过后再扩展大厅、任务、商店、记录、排行榜和设置。

验收标准：

- `ART_DIRECTION.md` 作为后续资产生成、UI 设计、AI 生成提示词、Unity 接入和验收的唯一风格标准。
- 第一轮基础资产在 Unity 游戏视图中通过手动验收。
- 所有正式 UI sprite 的透明外边距符合文档约束。
- UI 文字不溢出，按钮状态清楚，图标 32x32 可识别。
- 九宫格拉伸后边框不变形。

### 阶段 1：玩法回归与协议清理

待办：

- 在 Unity 编辑器中验证单机启动、食物成长、玩家吞噬、死亡、复活和结算全流程。
- 在 Silo 和 Server 运行时，验证联机登录、匹配、实时绑定、输入、世界快照和结算全流程。
- 决定是否继续保留 `ArenaMapVariant` / `ArenaRuleVariant` 的联机扩展点；如果短期不扩展联机规则变体，将联机侧字段收敛为简单常量。

验收标准：

- 本地单机对局可以完整游玩到结束。
- 联机对局可以通过匹配开始，并持续收到实时世界快照。
- 可见 UI 路径不再依赖已移除的旧玩法概念。
- 生成的 RPC 代码与协议变更一致。

### 阶段 2：客户端拆分

待办：

- 从 `DotArenaGame` 中拆出对局流程职责，将 `UiSurface` / `UiActions` 中的状态切换逻辑收敛到显式的状态机或流程编排方法中。
- 保持 RPC 生命周期由 `DotArenaNetworkSession` 管理。
- 保持回调缓存和主线程消化由 `DotArenaCallbackInbox` 管理。
- 将 `DotArenaSceneUiPresenter` 中重复的 UI 构建逻辑（`Layout.cs`、`Layout2.cs`、`Styling.cs`）移动到更小的辅助类或 Unity 预制件/资源中。

验收标准：

- 模式入口、匹配、对局中和结算状态切换更容易推理。
- 不引入 `SampleClient.Rpc` 和 `SampleClient.Gameplay` 之间的循环依赖。
- 生成的 RPC 代码保持不被手写修改。
- `DotArenaSceneUiPresenter` 的运行时控件构建逻辑减少至少 30%（以行数计）。

### 阶段 3：网关清理语义

待办：

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

待办：

- 做联机实机回归，确认对局结束后真实玩家按排名获得胜利积分。
- 验证客户端排行榜展示服务端真实排名、当前周期剩余时间和本地玩家积分变化。
- 验证 AI 玩家不会获得胜利积分。
- 验证榜单当地时间周一 00:00 后首次查询会触发自动重置，并能查看最近归档数据。
- 继续迁移客户端文案和字段命名中 `PeriodStartUtc` 这类体验口径；服务端兼容字段可保留，但 UI 不应继续表达为 UTC。

验收标准：

- 联机对局结束后，玩家按排名获得对应胜利积分（1st=10, 2nd=7, 3rd=5, 4th=3, 5th=1）。
- AI 玩家不获得胜利积分。
- 客户端排行榜展示真实的全局玩家排名。
- 日志可追踪积分发放和排行榜查询。
- 排行榜在榜单当地时间周一 00:00 后首次查询时自动重置，最近归档数据可查。
- 单机模式不发放胜利积分。

### 阶段 5：跨网关实时路由设计

待办：

- 选择网关到网关的事件机制：Orleans stream、Orleans observer 或 Redis 发布订阅。
- 定义输入转发、世界状态扇出、断线事件和背压的所有权。
- 定义顺序、重试和运行时所有者过期时的行为。
- 实现前先更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`。

验收标准：

- 控制连接在网关 A 的玩家，可以把输入送到网关 B 上的房间运行时。
- 对局事件可以送回正确的在线客户端连接。
- 失败行为足够明确，可以编写测试或手动验证。

### 阶段 6：跨网关实时路由实现

待办：

- 实现阶段 5 选定的路由层。
- 保持房间模拟在同一时刻只由一个运行时所有者权威推进。
- 不把实时回调对象序列化到 Redis 或 Orleans 状态中。
- 增加路由决策和投递失败日志。

验收标准：

- 多网关部署可以处理控制连接、实时连接和房间运行时位于不同网关的情况。
- 世界状态广播可以到达连接在不同网关节点上的玩家。
- 断线、登出和离房清理在跨网关所有权下仍然正确。

### 阶段 7：测试扩展

待办：

- 为 `RoomRuntime` 增加测试：玩家加入/离开、输入提交、对局结束结算、空房间清理。
- 为 `SessionDirectory` / `SessionRegistration` 增加测试：注册、解绑、房间查询、回调获取。
- 为 `DotArenaNetworkSession` 增加测试：连接生命周期、重连参数、实时绑定。
- 为 `ArenaSimulation` 补充测试：食物刷新边界、吞噬比例边界、AI 补位、多人同时死亡。
- 为 `DotArenaMetaProgression` 增加测试：经验升级边界、每日任务重置、首胜断言。
- 为 `LeaderboardGrain` 补足测试：AI 过滤、全周期路径、未来全量用户目录接入后的全用户重置路径。

验收标准：

- 自动化测试数量从 9 个增加到至少 30 个。（当前 20 个）
- 核心业务逻辑路径（模拟、匹配、房间、会话）有可运行的测试覆盖。

### 阶段 8：验证与打包

待办：

- 每轮触碰服务端或共享协议后，构建 `Shared/Shared.csproj`、`Server/Silo/Silo.csproj`、`Server/Server/Server.csproj`。
- 每轮触碰业务逻辑后，运行已有自动化测试和新增测试。
- 每轮触碰 Unity 客户端脚本或资源后，触发 Unity 资源刷新/脚本编译并检查控制台错误。
- 每轮发布前，手动冒烟测试单机和联机流程。
- 当命令、端口或架构事实变化时，同步 README 和 `CLAUDE.md`。

验收标准：

- 被触碰的项目可以编译。
- 所有自动化测试通过。
- 文档中的运行说明和当前代码一致。
- 不再引用已经删除的文档。

## 已完成记录

这一节记录已经完成的事实，供回溯使用；不要把这里的条目当作后续执行待办。

### 阶段 0A：启动界面视觉修复

- 已接入非对局 UI 背景：入口、匹配、大厅和结算这些状态显示 `BG_Arena_Grid_Dark_01.png`，实际对局中隐藏菜单背景。
- 已校正入口/模式选择按钮布局，三个模式按钮同轴、等尺寸、文字居中。
- 已移除联机登录界面的 `Enter account credentials` 和 `联机登录` 标题文案。
- 已把 `MultiplayerPanel` 从铺满面板背景改为独立内容安全区，登录表单控件相对安全区排布。
- 已把 UI sprite 透明边缘约束写入 `ART_DIRECTION.md`。
- 已扫描并裁切 `Assets/Art` 下透明外边距超过 5% 的正式 PNG：`UI_Panel_Dark_01.png`、`UI_Button_Primary_Normal.png`、`UI_Button_Primary_Pressed.png`、`Icon_Leaderboard_01.png`、`Icon_Shop_01.png`、`FX_Absorb_Ring_01.png`、`FX_Spawn_Wave_01.png`、`Pickup_Mass_Gold_01.png`、`Pickup_Mass_Teal_01.png`。
- 资源裁切后重新扫描正式 PNG，透明外边距违规列表为空。
- Unity 资源刷新和脚本编译检查通过，控制台无错误。

### 阶段 0：AI 生成美术资产

- 第一批玩家球皮肤已生成并接入运行时：
  - `Skin_Jelly_Cyan.png`
  - `Skin_Jelly_Crimson.png`
  - `Skin_Jelly_Sunburst.png`
- 第一轮基础美术资产已进入 Unity 项目目录，并已完成脚本侧运行时接入：玩家球皮肤、质量拾取物、竞技场背景、入口/匹配/大厅/结算按钮基准图、商店/排行榜图标、吸收环和出生波纹。
- `ART_DIRECTION.md` 已作为后续资产生成、UI 设计、AI 生成提示词、Unity 接入和验收的风格标准。

### 阶段 1：玩法回归与协议清理

- 已完成 HUD 文案扫描和清理，可见 UI 路径不再引用冲刺、强化、眩晕等旧玩法语义。
- 已从 `InputMessage` 中移除 `Dash` 字段。
- 已从 `PlayerLifeState` 中移除 `Dash` 和 `Stunned`。
- 已从 `ArenaSimulationOptions.EnabledPickupTypes` 默认值和 `PickupType` 枚举中移除旧强化拾取物，只保留 `ScorePoint`。
- 已从 `PlayerState` 中移除旧强化剩余时间字段。
- Unity 脚本刷新通过，KCP factory 引用保持正常。
- 已创建 `samples/Agar.Unity/CLAUDE.md`，记录项目入口、关键文件路径和开发工作流规则。

### 阶段 2：客户端拆分

- `DotArenaMetaProgression` 已按关注点拆分为 Models、Catalog、Persistence、Queries、Rules 五个 partial 文件。

### 阶段 4：胜利积分与排行榜

- 已在 `IUserGrain`、`UserLoginResult` 和 `UserProfileSnapshot` 中增加胜利积分相关合同。
- 已在 `UserGrain` 中持久化 `VictoryPoints` 并实现积分增加。
- 已新增 `ILeaderboardGrain` 和 `LeaderboardGrain`。
- `LeaderboardGrain` 已维护由对局结算写入的排行榜积分索引，并按积分、胜场和玩家标识排序。
- 排行榜周期已改为榜单当地时间周一 00:00；`LeaderboardGrain` 当前使用 `TimeZoneInfo.Local`。
- 周期重置已归档上周 top 100，并只保留最近两周归档。
- 服务端合同已新增 `PeriodStartLocalDate`；旧 `PeriodStartUtc` 字段暂保留兼容。
- `RoomRuntime.PersistMatchEndAsync` 已按排名发放胜利积分，并过滤 AI 玩家。
- `IPlayerService` 已新增 `GetLeaderboardAsync`，`PlayerService` 已转发到 `ILeaderboardGrain`。
- 客户端已从本地 mock 排行榜切换到服务端真实排行榜数据缓存展示。
- 排行榜 UI 已展示当前周期剩余时间。

### 阶段 7：测试扩展

- 当前自动化测试数量为 20 个。
- `ArenaSimulationRulesTests` 已覆盖 16 个模拟规则测试。
- `MatchmakingQueuePolicyTests` 已覆盖 4 个匹配队列策略测试。
- `LeaderboardGrain` 已覆盖排序、当地时区周重置和归档保留。
- AI 过滤由 `VictoryPointAwards` 测试覆盖。

### 阶段 8：验证与打包

- 本轮已通过 `Shared/Shared.csproj` 构建。
- 本轮已通过 `Server/Silo/Silo.csproj` 构建。
- 本轮已通过 `Server/Server/Server.csproj` 构建。
- 本轮已通过已有自动化测试，20/20。
- Unity 脚本刷新和控制台错误检查已通过；完整手动游玩仍需按待办执行。
- README 和 `CLAUDE.md` 已同步到当时的命令、端口和架构事实。
