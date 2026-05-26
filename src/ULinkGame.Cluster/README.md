# ULinkGame.Cluster

`ULinkGame.Cluster` contains optional explicit cluster routing contracts for ULinkGame.

This package is intentionally small. It defines node identity, route identity, route locations, message envelopes, explicit actor route envelopes, route directory abstractions, router abstractions, a loopback messenger, and in-memory implementations for tests or local single-process validation.

Diagnostics are exposed through the `ULinkGame.Cluster` `Meter` and `ActivitySource`. Metrics use low-cardinality tags such as stage, status, delivery, and message kind.

It does not provide a production network adapter, Redis storage, service discovery, remote actor proxies, actor migration, or durable route state.

Actor route helpers produce route keys from application-chosen actor ids only. They do not encode node ids, endpoints, execution lanes, or ULinkActor scheduler internals.

The selected first production adapter direction is a separate ULinkRPC-based package, for example `ULinkGame.Cluster.ULinkRPC`, when a real cross-process sample or template needs it. The core package should remain transport-neutral.
