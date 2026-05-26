# ULinkGame.Cluster

`ULinkGame.Cluster` contains optional explicit cluster routing contracts for ULinkGame.

This package is intentionally small. It defines node identity, route identity, generation-aware route locations, message envelopes, explicit actor route envelopes, route directory abstractions, router abstractions, a loopback messenger, and in-memory implementations for tests or local single-process validation.

Diagnostics are exposed through the `ULinkGame.Cluster` `Meter` and `ActivitySource`. Metrics use low-cardinality tags such as stage, status, delivery, and message kind.

It does not provide a production network adapter, Redis storage, service discovery, remote actor proxies, actor migration, or durable route state.

Actor route helpers produce route keys from application-chosen actor ids only. They do not encode node ids, endpoints, execution lanes, or ULinkActor scheduler internals.

Route locations include a route generation, node epoch, endpoint, lease expiration, and metadata. In-memory registration rejects stale generations and older node epochs, and lease refresh requires the caller to present the matching route owner. This keeps restarted nodes and moved route owners from accidentally reviving old ownership.

The selected first production adapter direction is the separate `ULinkGame.Cluster.ULinkRPC` package. The core package remains transport-neutral.
