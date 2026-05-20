---
title: "可靠业务推送：为什么传输可靠还不够"
date: 2026-05-07T11:25:00+08:00
summary: "业务推送需要证明客户端已经应用事件；这件事不能只交给 transport 层。"
---

Transport 可以让数据更可靠地到达连接，但它不能证明客户端已经把一个业务事件应用到 UI 或本地会话状态里。在线游戏里，这个差异会直接变成卡流程问题。

## 典型失败场景

1. A 和 B 进入匹配。
2. 服务端创建房间，并向两端推送 `Matched`。
3. A 收到并进入房间。
4. B 在推送窗口内重连。
5. 旧连接已经断开，服务端不知道 B 是否真的处理了 `Matched`。
6. B 可能永远停在等待匹配界面。

这不是 serializer 或 transport 单独能解决的问题。业务层需要一套可确认、可重放、可去重的推送机制。

## ULinkGame 的模型

ULinkGame 使用 at-least-once delivery 加 per-owner monotonic sequence number：

- 服务端为每个 owner 分配递增 sequence
- outbox 保存尚未确认的业务推送记录
- 客户端只应用比本地 latest sequence 更新的消息
- 客户端应用完成后 ack latest sequence
- 服务端删除 `sequence <= latestAppliedSequence` 的记录
- 客户端重连后，服务端重放仍然 pending 的记录

这比追求 exactly-once 更实际。重连、进程重启、客户端崩溃和服务器 failover 都会破坏 exactly-once 的假设；at-least-once 加幂等处理更容易验证。

## 职责边界

`ULinkGame.Server` 负责通用机制：

- sequence 分配
- pending records 存储
- reconnect 后重放
- ack 后裁剪
- retention 和 pending-count 限制

业务代码负责语义：

- 决定哪些消息需要可靠投递
- 在 payload 中携带 sequence
- 暴露 ack RPC 或复用已有请求携带 ack
- 让客户端 handler 幂等

这样可靠推送仍然是 host/session infrastructure，不会把 matchmaking、room、mail、reward 等业务概念塞进框架核心。

## 状态丢失要显式处理

如果客户端以为自己能恢复 session，但服务端已经丢失兼容状态，不能把它当成普通重连成功。

常见原因包括：

- 客户端离线超过 reconnect grace period
- gateway 重启导致 in-memory outbox 丢失
- 服务端清理了 session

正确行为是返回明确的 state-lost 结果，要求客户端清理旧状态并开始新 session，而不是继续停留在旧 matchmaking 或 in-match UI。

## 实现位置

实现细节属于 `ULinkGame.Server.ReliablePush`：

- `IReliablePushOutbox`
- `InMemoryReliablePushOutbox`
- `ReliablePushOptions`

更完整的内部设计文档保留在 repository 根目录的 `CONTRIBUTING.md` 中。
