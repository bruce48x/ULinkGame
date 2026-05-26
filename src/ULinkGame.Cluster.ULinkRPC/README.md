# ULinkGame.Cluster.ULinkRPC

`ULinkGame.Cluster.ULinkRPC` contains the ULinkRPC adapter layer for explicit ULinkGame cluster node-to-node messaging and remote route-directory calls.

The package stays outside `ULinkGame.Cluster` so core route contracts remain transport-neutral. It provides:

- a ULinkRPC method contract for sending `ClusterMessage` envelopes between nodes
- `ULinkRpcClusterNodeMessenger`, an `INodeMessenger` implementation backed by a ULinkRPC client factory
- `ULinkRpcClusterClientFactory`, a reusable client cache over application-provided ULinkRPC transports
- `IULinkRpcClusterTransportFactory`, the boundary where projects choose TCP, WebSocket, KCP, security, and endpoint policy
- `TcpULinkRpcClusterTransportFactory`, a TCP transport factory for endpoint addresses such as `tcp://127.0.0.1:20010`
- `ULinkRpcClusterMessageBinder`, a server-side binder that dispatches inbound cluster messages into an `IClusterMessageHandler`
- `ULinkRpcRouteDirectory`, an `IRouteDirectory` client backed by ULinkRPC calls
- `ULinkRpcRouteDirectoryBinder`, a server-side binder that exposes an application-provided `IRouteDirectory`

It does not provide durable route directory storage, service discovery, durable queues, gameplay DTOs, actor migration, or transparent remote actor clients. A route directory service can expose `InMemoryRouteDirectory` for smoke tests, or a project-owned durable implementation for production-specific policy.

Additional concrete transport factories should be added only with passing cross-process smoke tests. The package exposes `IULinkRpcClusterTransportFactory` so consuming projects can wire custom ULinkRPC transport policy while the package keeps the node messaging protocol and status mapping centralized.
