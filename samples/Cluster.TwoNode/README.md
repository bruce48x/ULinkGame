# Cluster.TwoNode

`Cluster.TwoNode` is a minimal cross-process ULinkGame cluster smoke sample.

It starts:

- a route-directory process exposing `IRouteDirectory` through `ULinkRpcRouteDirectoryBinder`
- a worker process exposing `IClusterMessageHandler` through `ULinkRpcClusterMessageBinder`
- a driver process that registers a local route, sends local and remote cluster messages, clears a stale worker epoch, restarts the worker with a new epoch, and sends again

Run:

```powershell
dotnet run --project samples/Cluster.TwoNode/Cluster.TwoNode.csproj -- --mode driver
```

The sample intentionally contains no matchmaking, room rules, account system, persistence schema, or gameplay DTOs. It only verifies cluster infrastructure boundaries and failure statuses.
