# 开发计划

这份文档只记录阶段计划、验收点和执行顺序。玩法规则和架构判断放在 `GAMEPLAY_DESIGN.md`，避免两个文档重复解释同一件事。

## 工作流规则

这个样例的功能开发按以下顺序推进：

1. 更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`、`ART_DIRECTION.md` 以及 `docs/features/` 下对应的功能子文档。
2. 更新本开发计划。
3. 实现代码改动。
4. 验证被影响的运行路径。

小改动可以让设计说明和计划说明保持简短，但只要行为或架构发生变化，就应该同步更新这两个核心文档。

共享协议变更时，先改 `Shared` 合同，再从 `samples/Agar.Unity` 目录重新生成 RPC 代码：

```powershell
ulinkgame-tool codegen
```

常规验证基线：

```powershell
dotnet build Shared/Shared.csproj -f net10.0
dotnet build Server/Silo/Silo.csproj
dotnet build Server/Server/Server.csproj
dotnet test tests/BusinessLogic.Tests/BusinessLogic.Tests.csproj
```

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
- 旧食物协议命名已删除，食物行为统一为 `PickupType.MassPoint` 质量成长。
- 战斗内 HUD、实时排名和结算只展示整型质量；玩家可见的实时排名只有质量列，不再出现“分数”或分数列。
- `PlayerState` 应只发布位置、速度、生死、整型质量、半径和移动速度；旧分数字段已从共享协议、服务端快照和客户端读取路径中彻底删除。
- `IUserGrain` 已持久化 `VictoryPoints`，`ILeaderboardGrain` 已提供周榜查询、周期重置和最近两周归档。
- 排行榜周期重置已按榜单当地时间周一 00:00 计算，旧 `PeriodStartUtc` 字段仅作为兼容字段保留。
- `RoomRuntime.PersistMatchEndAsync` 已按排名发放胜利积分，AI 玩家不获得积分。
- 游戏暂不包含任务、商店和记录功能；大厅不应展示或跳转到 Tasks / Shop / Records 页面。
- 面向玩家的 Unity 界面应使用中文文案；联机大厅和战斗内 HUD 不显示 DEBUG 信息、调试面板、tick、连接细节、内部状态枚举、同步视图数或快捷键提示。将来需要排查问题时只通过 Unity Console、服务端日志或客户端日志打印，不在玩家 UI 中保留。
- 战斗中需要实时显示当前对局玩家排名，排名框固定在画面右侧，背景必须低遮挡半透明，避免遮挡游戏画面和影响玩家判断。
- 自动化测试当前为 31 个（`ArenaSimulationRulesTests` 17 个测试用例、`LeaderboardGrainTests` 6 个、`MatchmakingQueuePolicyTests` 4 个、`SessionDirectoryCleanupTests` 4 个）。
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
- 旧局内分数字段与质量语义重复。后续应继续让战斗内实时排名、HUD 和结算只使用质量；旧分数字段需要从共享协议、模拟状态、服务端快照、客户端模型和玩家可见路径中保持删除状态。

### 客户端结构

- `DotArenaGame.cs` 有 93 个实例字段，分散在 12 个 partial 文件中。`UiSurface`、`UiActions`、`Presentation`、`Views` 之间的职责边界不清晰。
- `DotArenaSceneUiPresenter` 仍偏大，负责运行时控件构建（`Layout.cs`、`Layout2.cs`）、刷新（`Refresh.cs`）、样式（`Styling.cs`）、大厅（`Lobby.cs`）。
- 联机大厅的任务、商店和记录入口已从玩家可见路径移除；旧场景对象仅作为兼容占位隐藏，不再绑定玩家导航动作。
- 玩家可见 UI 已收敛为中文，并隐藏 debug 面板、连接端点、内部状态枚举和快捷键提示；后续继续确保联机大厅和战斗内 HUD 不展示任何 DEBUG 信息。
- `DotArenaMetaProgression` 的拆分已完成（Models、Catalog、Persistence、Queries、Rules），本计划不再对其安排额外拆分工作。

### 测试缺口

当前已有 31 个自动化测试。仍缺少覆盖：网络会话生命周期、房间运行时行为、客户端流程状态，以及更完整的网关清理语义。

## 待办

这一节只记录尚未完成、后续执行计划时真正需要处理的事项。已经完成的工作放在后面的“已完成记录”中。

### 当前优先级

0. 阶段 6：优先整理并执行客户端巨型类拆分。当前 partial 文件只是把大类切成多个文件，没有形成真正的职责边界，后续功能会继续增加维护成本。
1. 阶段 2：完成启动/登录/匹配/结算 UI 的人工视觉回归，确认裁切后的 UI sprite、实时排名面板和运行时布局在 1200x600、960x540 下不再出现视觉边界错位。
2. 阶段 4：完成第一轮基础美术资产的 Unity 实机验收，确认玩家球、拾取物、背景、UI 基础件、图标、吸收环和出生波纹是否继续沿用或需要重做。
3. 阶段 5：完成单机和联机全流程手动回归。
4. 后续进入阶段 7 及之后的清理语义、跨网关路由和测试扩展。

### 阶段 2：启动界面视觉修复

待办：

- 在 Unity 游戏视图手动检查入口/模式选择、联机登录、匹配中、大厅和结算界面。
- 在 Unity 游戏视图手动检查对局中的右侧实时排名面板。
- 在 Unity 游戏视图复查右侧实时排名面板的新半透明背景 `UI_HUD_Rank_Panel_Translucent_01.png`，确认不再遮挡背后的玩家球、食物、边界和移动方向。
- 覆盖 1200x600 和 960x540，重点看文本、按钮、输入框与可见面板边框是否对齐。
- 确认裁切后的 `UI_Panel_Dark_01.png`、`UI_Button_Primary_Normal.png`、`UI_Button_Primary_Pressed.png` 不再造成 `RectTransform` 逻辑尺寸和视觉边界错位。
- 如果现有 UI 面板或按钮风格仍不达标，按 `ART_DIRECTION.md` 的透明边缘规范重新生成或重做最小 UI 基础件。

验收标准：

- 启动到模式选择界面时，首屏能看到正式深色竞技场背景图。
- 三个模式按钮在同一中轴线上，尺寸一致，垂直间距一致，文字居中且不溢出。
- 联机登录界面的账号/密码标签与输入框形成居中窄列，登录/返回按钮等宽对齐，游客登录按钮居中且不压到底部边框。
- 非对局 UI 背景不覆盖实际对局画面。
- 对局中的右侧排名面板使用低遮挡半透明背景；透过面板仍能看清玩家球、食物、边界和移动方向，不遮挡核心玩法区域。
- UI sprite 的 alpha 包围盒贴合肉眼可见边框，不再用大透明 padding 充当布局留白。

### 阶段 4：AI 生成美术资产

待办：

- 为默认本地玩家小球接入蓝、橙、绿三色随机表现，要求每局开始时选色，同一局内保持稳定；不改共享协议和服务端权威状态。
- 对第一轮基础美术资产做 Unity 实机验收：
  - `Skin_Jelly_Cyan.png`
  - `Skin_Jelly_Crimson.png`
  - `Skin_Jelly_Sunburst.png`
  - `Pickup_Mass_Teal_01.png`
  - `Pickup_Mass_Gold_01.png`
  - `BG_Arena_Grid_Dark_01.png`
  - `UI_Panel_Dark_01.png`
  - `UI_HUD_Rank_Panel_Translucent_01.png`
  - `UI_Button_Primary_Normal.png`
  - `UI_Button_Primary_Pressed.png`
  - `Icon_Leaderboard_01.png`
  - `FX_Absorb_Ring_01.png`
  - `FX_Spawn_Wave_01.png`
- 验证玩家球、拾取物、背景和特效不会降低玩法判断效率，不遮挡玩家名、质量、HUD 和排行榜信息。
- 如用户确认第一轮资产方向成立，再生成下一批 UI 基础件：Secondary/Danger 按钮、输入框状态、排行榜/设置等图标、HUD 背板和列表行。
- 先接入入口/模式选择、匹配中、结算三个关键界面；通过后再扩展大厅、排行榜和设置。

验收标准：

- `ART_DIRECTION.md` 作为后续资产生成、UI 设计、AI 生成提示词、Unity 接入和验收的唯一风格标准。
- 第一轮基础资产在 Unity 游戏视图中通过手动验收。
- 默认本地玩家小球在多次开始对局时可以出现蓝、橙、绿三种颜色之一，且单局内不会跳色。
- 所有正式 UI sprite 的透明外边距符合文档约束。
- UI 文字不溢出，按钮状态清楚，图标 32x32 可识别。
- 九宫格拉伸后边框不变形。

### 阶段 5：玩法回归与协议清理

待办：

- 在 Unity 编辑器中验证单机启动、食物成长、玩家吞噬、死亡、复活和结算全流程。
- 在 Silo 和 Server 运行时，验证联机登录、匹配、实时绑定、输入、世界快照和结算全流程。
- 验证联机大厅不展示 DEBUG 信息，不保留调试面板、连接端点、连接细节、内部状态枚举、同步视图数、快捷键提示或开发诊断文本。
- 验证单机和联机对局中的右侧实时排名面板随世界状态变化正确刷新，且只展示整型质量；表头、行内容和排序都不出现“分数”。
- 验证右侧实时排名面板背景已改为低遮挡半透明，不再因为背景框过深或过实影响游戏画面可读性。
- 验证战斗内 HUD 不展示 DEBUG 信息：不出现 tick、endpoint、连接细节、内部状态枚举、同步视图数、快捷键提示或开发诊断文本。
- 将局内质量改为整型展示和排序口径；如果共享协议仍使用浮点质量，先明确取整规则，再安排协议清理。
- 删除旧分数字段和相关读取路径；玩家可见文案、HUD、结算和单机/联机实时排名不再出现“分数”，统一使用“质量”。
- 将旧食物协议命名改为 `PickupType.MassPoint`。
- 决定是否继续保留 `ArenaMapVariant` / `ArenaRuleVariant` 的联机扩展点；如果短期不扩展联机规则变体，将联机侧字段收敛为简单常量。

验收标准：

- 本地单机对局可以完整游玩到结束。
- 联机对局可以通过匹配开始，并持续收到实时世界快照。
- 单机和联机对局中的实时排名与当前玩家整型质量和生死状态一致。
- 单机和联机对局中的实时排名面板背景半透明且低遮挡，不影响玩家观察场地和移动。
- 战斗内 HUD 只显示玩家决策需要的信息，不显示 DEBUG 信息。
- 联机大厅只显示玩家决策需要的信息，不显示 DEBUG 信息；开发诊断只允许进 console/log。
- 可见 UI 路径不再依赖已移除的旧玩法概念。
- 生成的 RPC 代码与协议变更一致。

### 阶段 6：客户端拆分

目标：

- 把 `DotArenaGame` 收敛为 Unity 场景级组合根，只负责生命周期入口、组件装配和高层调度。
- 把 `DotArenaSceneUiPresenter` 收敛为 UI 根协调器，不再直接拥有所有面板字段、控件创建、样式和刷新细节。
- 用小类边界替代继续增加 partial 文件。partial 只允许作为短期迁移手段，不能作为最终架构。
- 保持样例易读：拆分后的类名、文件名和职责应能直接对应玩家流程，而不是把逻辑隐藏到过度抽象的工具层。

拆分原则：

- 单一职责优先于按 Unity 事件函数分文件。`Update`、按钮回调、网络回调和 UI 刷新都应流向明确的流程对象。
- 网络生命周期继续由 `DotArenaNetworkSession` 管理，不能把 RPC 连接细节散回 UI 或场景脚本。
- 服务端回调缓存和主线程消化继续由 `DotArenaCallbackInbox` 管理，Unity 对象只在主线程被修改。
- `SampleClient.Rpc` 不能依赖 `SampleClient.Gameplay`，生成的 RPC 代码不能手写修改。
- 新增类优先放在 `Client/Assets/Scripts/Gameplay`；只有传输或生成代码辅助才放到 `Client/Assets/Scripts/Rpc`。

待办：

- 盘点 `DotArenaGame*.cs` 中的字段和方法，按所有权分成会话、单机模拟、输入、世界表现、UI 快照、资源/皮肤、本地元进度七组；每组记录迁移目标类。
- 新增轻量状态模型，集中描述 `SessionMode`、`FrontendFlowState`、`EntryMenuState`、待处理 UI 请求和匹配计时等流程状态，避免多个 partial 文件直接读写同一组字段。
- 从 `DotArenaGame.Session.cs` / `DotArenaGame.Callbacks.cs` 中抽出联机流程编排，优先复用并扩展现有 `DotArenaMultiplayerFlow`，让登录、游客登录、匹配、取消匹配、实时绑定、断线恢复和可靠推送确认有单一入口。
- 从 `DotArenaGame.SinglePlayer.cs` 中抽出单机对局控制器，负责创建本地模拟、推进 tick、处理结算和重开；`DotArenaGame` 只转发输入意图和接收渲染状态。
- 从 `DotArenaGame.Views.cs` / `DotArenaGame.Presentation.cs` 中抽出视图注册表和表现资源目录，减少 `_views`、`_renderStates`、`_pickupViews`、sprite、shader、皮肤列表等字段直接挂在 `MonoBehaviour` 上。
- 从 `DotArenaGame.UiSurface.cs` / `DotArenaGame.UiActions.cs` 中抽出 UI 快照构建器和 UI 命令处理器。UI 快照只表达要显示什么，UI 命令只表达玩家请求什么，避免 UI 代码直接改网络或模拟状态。
- 将 `DotArenaSceneUiPresenter` 拆成根协调器和面板 presenter：入口/模式选择、登录、匹配中、大厅、HUD、对局排名、结算分别拥有自己的字段和刷新方法；不再保留调试面板 presenter。
- 将 `DotArenaSceneUiPresenter.Layout.cs`、`Layout2.cs`、`Styling.cs` 中重复的运行时 UI 构建逻辑移动到 `DotArenaUiFactory`、`DotArenaUiStyleCatalog` 或稳定 prefab/资源中；短期先用工厂类，后续需要可视化调参时再资源化。
- 删除拆分后不再需要的 partial 文件，避免留下空壳文件或只转发一行方法的文件。
- 每一小步拆分后都做 Unity 脚本编译检查；触碰共享协议时才重新运行 `ulinkgame-tool codegen`。

验收标准：

- `DotArenaGame` 只保留场景生命周期、组件装配和少量跨组件协调字段；实例字段数量从当前 93 个降到 25 个以内。
- `DotArenaGame` 不再需要 12 个 partial 文件；保留的 partial 文件不超过 4 个，且每个文件都有明确职责。
- 模式入口、登录、匹配、对局中、结算和返回大厅状态切换都能从一个流程对象或状态模型中追踪，不需要跨多个 partial 文件查找同一状态的写入点。
- `DotArenaSceneUiPresenter` 不再直接持有所有面板控件字段；每个主要面板有独立 presenter 或稳定 prefab 作为边界。
- `DotArenaSceneUiPresenter` 的运行时控件构建逻辑减少至少 30%（以行数计），并且重复按钮、文本、面板样式创建集中在一个工厂或样式目录中。
- 不引入 `SampleClient.Rpc` 和 `SampleClient.Gameplay` 之间的循环依赖。
- 生成的 RPC 代码保持不被手写修改。
- 单机开始、联机登录、匹配、取消匹配、实时绑定、世界刷新、断线返回和结算按钮路径都能通过手动回归。
- Unity 脚本编译无错误；受影响的服务端和共享项目在本阶段未改动时不强制重建。

### 阶段 7：网关清理语义

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

### 阶段 8：胜利积分与排行榜

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

### 阶段 9：跨网关实时路由设计

待办：

- 选择网关到网关的事件机制：Orleans stream、Orleans observer 或 Redis 发布订阅。
- 定义输入转发、世界状态扇出、断线事件和背压的所有权。
- 定义顺序、重试和运行时所有者过期时的行为。
- 实现前先更新 `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`。

验收标准：

- 控制连接在网关 A 的玩家，可以把输入送到网关 B 上的房间运行时。
- 对局事件可以送回正确的在线客户端连接。
- 失败行为足够明确，可以编写测试或手动验证。

### 阶段 10：跨网关实时路由实现

待办：

- 实现阶段 9 选定的路由层。
- 保持房间模拟在同一时刻只由一个运行时所有者权威推进。
- 不把实时回调对象序列化到 Redis 或 Orleans 状态中。
- 增加路由决策和投递失败日志。

验收标准：

- 多网关部署可以处理控制连接、实时连接和房间运行时位于不同网关的情况。
- 世界状态广播可以到达连接在不同网关节点上的玩家。
- 断线、登出和离房清理在跨网关所有权下仍然正确。

### 阶段 11：测试扩展

待办：

- 为 `RoomRuntime` 增加测试：玩家加入/离开、输入提交、对局结束结算、空房间清理。
- 为 `SessionDirectory` / `SessionRegistration` 增加测试：注册、解绑、房间查询、回调获取。
- 为 `DotArenaNetworkSession` 增加测试：连接生命周期、重连参数、实时绑定。
- 为 `ArenaSimulation` 补充测试：食物刷新边界、吞噬比例边界、AI 补位、多人同时死亡。
- 为 `DotArenaMetaProgression` 增加测试：经验升级边界、首胜断言，以及阶段 1 后仍保留的本地进度路径。
- 为 `LeaderboardGrain` 补足测试：AI 过滤、全周期路径、未来全量用户目录接入后的全用户重置路径。

验收标准：

- 自动化测试数量从 9 个增加到至少 30 个。（当前 31 个）
- 核心业务逻辑路径（模拟、匹配、房间、会话）有可运行的测试覆盖。

### 阶段 12：验证与打包

待办：

- 每轮触碰服务端或共享协议后，构建 `Shared/Shared.csproj`、`Server/Silo/Silo.csproj`、`Server/Server/Server.csproj`。
- 每轮触碰业务逻辑后，运行已有自动化测试和新增测试。
- 每轮触碰 Unity 客户端脚本或资源后，触发 Unity 资源刷新/脚本编译并检查控制台错误。
- 每轮发布前，手动冒烟测试单机和联机流程。
- 当命令、端口或架构事实变化时，同步 README。

验收标准：

- 被触碰的项目可以编译。
- 所有自动化测试通过。
- 文档中的运行说明和当前代码一致。
- 不再引用已经删除的文档。

## 已完成记录

这一节记录已经完成的事实，供回溯使用；不要把这里的条目当作后续执行待办。

### 阶段 1：清理未上线与调试 UI

验收整理（2026-05-08）：

阶段 1 原待办共 8 项，当前 7 项完成，1 项部分完成。玩家可见路径的验收已基本完成；剩余部分完成项是更深层的本地进度数据模型收敛，当前保留的商店/记录相关字段仍有皮肤和排行榜趋势等实际用途，暂未做破坏性删除。

| 序号 | 原待办 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | 移除 Tasks / Shop / Records 的玩家可见入口、页签、快捷按钮和跳转映射 | 完成 | 大厅只保留资料、排行榜、设置；旧按钮隐藏且不再绑定动作。 |
| 2 | 移除任务、商店、记录页面内容、按钮文案、状态提示和事件提示 | 完成 | 玩家可见 UI 不再展示任务、商店、记录相关页面或提示。 |
| 3 | 清理结算界面的任务摘要 | 完成 | 结算只展示结果、质量、奖励、下一步、再来一局和返回大厅。 |
| 4 | 清理商店/记录相关图标加载和挂载路径 | 完成 | 运行时不再加载或挂载 `Icon_Shop_01.png`。 |
| 5 | 删除 debug 面板、连接端点、内部状态、tick、同步视图数和快捷键提示 | 完成 | 玩家界面、联机大厅和战斗内 HUD 不再展示这些内部信息；开发诊断只进 console/log。 |
| 6 | 将玩家可见文案收敛为中文 | 完成 | 入口、登录、匹配、大厅、HUD、结算、排行榜、设置和错误状态已中文化；后续仅保留常规措辞打磨。 |
| 7 | 评估并清理 `DotArenaMetaProgression` 中任务、商店、记录相关模型 | 部分完成 | 任务进度不再生成或推进，旧任务存档会清空；皮肤、历史和排行榜趋势仍依赖部分本地进度字段，未强删。 |
| 8 | 同步 `ART_DIRECTION.md` 和客户端架构文档 | 完成 | 文档已明确当前 UI 验收范围暂不包含任务、商店和记录，玩家 UI 使用中文。 |

- 已从大厅玩家可见导航中移除任务、商店和记录入口；保留资料、排行榜和设置。
- 已禁用大厅快捷按钮到任务、商店和记录页面的跳转，旧场景按钮会被隐藏且不再绑定动作。
- 已从结算界面移除任务摘要；结算只展示结果、质量、奖励、下一步、再来一局和返回大厅。
- 已停止运行时 UI 加载和挂载 `Icon_Shop_01.png`。
- 已删除玩家界面上的 debug 面板，并清理联机大厅和备用 HUD 中的 endpoint、tick、同步视图数、快捷键提示等内部信息；联机大厅和战斗内 HUD 不应显示 DEBUG 信息。
- 已将入口、登录、匹配、大厅、对局 HUD、结算、排行榜、设置和错误状态的玩家可见文案收敛为中文。
- `DotArenaMetaProgression` 不再为任务生成新进度或推进任务计数；旧本地存档中的任务字段会被清空以保持兼容加载。
- `ART_DIRECTION.md` 和 `docs/features/client-architecture.md` 已同步当前 UI 验收范围：暂不包含任务、商店和记录，玩家 UI 使用中文。

### 阶段 3：战斗中实时排名面板

- 已在对局中增加右侧当前对局排名面板，非对局界面隐藏。
- 排名数据来自 `DotArenaGame` 的当前 `PlayerRenderState` 集合，单机和联机共用同一套展示逻辑。
- 排序规则需要按新口径调整为整型质量降序、玩家标识升序；旧分数不再参与单机或联机战斗内实时排名。
- 面板应展示名次、玩家名和整型质量，本地玩家行使用高亮背景和文字颜色；旧分数列、表头和行文本应彻底移除。
- 面板背景需要按新视觉要求改为低遮挡半透明；当前背景框遮挡游戏画面，影响用户体验，应作为后续 UI 修复项处理。
- 已为右侧实时排名面板生成并接入专用低遮挡背景 `UI_HUD_Rank_Panel_Translucent_01.png`；中心 alpha 约 9%，边缘 alpha 约 33%，角落透明，并在 UI 侧再乘 72% 整体透明度。
- 已降低排名行背景透明度：普通行约 6% 到 10%，本地玩家行约 20%，避免行底色继续遮挡游戏画面。
- 面板文案为中文，不展示 tick、连接状态、端点等内部字段。
- Unity 脚本刷新通过，控制台无编译错误；人工视觉回归仍在阶段 2/5 继续覆盖。

### 阶段 2：启动界面视觉修复

- 已接入非对局 UI 背景：入口、匹配、大厅和结算这些状态显示 `BG_Arena_Grid_Dark_01.png`，实际对局中隐藏菜单背景。
- 已为战斗内右侧实时排名面板接入专用半透明背景 `UI_HUD_Rank_Panel_Translucent_01.png`，不再复用通用深色面板。
- 已校正入口/模式选择按钮布局，三个模式按钮同轴、等尺寸、文字居中。
- 已移除联机登录界面的 `Enter account credentials` 和 `联机登录` 标题文案。
- 已把 `MultiplayerPanel` 从铺满面板背景改为独立内容安全区，登录表单控件相对安全区排布。
- 已把 UI sprite 透明边缘约束写入 `ART_DIRECTION.md`。
- 已扫描并裁切 `Assets/Art` 下透明外边距超过 5% 的正式 PNG：`UI_Panel_Dark_01.png`、`UI_Button_Primary_Normal.png`、`UI_Button_Primary_Pressed.png`、`Icon_Leaderboard_01.png`、`Icon_Shop_01.png`、`FX_Absorb_Ring_01.png`、`FX_Spawn_Wave_01.png`、`Pickup_Mass_Gold_01.png`、`Pickup_Mass_Teal_01.png`。
- 资源裁切后重新扫描正式 PNG，透明外边距违规列表为空。
- Unity 资源刷新和脚本编译检查通过，控制台无错误。

### 阶段 4：AI 生成美术资产

- 第一批玩家球皮肤已生成并接入运行时：
  - `Skin_Jelly_Cyan.png`
  - `Skin_Jelly_Crimson.png`
  - `Skin_Jelly_Sunburst.png`
- 第一轮基础美术资产已进入 Unity 项目目录，并已完成脚本侧运行时接入：玩家球皮肤、质量拾取物、竞技场背景、入口/匹配/大厅/结算按钮基准图、商店/排行榜图标、吸收环和出生波纹。
- `ART_DIRECTION.md` 已作为后续资产生成、UI 设计、AI 生成提示词、Unity 接入和验收的风格标准。

### 阶段 5：玩法回归与协议清理

- 已完成 HUD 文案扫描和清理，可见 UI 路径不再引用冲刺、强化、眩晕等旧玩法语义。
- 已从 `InputMessage` 中移除 `Dash` 字段。
- 已从 `PlayerLifeState` 中移除 `Dash` 和 `Stunned`。
- 已从 `ArenaSimulationOptions.EnabledPickupTypes` 默认值和 `PickupType` 枚举中移除旧强化拾取物，并将质量拾取物命名改为 `MassPoint`。
- 已从 `PlayerState` 中移除旧强化剩余时间字段。
- Unity 脚本刷新通过，KCP factory 引用保持正常。
### 阶段 6：客户端拆分

- `DotArenaMetaProgression` 已按关注点拆分为 Models、Catalog、Persistence、Queries、Rules 五个 partial 文件。
- 已完成第一轮客户端职责拆分：新增 `DotArenaMultiplayerState` 集中保存联机会话身份、认证资料、待处理 UI 请求、重连标记、匹配计时、实时连接和可靠推送 tracker；`DotArenaGame` 通过转发属性兼容现有 partial，后续还需继续减少直接状态写入。
- 已新增 `DotArenaSinglePlayerController`，负责本地模拟创建、无敌模式选项、本地输入提交、tick catch-up 和模拟推进；`DotArenaGame.SinglePlayer.cs` 改为把单机结果接回现有表现和结算流程的适配层。
- 已新增 `DotArenaUiFactory` 和 `DotArenaUiStyleCatalog`，将 `DotArenaSceneUiPresenter.Layout*.cs` 与 `Styling.cs` 中重复的运行时文本、按钮、面板、输入框创建和样式逻辑抽到共享工厂/样式目录。
- 已把 UI 快照构建移动到 `DotArenaGame.UiSnapshot.cs`，让 `DotArenaGame.UiSurface.cs` 更集中于 UI 命令处理。
- 本轮客户端脚本编译通过：`dotnet build samples/Agar.Unity/Client/SampleClient.Gameplay.csproj --no-restore`，0 警告、0 错误。
- 尚未达到阶段 6 的最终验收硬指标：`DotArenaGame` 仍有多份 partial 文件和较多转发状态，`DotArenaSceneUiPresenter` 仍未拆成独立面板 presenter；完整单机/联机手动回归仍需在 Unity 游戏视图执行。

### 阶段 8：胜利积分与排行榜

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

### 阶段 11：测试扩展

- 当前自动化测试数量为 31 个。
- `ArenaSimulationRulesTests` 已覆盖 17 个模拟规则测试用例。
- `MatchmakingQueuePolicyTests` 已覆盖 4 个匹配队列策略测试。
- `LeaderboardGrainTests` 已覆盖 6 个排序、当地时区周重置、归档保留和旧 UTC 周期迁移测试。
- `SessionDirectoryCleanupTests` 已覆盖 4 个房间和实时注册清理测试。
- AI 过滤由 `VictoryPointAwards` 测试覆盖。

### 阶段 12：验证与打包

- 本轮已通过 `Shared/Shared.csproj` 构建。
- 本轮已通过 `Server/Silo/Silo.csproj` 构建。
- 本轮已通过 `Server/Server/Server.csproj` 构建。
- 本轮已通过已有自动化测试，31/31。
- Unity 脚本刷新和控制台错误检查已通过；完整手动游玩仍需按待办执行。
- README 已同步到当时的命令、端口和架构事实。
