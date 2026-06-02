# Managed Distributed Actor API Implementation Plan

> **For agentic workers:** Use task-by-task execution with tests first. Keep each phase independently buildable and commit after verification.

**Goal:** Move ULinkGame's generated actor API toward the agreed managed distributed actor model: simple business calls, failure by exception, default distributed `Get`, local-only lifecycle, and distributed actor directory support.

**Architecture Decisions:**

- All `[Actor]` actors are framework-managed in the first version.
- Actor implementations must implement the actor contract shape used by the generator/runtime. Hotfix is for bug fixes, not contract changes.
- Generated business calls return business results directly. Remote failure throws typed actor call exceptions instead of exposing `RemoteActorInvocationResult` or `RemoteAskResult<T>` to users.
- `Get(id)` is the default distributed actor reference: local first, then actor directory, never creates.
- `Local(id)` only targets the current process.
- `Remote(nodeId, id)` only targets the specified node and does not query actor directory.
- `SpawnAsync` and `DestroyAsync` are local-only lifecycle operations. ULinkGame does not provide `SpawnRemoteAsync` or `DestroyRemoteAsync`.
- Cross-node creation/destruction is a business command to a manager actor/service on the target node.
- `ActorDirectory` lives in `ULinkGame.Server`; the distributed first version finds its host through cluster feature discovery.

**Tech Stack:** C# 13 / .NET 10, ULinkGame.Server, ULinkGame.Cluster, source generator tests, xUnit v3.

---

## Phase 1: Exception-First Generated Remote Calls

- [x] Add tests for actor call exception hierarchy and status mapping.
- [x] Add generator tests proving remote methods no longer emit `result.Status` switch/checks in user-facing generated methods.
- [x] Implement `ActorCallStatus`, `ActorCallException`, and common derived exceptions.
- [x] Add a runtime helper that maps `RemoteActorInvocationResult` to exceptions for generated code.
- [x] Update `TypedActorGenerator` to call the helper and return business results directly.
- [x] Run `dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj --no-restore`.
- [x] Run `dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --no-restore -m:1`.
- [ ] Commit `Use exceptions for generated remote actor calls`.

## Phase 2: Actor Directory Contracts

- [ ] Add Server-layer actor directory contracts: record, register/unregister statuses, cache abstraction, and in-memory store/host implementation for tests.
- [ ] Add distributed client shape that can discover the actor-directory host by feature, cache host node id, and retry directory calls once after host failure.
- [ ] Keep actor directory out of `Shared`/`Abstractions`.
- [ ] Add tests for register conflict, unregister ownership mismatch, resolve, cache hit/miss, and host rediscovery.
- [ ] Commit `Add distributed actor directory contracts`.

## Phase 3: Generated `Get/Local/Remote`

- [ ] Collapse generated local/remote ref shape into a single actor ref type where practical, or introduce `Get` first if full collapse is too large.
- [ ] Implement `Get(id)` local-first resolution: local runtime, actor directory cache, directory resolve, remote call.
- [ ] Ensure `Get` never creates actors and never auto-retries business actor calls.
- [ ] Add generator and runtime tests for local-first, directory cache, cache invalidation on location failures, and local-only semantics.
- [ ] Commit `Add distributed actor Get accessors`.

## Phase 4: Local-Only Managed Lifecycle

- [ ] Add `[ActorSpawn]` and `[ActorDestroy]` markers.
- [ ] Generate local-only `SpawnAsync` and `DestroyAsync` for all actors.
- [ ] Spawn flow: create local actor, invoke spawn hook if present, register actor directory, rollback local actor on failure.
- [ ] Destroy flow: invoke destroy hook if present, remove local actor, unregister actor directory.
- [ ] Do not generate or implement remote spawn/destroy.
- [ ] Add tests for spawn hook, destroy hook, directory register/unregister, rollback, and conflicts.
- [ ] Commit `Add local managed actor lifecycle`.

## Phase 5: Feature Node Discovery API

- [ ] Add `IClusterNodeDiscovery` API with `ListAsync(feature)` and `AnyAsync(feature)`.
- [ ] Return `NodeId`/node descriptors, never endpoints, from business-facing discovery.
- [ ] Use the API internally for actor-directory host discovery.
- [ ] Add tests for ready node filtering, missing feature, and default selection.
- [ ] Commit `Add cluster feature node discovery`.

## Verification

- [ ] Run `dotnet test Tests\tests.slnx --no-restore -m:1`.
- [ ] Update contributor/design docs to reflect the new accepted model and remove stale "do not generate remote actor client" language.
