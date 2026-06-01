# Cluster Node Sender Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move nodeId-to-endpoint delivery out of Remote Actor runtime and into a Cluster-layer direct node sender.

**Architecture:** `RemoteActorInvoker` should only build actor envelopes and call `IClusterNodeSender`. `ClusterNodeSender` in `ULinkGame.Cluster` resolves `NodeId` through `INodeDirectory`, selects the configured cluster endpoint, builds `RouteLocation`, and delegates to `INodeMessenger`. `RemoteActorOptions` keeps only actor-call semantics such as `DefaultTimeout`.

**Tech Stack:** C# 13 / .NET 10, ULinkGame.Cluster, ULinkGame.Server, xUnit v3.

---

## File Structure

- Create `src/ULinkGame.Cluster/Messaging/IClusterNodeSender.cs`: direct node sender interface.
- Create `src/ULinkGame.Cluster/Messaging/ClusterNodeSenderOptions.cs`: cluster deployment defaults for direct node send.
- Create `src/ULinkGame.Cluster/Messaging/ClusterNodeSender.cs`: default node directory + node messenger implementation.
- Modify `src/ULinkGame.Server/Actors/RemoteActorOptions.cs`: remove `ClusterName` and `EndpointName`.
- Modify `src/ULinkGame.Server/Actors/RemoteActorInvoker.cs`: depend on `IClusterNodeSender` instead of `INodeDirectory` and `INodeMessenger`.
- Modify tests in `Tests/ULinkGame.Cluster.Tests` and `Tests/ULinkGame.Server.Tests`.

## Task 1: Cluster Node Sender

- [x] Add failing `ClusterNodeSenderTests` covering successful node resolution, missing node, missing endpoint, and messenger status passthrough.
- [x] Implement `IClusterNodeSender`, `ClusterNodeSenderOptions`, and `ClusterNodeSender`.
- [x] Run `dotnet test Tests\ULinkGame.Cluster.Tests\ULinkGame.Cluster.Tests.csproj --no-restore --filter "FullyQualifiedName~ClusterNodeSender"`.
- [x] Commit `Add cluster node sender`.

## Task 2: Remote Actor Invoker Refactor

- [x] Update `RemoteActorInvokerTests` to stub `IClusterNodeSender`, proving `invocation.Node` and actor route are passed through.
- [x] Remove `ClusterName` and `EndpointName` from `RemoteActorOptions`.
- [x] Refactor `RemoteActorInvoker` to call `IClusterNodeSender.SendAsync`.
- [x] Run `dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~RemoteActor"`.
- [x] Commit `Route remote actor sends through cluster node sender`.

## Task 3: Verification

- [x] Run `dotnet test Tests\ULinkGame.Cluster.Tests\ULinkGame.Cluster.Tests.csproj --no-restore`.
- [x] Run `dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --no-restore -m:1`.
- [x] Run `dotnet test Tests\tests.slnx --no-restore -m:1`.
- [x] Update docs/changelog if stale references to `RemoteActorOptions.ClusterName` or `EndpointName` remain.
