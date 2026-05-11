# 服务端架构

这份文档描述服务端网关和 Orleans Silo 的职责边界、联机流程和分布式设计。核心玩法见 [`gameplay-rules.md`](gameplay-rules.md)，胜利积分系统见 [`victory-points.md`](victory-points.md)。

## 服务端边界

`samples/Agar.Unity/Server/Edge` 是 RPC 网关和房间运行时宿主。

当前职责：

- 控制面 RPC：登录、登出、匹配和低频业务接口。
- 实时面 RPC：对局输入和实时会话绑定。
- 维护网关本地的在线会话和回调对象。
- 通过 `RoomRuntimeHost` 和 `RoomRuntime` 承载房间运行时。
- 推送世界状态、死亡事件、结算事件和匹配状态。
- 对局结束时按排名发放胜利积分到 `IUserGrain`。

`samples/Agar.Unity/Server/Silo` 承载 Orleans grains。

当前职责：

- 用户身份和胜场持久化。
- 用户胜利积分持久化（`UserState.VictoryPoints`）。
- 玩家会话状态。
- 匹配队列状态。
- 房间分配和房间快照状态。
- 排行榜聚合查询（`ILeaderboardGrain`）。

PostgreSQL 是 Orleans 集群成员表和 grain 状态的持久化后端。

排行榜 grain 职责：

- 接收排行榜查询请求，读取由结算写入维护的排行榜积分索引。
- 接收结算后的 `RecordVictoryPointsAsync` 写入，更新玩家当前周期积分和胜场。
- 按积分降序、胜场降序、玩家标识升序排序后返回 top N。
- 榜单当地时间周一 00:00 触发重置：清空排行榜索引内用户的 `VictoryPoints`，归档上周数据。
- 排行榜 grain 自身状态存储当前周期本地日期（`CurrentPeriodStartLocalDate`）、兼容旧字段（`CurrentPeriodStartUtc`）、当前周期积分索引和最近两周快照。当前没有全量用户目录，因此重置覆盖的是索引内出现过的玩家；如果后续新增用户目录，需要扩展为全用户重置。

## 联机流程

控制连接流程：

1. 客户端连接控制面 RPC。
2. 客户端登录。
3. 客户端发起匹配。
4. 网关调用匹配 grain。
5. 匹配和房间 grain 分配房间与运行时网关。
6. 网关可靠推送匹配状态，并携带实时连接信息。

实时连接流程：

1. 客户端打开实时 RPC 连接。
2. 客户端用玩家、会话、房间和对局令牌调用 `AttachRealtimeAsync`。
3. 运行时网关登记实时回调。
4. 客户端通过实时 RPC 发送输入。
5. 房间运行时通过实时回调广播世界状态。

排行榜查询流程：

1. 客户端在登录后或模式入口界面通过控制面 RPC 请求排行榜。
2. 网关将请求转发到 `ILeaderboardGrain`。
3. Leaderboard grain 检查当前周期（若已过周一 00:00 则触发重置）。
4. 按排行榜索引排序后返回 top N。
5. 网关将结果返回客户端渲染。

## 联机同步边界

- 客户端发送输入。
- 服务端推进模拟。
- 服务端广播快照。
- 客户端对玩家位置做插值，减少快照跳动。

客户端输入消息包含：

```txt
InputMessage
{
    playerId
    moveX
    moveY
    tick
}
```

服务端广播的世界状态包含：

```txt
WorldState
{
    tick
    respawnDelaySeconds
    players[]
    pickups[]
}
```

`players[]` 包含位置、速度、生死状态、整型质量、半径和移动速度。战斗内实时排名只读取并展示整型质量，单机和联机都不再展示独立分数字段；协议和代码里不得保留旧分数字段。`pickups[]` 描述当前仍在地图上的食物。

## 分布式边界

已经分布式或持久化的部分：

- Orleans 成员表和 grain 状态使用 PostgreSQL。
- 匹配队列状态在 Orleans 中。
- 房间分配携带明确的运行时网关信息。
- 客户端收到明确的实时连接目标，不假设控制网关一定拥有房间。
- 实时绑定不再要求本地已有控制连接回调。
- 胜利积分存储在用户 grain 中，跨网关读写均通过 Orleans 客户端。
- 排行榜查询通过控制面 RPC 进入网关，再转发到 singleton `ILeaderboardGrain` key `0`。

仍然局限在单个网关进程内的部分：

- 活跃 RPC 回调对象。
- 活跃房间模拟。
- 世界状态广播扇出。
- 部分断线、登出和离房清理语义。

下一步分布式架构重点是网关到网关的输入和事件路由。候选方式包括 Orleans streams、Orleans observers 或 Redis 发布订阅。
