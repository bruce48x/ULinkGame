---
title: "ULinkGame 入门：从 RPC 到游戏会话基础设施"
date: 2026-05-07T11:20:00+08:00
summary: "ULinkGame 同时集成 ULinkRPC 与 Microsoft Orleans，补齐在线游戏常见的会话、宿主、重连和可靠业务推送基础设施。"
---

ULinkRPC 负责强类型 RPC、生成代码、transport、serializer 和 callback。Microsoft Orleans 负责分布式 actor、cluster、placement 和 grain state。ULinkGame 站在两者的集成层，关注在线游戏经常重复实现的会话基础设施。

## 适合解决的问题

一个真实在线游戏通常不只是调用几个 RPC 方法。项目很快会遇到这些问题：

- 一个 .NET server 中需要托管多个 RPC endpoint
- 控制面连接和实时 gameplay 连接需要分开
- 客户端重连后不能丢掉重要业务通知
- Unity、Godot、plain .NET 客户端不应重复写同一套可靠序号判断
- Orleans silo、gateway、RPC server 生命周期需要一致的启动方式

ULinkGame 把这些重复设施封装成可复用包，但不接管你的游戏业务。

## 包怎么选

`ULinkGame.Server` 用在 .NET 服务端进程中，提供 ULinkRPC hosting、Microsoft Orleans integration 和 reliable push outbox。

`ULinkGame.Client` 用在客户端代码中，提供 engine-neutral 的可靠推送序号追踪和重复消息过滤。

`ULinkGame.Tool` 用来创建和维护 ULinkGame 项目布局。

## 快速安装

服务端：

```powershell
dotnet add package ULinkGame.Server
```

客户端：

```powershell
dotnet add package ULinkGame.Client
```

项目工具：

```powershell
dotnet tool install --global ULinkGame.Tool
```

创建项目：

```powershell
ulinkgame-tool new --name MyGame --client-engine unity --transport kcp --serializer memorypack --persistence none
```

生成的项目会使用 ULinkRPC starter 创建基础 RPC 项目，并补充 Microsoft Orleans 配置、ULinkGame server hosting 和可靠业务推送基础设施。

## 边界

ULinkGame 不定义账号、匹配规则、房间规则、玩法模拟、UI 或持久化 schema。这些属于你的游戏。

ULinkGame 只应该保留多数在线游戏都会重复写的基础设施：连接生命周期、host integration、session infrastructure、reliable push mechanics 和可复用 client state helpers。
