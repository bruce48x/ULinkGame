# ULinkGame Runtime Guardrails

## Purpose

`ulinkgame-tool new` reduces the configuration surface for new projects, but the runtime framework must still protect projects from invalid or unsafe states after users start editing configuration, deployment profiles, or generated code.

Runtime guardrails make ULinkGame easier to use and easier to maintain by moving common "do not configure it this way" knowledge into framework-owned validation, diagnostics, and check output.

The goal is not to remove advanced configuration. The goal is to make the default path safe, make advanced paths explicit, and fail fast when a configuration violates ULinkGame runtime invariants.

## Design Principle

Tooling and runtime validation have different responsibilities:

- `ULinkGame.Tool` hides unnecessary choices and generates safe defaults.
- ULinkGame runtime packages enforce invariants that must hold for a server to run correctly.
- `--ulinkgame-check` explains the final derived state and repair steps in user-facing language.

Do not make Cluster, Hotfix, or Reliable Push ordinary optional modules in generated projects. They are part of the ULinkGame application model. Users may change their source, storage, topology, or deployment profile, but generated projects should not teach users to disable the core model.

## Validation Levels

Runtime guardrails use three levels.

### Errors

Errors are invalid states. Startup or `--ulinkgame-check` should fail.

Use errors for framework invariants:

- node id is missing or has an invalid format
- endpoint transport is unknown
- endpoint scheme, transport, and path are inconsistent
- WebSocket transport cannot derive a listener path
- cluster service names are duplicated
- cluster service kind is unknown
- gateway service is configured without reachable route-directory or node-directory support
- advertised endpoint cannot be parsed
- advertised endpoint conflicts with the configured listener in a way the runtime cannot route
- Hotfix is expected but no initial hotfix assembly can be loaded
- Hotfix reload produces duplicate dispatch keys or unsupported method signatures
- Reliable Push is enabled but no session identity or resume identity resolver is available
- production profile advertises localhost or loopback endpoints
- production profile selects in-memory node directory storage

### Warnings

Warnings are states that may be acceptable in development but are risky or surprising.

Use warnings for local or temporary defaults:

- Reliable Push uses in-memory storage
- node directory uses in-memory storage
- route directory uses in-memory storage
- advertised endpoint is loopback in a development profile
- endpoint uses a default port
- single-node topology is active
- Hotfix assembly is missing during a local check before the hotfix project has been built
- persistence is not configured
- route lease duration, send timeout, replay retention, or pending push limit uses defaults

Warnings should not make local development painful. They should be visible in `--ulinkgame-check` and diagnostics.

### Info

Info explains derived state without implying risk:

- selected node id
- selected transport and listener address
- derived advertised client endpoint
- configured service list
- hotfix source type and assembly name
- reliable push replay window
- selected topology or deployment profile

## Profiles

Validation should be profile-aware.

The default generated profile is development. Development allows local defaults such as single-node topology, loopback endpoints, in-memory directories, and in-memory reliable push storage.

Production-oriented profiles must be stricter. A production profile should reject configuration that is only safe for local development, including loopback advertised endpoints and in-memory cluster directory storage.

Profiles should not reintroduce `Hotfix.Enabled`, `Cluster.Enabled`, or `ReliablePush.Enabled` as normal user-facing switches. A profile changes topology, storage, endpoints, and operational strictness; it does not redefine the ULinkGame application model.

## Runtime Validation API

Add a framework-owned validation model that can be reused by server startup, generated check commands, tests, and future tooling.

Suggested shape:

```csharp
public enum ULinkGameDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ULinkGameDiagnostic(
    string Code,
    ULinkGameDiagnosticSeverity Severity,
    string Message,
    string? Repair = null);

public sealed record ULinkGameValidationResult(
    IReadOnlyList<ULinkGameDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.All(diagnostic => diagnostic.Severity != ULinkGameDiagnosticSeverity.Error);
}

public interface IULinkGameRuntimeValidator
{
    ULinkGameValidationResult Validate(ULinkGameRuntimeContext context);
}
```

The context should be built from resolved runtime options, not directly from raw JSON. Validators should see the same final state the server will use.

Suggested validator groups:

- `NodeValidator`
- `EndpointValidator`
- `ClusterValidator`
- `HotfixValidator`
- `ReliablePushValidator`
- `ProfileValidator`

Each validator should be small, deterministic, and testable. It should not start network listeners or mutate runtime state.

## Diagnostic Codes

Diagnostics should use stable codes so documentation, tests, logs, and check output can refer to the same condition.

Initial code families:

- `ULINK001-ULINK019`: node identity and profile
- `ULINK020-ULINK039`: endpoint and advertised addresses
- `ULINK040-ULINK069`: cluster services, node directory, route directory
- `ULINK070-ULINK089`: hotfix loading and dispatch
- `ULINK090-ULINK109`: Reliable Push and session identity
- `ULINK110-ULINK129`: production readiness

Messages should be short and actionable. Repairs should tell the user what to change or what command to run.

Example diagnostics:

```txt
ULINK001 error Node id is required.
ULINK023 error WebSocket endpoint path is required.
ULINK041 error Cluster service name 'gateway' is duplicated.
ULINK071 error Hotfix assembly was not found.
ULINK071 repair dotnet build Server/Hotfix/Server.Hotfix.csproj
ULINK091 error Reliable Push requires a session identity resolver.
ULINK111 error Production profile cannot advertise 127.0.0.1.
```

## Startup Behavior

Server startup should run runtime validation after configuration has been bound and derived, but before the server starts accepting traffic.

If validation returns errors:

- log all diagnostics
- throw a single startup exception that summarizes the error count and first actionable error
- do not start listeners

If validation returns warnings:

- log warnings
- continue startup in development profile
- fail startup in production profile only when the warning represents a production readiness rule promoted to error

Startup exceptions should preserve diagnostic codes so tests and tools can assert them without string matching.

## Check Command Behavior

Generated `--ulinkgame-check` should call the same runtime validation pipeline used by startup.

The check command should format diagnostics for humans:

```txt
cluster: ok single-node
node: ok dev-1
services: ok node-directory, route-directory, gateway
hotfix: failed local build output not found
fix: dotnet build Server/Hotfix/Server.Hotfix.csproj
reliable-push: ok pending limit 256, replay window 120s
rpc: ok kcp://127.0.0.1:20000
```

The generated check command may add friendly grouping and project-specific wording, but it must not maintain a separate validation logic fork. Framework validators own the rules; generated code owns presentation.

## Configuration Boundary

Default generated configuration should remain compact:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoint": {
      "Transport": "kcp",
      "Host": "127.0.0.1",
      "Port": 20000
    }
  }
}
```

Advanced configuration should express source values, not derived internals.

Acceptable advanced values:

- node id
- endpoint transport, host, port, path
- deployment profile
- topology profile
- persistent storage provider and connection string names
- advertised endpoints when deployment requires them
- service descriptors for split-node deployments

Avoid user-facing defaults for:

- `Hotfix.Enabled`
- `Cluster.Enabled`
- `ReliablePush.Enabled`
- `Hotfix.Directory`
- `ReliablePush.Outbox`
- `Node.Profile`
- derived bootstrap endpoints
- derived service lists for the default local topology

## Implementation Phases

### Phase 1: Foundation

- Add diagnostic result types.
- Add a runtime validation context built from resolved options.
- Add validators for node id, endpoint transport/path, duplicate service names, and hotfix assembly availability.
- Add unit tests for each diagnostic.

### Phase 2: Check Command Integration

- Make generated `--ulinkgame-check` call the framework validation pipeline.
- Keep current readable output shape.
- Ensure missing Hotfix build output returns a clear repair command.

### Phase 3: Profile-Aware Validation

- Add development and production validation profiles.
- Promote loopback advertised endpoints and in-memory directory storage to production errors.
- Keep development defaults warning-only.

### Phase 4: Reliable Push And Cluster Readiness

- Validate Reliable Push session identity and resume identity dependencies.
- Validate gateway dependencies on route-directory and node-directory capabilities.
- Validate advertised endpoint reachability rules that can be checked without opening sockets.

## Success Criteria

A generated development project should still run with minimal configuration and no manual edits.

Common local mistakes should fail with specific repair guidance.

Production-oriented configuration should not silently accept local-only defaults.

Tooling and framework startup should use the same validation rules, so a project that passes `--ulinkgame-check` has the same basic runtime invariants that server startup expects.
