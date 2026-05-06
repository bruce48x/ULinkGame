# ULinkGame Package Decision

## Background

The framework started as a thin server-hosting layer: it wired ULinkRPC servers, Orleans, dependency injection, and process lifetime. Reliable business push changes that boundary. The framework now owns mechanics that must be understood by both sides of a game session:

- reconnect versus new-session decisions
- business push sequencing
- client acknowledgement semantics
- replay after reconnect
- state-mismatch handling when the server no longer has compatible session state

The old `Host` name now undersells the scope and can imply a server-only library.

## Decision

Rename the framework family to `ULinkGame`.

The first package split is:

- `ULinkGame.Server`
- `ULinkGame.Client`

Do not introduce `ULinkGame.Shared` yet.

Do not introduce `ULinkGame.Unity` yet.

## Why ULinkGame

`ULinkGame` clearly communicates that this layer is above raw RPC and is intended for game networking workflows. The relationship should be:

- `ULinkRPC`: transport, serialization, RPC calls, and generated bindings
- `ULinkGame`: game-session infrastructure built on top of ULinkRPC
- user game code: matchmaking, room rules, gameplay state, rewards, inventory, and other domain features

This keeps the product line understandable without forcing a thick game framework.

## Why Not ULinkGame.Shared Now

A third shared package raises the first-time learning cost. Users already have:

- `ULinkRPC`
- their own shared RPC contract project
- server code
- client code

Adding `ULinkGame.Shared` too early creates a naming collision with user-owned `Shared` projects and makes it unclear where business DTOs should live.

For now, shared business contracts should remain in the user's own shared project. Examples:

- login request/reply DTOs
- matchmaking status payloads
- reliable sequence fields on business messages
- app-specific result codes

If cross-side framework abstractions become stable and numerous, introduce `ULinkGame.Abstractions` later. Prefer `Abstractions` over `Shared`, because it communicates framework-owned contracts instead of user-owned game DTOs.

## Why Not ULinkGame.Unity Now

Unity-specific integration is useful, but it should not be the first client package. The reusable core should not depend on:

- `MonoBehaviour`
- Unity main-thread APIs
- `Time.time`
- Unity logging
- Unity assembly definition layout

The first client package should be a plain .NET library. Unity projects can consume it through normal package/import mechanisms while keeping Unity-specific glue in the sample or in the user's project.

`ULinkGame.Unity` can be added later only when repeated Unity-specific integration code becomes stable enough to justify a package.

## First Client Library Boundary

`ULinkGame.Client` should own client-side mechanisms that are not engine-specific:

- latest applied reliable push sequence tracking
- duplicate reliable message detection
- ack decision helpers
- state-mismatch result handling
- reconnect state transitions that are independent of UI rendering

It should not own:

- Unity scene state
- UI text
- gameplay-specific callbacks such as `MatchmakingStatusUpdate`
- transport creation details unless they can be expressed through small interfaces

Unity sample code should remain responsible for:

- copying RPC DTOs into a main-thread inbox
- mutating Unity UI and scene state on the main thread
- choosing how to display reconnect/new-session outcomes
- calling generated RPC clients

## Migration Plan

1. Keep reusable framework code under `src/ULinkGame.Server`, `src/ULinkGame.Client`, and `src/ULinkGame.Tool`.
2. Keep sample-owned `Shared`, `Server`, and `Client` projects under `samples/Agar.Unity`.
3. Keep business DTOs in the sample or consuming game's `Shared` project until a real `ULinkGame.Abstractions` need appears.
4. Keep cross-package framework decisions under root `docs`, package-specific design under the owning package, and sample design under `samples/Agar.Unity/docs`.
5. Replace sample-local reliable sequence bookkeeping with `ULinkGame.Client` where practical.

## Compatibility Note

During early development, breaking namespace and project-name changes are acceptable. Once packaged, add compatibility shims or a migration guide only if external users already depend on older package names.
