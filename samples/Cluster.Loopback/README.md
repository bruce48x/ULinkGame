# Cluster Loopback Sample

This sample proves the explicit `ULinkGame.Cluster` contract without a production transport or game-specific DTOs.

Run it from the repository root:

```powershell
dotnet run --project samples/Cluster.Loopback/Cluster.Loopback.csproj
```

Expected output:

```txt
local=Accepted
remote=Accepted
missing=RouteNotFound
expired=Expired
timeout=Timeout
backpressure=Backpressure
```

The sample uses:

- `InMemoryRouteDirectory` for route registration and expiration checks.
- `InMemoryLoopbackNodeMessenger` for same-process node-to-node delivery.
- `ClusterActorEnvelope` only as bytes plus metadata, not a remote actor reference.
- explicit failure statuses for missing routes, expired messages, timeout, and backpressure.

It intentionally does not include Redis, service discovery, production transport, generated remote proxies, actor migration, or game business contracts.

The next production adapter direction is a separate ULinkRPC-based adapter package after a real cross-process sample or generated template needs it. This loopback sample remains the contract smoke test.
