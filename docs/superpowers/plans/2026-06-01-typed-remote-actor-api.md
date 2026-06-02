# Typed Remote Actor API Implementation Plan

> **Status:** Superseded by [2026-06-02 Managed Distributed Actor API](2026-06-02-managed-distributed-actor-api.md). This plan is retained as historical implementation context for the lower-level remote invoker work. Current architecture guidance is `Get(id)` by default, explicit `Local(id)` and `Remote(nodeId, id)` selectors, typed actor call exceptions for generated business calls, Server-owned `ActorDirectory`, and local-only generated `SpawnAsync`/`DestroyAsync`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the skynet-style typed actor access API where generated `Local(id)` and `Remote(nodeId, id)` refs expose the same actor business methods.

**Architecture:** Add a small runtime foundation in `ULinkGame.Server` for typed actor keys, stable actor/method names, remote invocation, and structured remote failures. Add a new source-generator package that scans `Actor<TKey>` classes and generates typed local/remote refs plus dispatch glue, leaving low-level `ClusterMessage` APIs as implementation details.

**Tech Stack:** C# 13 / .NET 10, Roslyn incremental source generators, xUnit v3, existing `ULinkGame.Server`, `ULinkGame.Cluster`, and `ULinkActor` facades.

---

## File Structure

- Modify `src/ULinkGame.Server/Actors/Actor.cs`: add `Actor<TKey>` as the typed-key base class while preserving current `Actor`.
- Create `src/ULinkGame.Server/Actors/ActorNameAttribute.cs`: stable actor wire name.
- Create `src/ULinkGame.Server/Actors/ActorMethodAttribute.cs`: stable method wire id.
- Create `src/ULinkGame.Server/Actors/ActorIgnoreAttribute.cs`: exclude methods from generated refs.
- Create `src/ULinkGame.Server/Actors/ActorLocalOnlyAttribute.cs`: generate local refs only.
- Create `src/ULinkGame.Server/Actors/RemoteActorStatus.cs`: structured remote failure statuses.
- Create `src/ULinkGame.Server/Actors/RemoteActorException.cs`: generated API exception for remote failures.
- Create `src/ULinkGame.Server/Actors/RemoteActorOptions.cs`: default timeout/deadline options for remote generated calls.
- Create `src/ULinkGame.Server/Actors/IRemoteActorSerializer.cs`: serializer abstraction used by generated remote refs.
- Create `src/ULinkGame.Server/Actors/RemoteActorInvocation.cs`: runtime request object for low-level remote invoker.
- Create `src/ULinkGame.Server/Actors/RemoteActorInvocationResult.cs`: low-level status-returning result.
- Create `src/ULinkGame.Server/Actors/IRemoteActorInvoker.cs`: low-level status-returning remote call API.
- Create `src/ULinkGame.Server/Actors/RemoteActorInvoker.cs`: default invoker over `IClusterRouter` and `RemoteActorGateway`.
- Modify `src/ULinkGame.Server/Actors/ActorServiceCollectionExtensions.cs`: register remote actor runtime services.
- Create `src/ULinkGame.Server.Generators/ULinkGame.Server.Generators.csproj`: new analyzer package for typed actor source generation.
- Create `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`: generator implementation.
- Create `src/ULinkGame.Server.Generators/TypedActorGeneratorDiagnostics.cs`: generator diagnostic descriptors.
- Create `src/ULinkGame.Server.Generators/README.md`: package docs for the generator.
- Create `Tests/ULinkGame.Server.Generators.Tests/ULinkGame.Server.Generators.Tests.csproj`: generator test project.
- Create `Tests/ULinkGame.Server.Generators.Tests/GeneratorTestHost.cs`: Roslyn test harness.
- Create `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`: generated-source behavior tests.
- Modify `Tests/tests.slnx`: include the new generator test project.
- Modify `Tests/ULinkGame.Server.Tests/ActorRuntimeTests.cs`: verify `Actor<TKey>` remains compatible with existing local runtime.
- Create `Tests/ULinkGame.Server.Tests/RemoteActorInvokerTests.cs`: remote invoker status and exception behavior.
- Modify `docs/remote-actor-messaging.md`: align with any naming changes made during implementation.
- Modify `CHANGELOG.md`: note the typed remote actor API direction when implementation lands.

---

## Task 1: Runtime Metadata Primitives

**Files:**
- Modify: `src/ULinkGame.Server/Actors/Actor.cs`
- Create: `src/ULinkGame.Server/Actors/ActorNameAttribute.cs`
- Create: `src/ULinkGame.Server/Actors/ActorMethodAttribute.cs`
- Create: `src/ULinkGame.Server/Actors/ActorIgnoreAttribute.cs`
- Create: `src/ULinkGame.Server/Actors/ActorLocalOnlyAttribute.cs`
- Test: `Tests/ULinkGame.Server.Tests/ActorRuntimeTests.cs`

- [ ] **Step 1: Add a failing test for `Actor<TKey>` compatibility**

Append this test actor and test to `Tests/ULinkGame.Server.Tests/ActorRuntimeTests.cs`:

```csharp
public readonly record struct RuntimeRoomId(string Value);

public sealed class TypedRoomActor : Actor<RuntimeRoomId>
{
    public ValueTask<string> EchoAsync(string value, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult($"{Context.Id.Value}:{value}");
    }
}

[Fact]
public async Task ActorRuntime_supports_typed_actor_base()
{
    var provider = CreateProvider();
    var runtime = provider.GetRequiredService<IActorRuntime>();

    var result = await runtime.AskAsync<TypedRoomActor, string>(
        ActorId.From("room/alpha"),
        static (actor, ct) => actor.EchoAsync("joined", ct));

    Assert.Equal("room/alpha:joined", result);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~ActorRuntime_supports_typed_actor_base"
```

Expected: compile failure because `Actor<TKey>` does not exist.

- [ ] **Step 3: Add `Actor<TKey>`**

Change `src/ULinkGame.Server/Actors/Actor.cs` to:

```csharp
namespace ULinkGame.Server.Actors;

public abstract class Actor : IActor
{
    public ActorContext Context { get; private set; } = ActorContext.Uninitialized;

    internal async ValueTask ActivateAsync(ActorContext context, CancellationToken cancellationToken)
    {
        Context = context;
        await OnActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        await OnDeactivateAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual ValueTask OnActivateAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask OnDeactivateAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}

public abstract class Actor<TKey> : Actor
    where TKey : notnull
{
}
```

- [ ] **Step 4: Add metadata attributes**

Create `src/ULinkGame.Server/Actors/ActorNameAttribute.cs`:

```csharp
namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActorNameAttribute : Attribute
{
    public ActorNameAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Actor name is required.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}
```

Create `src/ULinkGame.Server/Actors/ActorMethodAttribute.cs`:

```csharp
namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ActorMethodAttribute : Attribute
{
    public ActorMethodAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Actor method name is required.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}
```

Create `src/ULinkGame.Server/Actors/ActorIgnoreAttribute.cs`:

```csharp
namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ActorIgnoreAttribute : Attribute
{
}
```

Create `src/ULinkGame.Server/Actors/ActorLocalOnlyAttribute.cs`:

```csharp
namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActorLocalOnlyAttribute : Attribute
{
}
```

- [ ] **Step 5: Run the focused test and verify it passes**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~ActorRuntime_supports_typed_actor_base"
```

Expected: PASS.

- [ ] **Step 6: Commit runtime metadata primitives**

Run:

```powershell
git add src\ULinkGame.Server\Actors\Actor.cs src\ULinkGame.Server\Actors\ActorNameAttribute.cs src\ULinkGame.Server\Actors\ActorMethodAttribute.cs src\ULinkGame.Server\Actors\ActorIgnoreAttribute.cs src\ULinkGame.Server\Actors\ActorLocalOnlyAttribute.cs Tests\ULinkGame.Server.Tests\ActorRuntimeTests.cs
git commit -m "Add typed actor metadata primitives"
```

---

## Task 2: Remote Actor Runtime Foundation

**Files:**
- Create: `src/ULinkGame.Server/Actors/RemoteActorStatus.cs`
- Create: `src/ULinkGame.Server/Actors/RemoteActorException.cs`
- Create: `src/ULinkGame.Server/Actors/RemoteActorOptions.cs`
- Create: `src/ULinkGame.Server/Actors/IRemoteActorSerializer.cs`
- Create: `src/ULinkGame.Server/Actors/RemoteActorInvocation.cs`
- Create: `src/ULinkGame.Server/Actors/RemoteActorInvocationResult.cs`
- Create: `src/ULinkGame.Server/Actors/IRemoteActorInvoker.cs`
- Test: `Tests/ULinkGame.Server.Tests/RemoteActorInvokerTests.cs`

- [ ] **Step 1: Write failing tests for remote exception shape**

Create `Tests/ULinkGame.Server.Tests/RemoteActorInvokerTests.cs`:

```csharp
using ULinkGame.Cluster;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class RemoteActorInvokerTests
{
    [Fact]
    public void RemoteActorException_preserves_structured_failure_fields()
    {
        var exception = new RemoteActorException(
            RemoteActorStatus.RouteNotFound,
            ActorId.From("room/1001"),
            "room",
            "join",
            "The route was not found.",
            new NodeId("node-a"),
            "corr-1");

        Assert.Equal(RemoteActorStatus.RouteNotFound, exception.Status);
        Assert.Equal(ActorId.From("room/1001"), exception.ActorId);
        Assert.Equal("room", exception.ActorName);
        Assert.Equal("join", exception.MethodName);
        Assert.Equal(new NodeId("node-a"), exception.Node);
        Assert.Equal("corr-1", exception.CorrelationId);
        Assert.Contains("RouteNotFound", exception.Message);
    }
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~RemoteActorException_preserves_structured_failure_fields"
```

Expected: compile failure because remote actor runtime types do not exist.

- [ ] **Step 3: Add remote status and exception types**

Create `src/ULinkGame.Server/Actors/RemoteActorStatus.cs`:

```csharp
namespace ULinkGame.Server.Actors;

public enum RemoteActorStatus
{
    Replied,
    Accepted,
    RouteNotFound,
    Expired,
    Timeout,
    Backpressure,
    HandlerUnavailable,
    NodeUnavailable,
    SerializationFailed,
    DeserializationFailed,
    Cancelled
}
```

Create `src/ULinkGame.Server/Actors/RemoteActorException.cs`:

```csharp
using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed class RemoteActorException : Exception
{
    public RemoteActorException(
        RemoteActorStatus status,
        ActorId actorId,
        string actorName,
        string methodName,
        string message,
        NodeId? node = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base($"Remote actor call failed with status {status}. Actor={actorId.Value}, Method={actorName}.{methodName}. {message}", innerException)
    {
        Status = status;
        ActorId = actorId;
        ActorName = actorName ?? throw new ArgumentNullException(nameof(actorName));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        Node = node;
        CorrelationId = correlationId;
    }

    public RemoteActorStatus Status { get; }

    public NodeId? Node { get; }

    public ActorId ActorId { get; }

    public string ActorName { get; }

    public string MethodName { get; }

    public string? CorrelationId { get; }
}
```

- [ ] **Step 4: Add serializer and invocation contracts**

Create `src/ULinkGame.Server/Actors/RemoteActorOptions.cs`:

```csharp
namespace ULinkGame.Server.Actors;

public sealed class RemoteActorOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
```

Create `src/ULinkGame.Server/Actors/IRemoteActorSerializer.cs`:

```csharp
namespace ULinkGame.Server.Actors;

public interface IRemoteActorSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T Deserialize<T>(ReadOnlyMemory<byte> payload);
}
```

Create `src/ULinkGame.Server/Actors/RemoteActorInvocation.cs`:

```csharp
using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed record RemoteActorInvocation(
    NodeId Node,
    ActorId ActorId,
    string ActorName,
    string MethodName,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset Deadline,
    string CorrelationId);
```

Create `src/ULinkGame.Server/Actors/RemoteActorInvocationResult.cs`:

```csharp
namespace ULinkGame.Server.Actors;

public sealed record RemoteActorInvocationResult(
    RemoteActorStatus Status,
    ReadOnlyMemory<byte> Payload,
    string? Message = null)
{
    public static RemoteActorInvocationResult Accepted()
    {
        return new RemoteActorInvocationResult(RemoteActorStatus.Accepted, ReadOnlyMemory<byte>.Empty);
    }

    public static RemoteActorInvocationResult Replied(ReadOnlyMemory<byte> payload)
    {
        return new RemoteActorInvocationResult(RemoteActorStatus.Replied, payload);
    }

    public static RemoteActorInvocationResult Failed(RemoteActorStatus status, string message)
    {
        return new RemoteActorInvocationResult(status, ReadOnlyMemory<byte>.Empty, message);
    }
}
```

Create `src/ULinkGame.Server/Actors/IRemoteActorInvoker.cs`:

```csharp
namespace ULinkGame.Server.Actors;

public interface IRemoteActorInvoker
{
    ValueTask<RemoteActorInvocationResult> AskAsync(
        RemoteActorInvocation invocation,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteActorInvocationResult> TellAsync(
        RemoteActorInvocation invocation,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Run focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~RemoteActorException_preserves_structured_failure_fields"
```

Expected: PASS.

- [ ] **Step 6: Commit remote actor runtime contracts**

Run:

```powershell
git add src\ULinkGame.Server\Actors\RemoteActorStatus.cs src\ULinkGame.Server\Actors\RemoteActorException.cs src\ULinkGame.Server\Actors\RemoteActorOptions.cs src\ULinkGame.Server\Actors\IRemoteActorSerializer.cs src\ULinkGame.Server\Actors\RemoteActorInvocation.cs src\ULinkGame.Server\Actors\RemoteActorInvocationResult.cs src\ULinkGame.Server\Actors\IRemoteActorInvoker.cs Tests\ULinkGame.Server.Tests\RemoteActorInvokerTests.cs
git commit -m "Add remote actor invocation contracts"
```

---

## Task 3: Default Remote Actor Invoker

**Files:**
- Create: `src/ULinkGame.Server/Actors/RemoteActorInvoker.cs`
- Modify: `src/ULinkGame.Server/Actors/ActorServiceCollectionExtensions.cs`
- Test: `Tests/ULinkGame.Server.Tests/RemoteActorInvokerTests.cs`

- [ ] **Step 1: Add failing tests for cluster status mapping**

Append these test doubles and tests to `Tests/ULinkGame.Server.Tests/RemoteActorInvokerTests.cs`:

```csharp
private sealed class StubClusterRouter : IClusterRouter
{
    public ClusterSendStatus Status { get; set; } = ClusterSendStatus.Accepted;

    public ClusterMessage? LastMessage { get; private set; }

    public ValueTask<ClusterSendStatus> SendAsync(
        ClusterMessage message,
        CancellationToken cancellationToken = default)
    {
        LastMessage = message;
        return ValueTask.FromResult(Status);
    }
}

[Fact]
public async Task TellAsync_maps_cluster_backpressure_to_remote_backpressure()
{
    var router = new StubClusterRouter
    {
        Status = ClusterSendStatus.Backpressure
    };
    var invoker = new RemoteActorInvoker(router, new RemoteActorGateway(), new NodeId("node-local"));
    var invocation = new RemoteActorInvocation(
        new NodeId("node-b"),
        ActorId.From("room/1001"),
        "room",
        "leave",
        new byte[] { 1, 2, 3 },
        DateTimeOffset.UtcNow.AddSeconds(5),
        "corr-1");

    var result = await invoker.TellAsync(invocation);

    Assert.Equal(RemoteActorStatus.Backpressure, result.Status);
}
```

- [ ] **Step 2: Run focused tests and verify they fail**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~TellAsync_maps_cluster_backpressure_to_remote_backpressure"
```

Expected: compile failure because `RemoteActorInvoker` does not exist.

- [ ] **Step 3: Add default invoker**

Create `src/ULinkGame.Server/Actors/RemoteActorInvoker.cs`:

```csharp
using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed class RemoteActorInvoker : IRemoteActorInvoker
{
    private readonly IClusterRouter _router;
    private readonly RemoteActorGateway _gateway;
    private readonly NodeId _localNode;

    public RemoteActorInvoker(
        IClusterRouter router,
        RemoteActorGateway gateway,
        NodeId localNode)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _localNode = localNode;
    }

    public async ValueTask<RemoteActorInvocationResult> AskAsync(
        RemoteActorInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var pending = _gateway.RegisterPendingAsync(
            invocation.CorrelationId,
            invocation.Deadline - DateTimeOffset.UtcNow,
            cancellationToken);

        var send = await SendEnvelopeAsync(invocation, includeReply: true, cancellationToken).ConfigureAwait(false);
        if (send.Status != RemoteActorStatus.Accepted)
        {
            return send;
        }

        try
        {
            var payload = await pending.ConfigureAwait(false);
            return RemoteActorInvocationResult.Replied(payload);
        }
        catch (TimeoutException ex)
        {
            return RemoteActorInvocationResult.Failed(RemoteActorStatus.Timeout, ex.Message);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return RemoteActorInvocationResult.Failed(RemoteActorStatus.Cancelled, ex.Message);
        }
    }

    public ValueTask<RemoteActorInvocationResult> TellAsync(
        RemoteActorInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return SendEnvelopeAsync(invocation, includeReply: false, cancellationToken);
    }

    private async ValueTask<RemoteActorInvocationResult> SendEnvelopeAsync(
        RemoteActorInvocation invocation,
        bool includeReply,
        CancellationToken cancellationToken)
    {
        var envelope = new ClusterActorEnvelope(
            ClusterActorRouteKeys.ForActor(invocation.ActorId.Value),
            invocation.ActorId.Value,
            invocation.MethodName,
            invocation.Payload,
            invocation.Deadline,
            _localNode,
            correlationId: invocation.CorrelationId,
            replyCorrelationId: includeReply ? invocation.CorrelationId : null);

        var status = await _router.SendAsync(envelope.ToClusterMessage(), cancellationToken).ConfigureAwait(false);
        return status == ClusterSendStatus.Accepted
            ? RemoteActorInvocationResult.Accepted()
            : RemoteActorInvocationResult.Failed(Map(status), $"Cluster send returned {status}.");
    }

    private static RemoteActorStatus Map(ClusterSendStatus status)
    {
        return status switch
        {
            ClusterSendStatus.RouteNotFound => RemoteActorStatus.RouteNotFound,
            ClusterSendStatus.Expired => RemoteActorStatus.Expired,
            ClusterSendStatus.Timeout => RemoteActorStatus.Timeout,
            ClusterSendStatus.Backpressure => RemoteActorStatus.Backpressure,
            ClusterSendStatus.HandlerUnavailable => RemoteActorStatus.HandlerUnavailable,
            _ => RemoteActorStatus.NodeUnavailable
        };
    }
}
```

- [ ] **Step 4: Register remote actor services**

Open `src/ULinkGame.Server/Actors/ActorServiceCollectionExtensions.cs` and add singleton registrations in `AddULinkGameServerActors(...)`:

```csharp
services.TryAddSingleton<RemoteActorGateway>();
services.TryAddSingleton<RemoteActorOptions>();
```

Do not register `IRemoteActorInvoker` yet because it needs deployment-specific `IClusterRouter` and local `NodeId`; generated projects or node features should provide those.

- [ ] **Step 5: Run focused tests and verify they pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~RemoteActorInvoker"
```

Expected: PASS.

- [ ] **Step 6: Commit invoker foundation**

Run:

```powershell
git add src\ULinkGame.Server\Actors\RemoteActorInvoker.cs src\ULinkGame.Server\Actors\ActorServiceCollectionExtensions.cs Tests\ULinkGame.Server.Tests\RemoteActorInvokerTests.cs
git commit -m "Add default remote actor invoker"
```

---

## Task 4: Create Typed Actor Generator Package

**Files:**
- Create: `src/ULinkGame.Server.Generators/ULinkGame.Server.Generators.csproj`
- Create: `src/ULinkGame.Server.Generators/TypedActorGeneratorDiagnostics.cs`
- Create: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Create: `src/ULinkGame.Server.Generators/README.md`
- Create: `Tests/ULinkGame.Server.Generators.Tests/ULinkGame.Server.Generators.Tests.csproj`
- Create: `Tests/ULinkGame.Server.Generators.Tests/GeneratorTestHost.cs`
- Create: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`
- Modify: `Tests/tests.slnx`

- [ ] **Step 1: Create generator project and test project files**

Create `src/ULinkGame.Server.Generators/ULinkGame.Server.Generators.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>ULinkGame.Server.Generators</RootNamespace>
    <PackageId>ULinkGame.Server.Generators</PackageId>
    <Version>0.1.0</Version>
    <Description>Source generators for ULinkGame server typed actor accessors.</Description>
    <PackageTags>ulinkgame;actor;source-generator;roslyn</PackageTags>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <NoWarn>$(NoWarn);RS2008</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <None Include="$(OutputPath)$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    <None Update="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

Create `Tests/ULinkGame.Server.Generators.Tests/ULinkGame.Server.Generators.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/ULinkGame.Server/ULinkGame.Server.csproj" />
    <ProjectReference Include="../../src/ULinkGame.Server.Generators/ULinkGame.Server.Generators.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the test project to the solution file**

Add this line to `Tests/tests.slnx`:

```xml
  <Project Path="ULinkGame.Server.Generators.Tests/ULinkGame.Server.Generators.Tests.csproj" />
```

- [ ] **Step 3: Add generator diagnostics shell**

Create `src/ULinkGame.Server.Generators/TypedActorGeneratorDiagnostics.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace ULinkGame.Server.Generators;

internal static class TypedActorGeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor UnsupportedMethodSignature = new(
        "ULINKACTOR001",
        "Actor method signature is not supported by typed actor generation",
        "Method '{0}' is public but does not match a supported typed actor method shape",
        "ULinkGame.TypedActors",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
```

- [ ] **Step 4: Add a compiling empty generator**

Create `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace ULinkGame.Server.Generators;

[Generator]
public sealed class TypedActorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
```

Create `src/ULinkGame.Server.Generators/README.md`:

```markdown
# ULinkGame.Server.Generators

Source generators for typed ULinkGame server actor accessors.
```

- [ ] **Step 5: Add generator test host**

Create `Tests/ULinkGame.Server.Generators.Tests/GeneratorTestHost.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ULinkGame.Server.Generators;

namespace ULinkGame.Server.Generators.Tests;

internal static class GeneratorTestHost
{
    public static GeneratorRunResult Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(ULinkGame.Server.Actors.Actor).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ULinkGame.Cluster.NodeId).Assembly.Location)
            })
            .Distinct(MetadataReferencePathComparer.Instance)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TypedActorGeneratorTests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypedActorGenerator();
        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updated,
            out var diagnostics);

        return new GeneratorRunResult(
            string.Join(
                Environment.NewLine,
                updated.SyntaxTrees.Skip(1).Select(static tree => tree.ToString())),
            diagnostics,
            updated.GetDiagnostics());
    }

    private sealed class MetadataReferencePathComparer : IEqualityComparer<MetadataReference>
    {
        public static readonly MetadataReferencePathComparer Instance = new();

        public bool Equals(MetadataReference? x, MetadataReference? y)
        {
            return string.Equals(x?.Display, y?.Display, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(MetadataReference obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Display ?? string.Empty);
        }
    }
}

internal sealed record GeneratorRunResult(
    string GeneratedSource,
    IReadOnlyList<Diagnostic> GeneratorDiagnostics,
    IReadOnlyList<Diagnostic> CompilationDiagnostics)
{
    public IReadOnlyList<Diagnostic> ErrorDiagnostics =>
        GeneratorDiagnostics.Concat(CompilationDiagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
}
```

- [ ] **Step 6: Add a failing generator smoke test**

Create `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`:

```csharp
using Xunit;

namespace ULinkGame.Server.Generators.Tests;

public sealed class TypedActorGeneratorTests
{
    [Fact]
    public void Generates_accessor_group_for_actor_key_base_type()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct RoomId(string Value);

            public sealed record JoinRoomRequest(string PlayerId);

            public sealed record JoinRoomReply(bool Accepted);

            public sealed class RoomActor : Actor<RoomId>
            {
                public ValueTask<JoinRoomReply> JoinAsync(
                    JoinRoomRequest request,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(new JoinRoomReply(true));
                }
            }
            """);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("public sealed class RoomActors", result.GeneratedSource);
        Assert.Contains("public RoomLocalRef Local(RoomId id)", result.GeneratedSource);
        Assert.Contains("public RoomRemoteRef Remote(global::ULinkGame.Cluster.NodeId node, RoomId id)", result.GeneratedSource);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync", result.GeneratedSource);
    }
}
```

- [ ] **Step 7: Run generator tests and verify the smoke test fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: FAIL because the empty generator emits no `RoomActors`.

- [ ] **Step 8: Commit generator package scaffold**

Run:

```powershell
git add src\ULinkGame.Server.Generators Tests\ULinkGame.Server.Generators.Tests Tests\tests.slnx
git commit -m "Add typed actor generator scaffold"
```

---

## Task 5: Generate Local Actor Refs

**Files:**
- Modify: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Modify: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`

- [ ] **Step 1: Add failing assertions for local ref body**

Extend `Generates_accessor_group_for_actor_key_base_type` with:

```csharp
Assert.Contains("private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;", result.GeneratedSource);
Assert.Contains("return _runtime.AskAsync<global::Game.Server.RoomActor, JoinRoomReply>", result.GeneratedSource);
Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"room/\" + id.Value)", result.GeneratedSource);
```

- [ ] **Step 2: Run generator tests and verify failure**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj --filter "FullyQualifiedName~Generates_accessor_group_for_actor_key_base_type"
```

Expected: FAIL because local ref body is not generated yet.

- [ ] **Step 3: Implement actor discovery and local ref generation**

Replace `src/ULinkGame.Server.Generators/TypedActorGenerator.cs` with an incremental generator that:

```csharp
// Discovery rule:
// - class declaration has a base type whose original definition metadata name is
//   ULinkGame.Server.Actors.Actor`1
// - actor key type is the single generic argument
// - eligible methods are public instance methods returning ValueTask or ValueTask<T>
//   with exactly one request parameter plus optional CancellationToken.
```

Generate source equivalent to:

```csharp
namespace Game.Server;

public sealed class RoomActors
{
    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;
    private readonly global::ULinkGame.Server.Actors.IRemoteActorInvoker _remote;
    private readonly global::ULinkGame.Server.Actors.IRemoteActorSerializer _serializer;
    private readonly global::ULinkGame.Server.Actors.RemoteActorOptions _options;

    public RoomActors(
        global::ULinkGame.Server.Actors.IActorRuntime runtime,
        global::ULinkGame.Server.Actors.IRemoteActorInvoker remote,
        global::ULinkGame.Server.Actors.IRemoteActorSerializer serializer,
        global::ULinkGame.Server.Actors.RemoteActorOptions options)
    {
        _runtime = runtime;
        _remote = remote;
        _serializer = serializer;
        _options = options;
    }

    public RoomLocalRef Local(RoomId id)
    {
        return new RoomLocalRef(_runtime, id);
    }

    public RoomRemoteRef Remote(global::ULinkGame.Cluster.NodeId node, RoomId id)
    {
        return new RoomRemoteRef(_remote, _serializer, _options, node, id);
    }
}

public readonly struct RoomLocalRef
{
    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;
    private readonly RoomId _id;

    public RoomLocalRef(
        global::ULinkGame.Server.Actors.IActorRuntime runtime,
        RoomId id)
    {
        _runtime = runtime;
        _id = id;
    }

    public global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        global::System.Threading.CancellationToken cancellationToken = default)
    {
        var actorId = global::ULinkGame.Server.Actors.ActorId.From("room/" + _id.Value);
        return _runtime.AskAsync<global::Game.Server.RoomActor, JoinRoomReply>(
            actorId,
            (actor, ct) => actor.JoinAsync(request, ct),
            cancellationToken);
    }
}
```

Generator implementation should use `SymbolDisplayFormat.FullyQualifiedFormat` for cross-namespace type names and preserve the actor's namespace for generated types.

- [ ] **Step 4: Run generator tests and verify pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit local ref generation**

Run:

```powershell
git add src\ULinkGame.Server.Generators\TypedActorGenerator.cs Tests\ULinkGame.Server.Generators.Tests\TypedActorGeneratorTests.cs
git commit -m "Generate typed local actor refs"
```

---

## Task 6: Generate Remote Actor Refs

**Files:**
- Modify: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Modify: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`

- [ ] **Step 1: Add failing remote ref assertions**

Extend the generator smoke test with:

```csharp
Assert.Contains("var payload = _serializer.Serialize(request);", result.GeneratedSource);
Assert.Contains("new global::ULinkGame.Server.Actors.RemoteActorInvocation", result.GeneratedSource);
Assert.Contains("var reply = _serializer.Deserialize<JoinRoomReply>(result.Payload);", result.GeneratedSource);
Assert.Contains("throw new global::ULinkGame.Server.Actors.RemoteActorException", result.GeneratedSource);
```

- [ ] **Step 2: Run generator tests and verify failure**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj --filter "FullyQualifiedName~Generates_accessor_group_for_actor_key_base_type"
```

Expected: FAIL because remote ref body is not complete.

- [ ] **Step 3: Generate remote method body for request-reply**

For `ValueTask<TReply>` methods, generate:

```csharp
public async global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync(
    JoinRoomRequest request,
    global::System.Threading.CancellationToken cancellationToken = default)
{
    var actorId = global::ULinkGame.Server.Actors.ActorId.From("room/" + _id.Value);
    var payload = _serializer.Serialize(request);
    var correlationId = global::System.Guid.NewGuid().ToString("N");
    var deadline = global::System.DateTimeOffset.UtcNow.Add(_options.DefaultTimeout);
    var invocation = new global::ULinkGame.Server.Actors.RemoteActorInvocation(
        _node,
        actorId,
        "room",
        "join",
        payload,
        deadline,
        correlationId);
    var result = await _remote.AskAsync(invocation, cancellationToken).ConfigureAwait(false);
    if (result.Status != global::ULinkGame.Server.Actors.RemoteActorStatus.Replied)
    {
        throw new global::ULinkGame.Server.Actors.RemoteActorException(
            result.Status,
            actorId,
            "room",
            "join",
            result.Message ?? "Remote actor call failed.",
            _node,
            correlationId);
    }

    return _serializer.Deserialize<JoinRoomReply>(result.Payload);
}
```

- [ ] **Step 4: Generate remote method body for one-way methods**

For `ValueTask` methods, generate:

```csharp
public async global::System.Threading.Tasks.ValueTask LeaveAsync(
    LeaveRoomRequest request,
    global::System.Threading.CancellationToken cancellationToken = default)
{
    var actorId = global::ULinkGame.Server.Actors.ActorId.From("room/" + _id.Value);
    var payload = _serializer.Serialize(request);
    var correlationId = global::System.Guid.NewGuid().ToString("N");
    var deadline = global::System.DateTimeOffset.UtcNow.Add(_options.DefaultTimeout);
    var invocation = new global::ULinkGame.Server.Actors.RemoteActorInvocation(
        _node,
        actorId,
        "room",
        "leave",
        payload,
        deadline,
        correlationId);
    var result = await _remote.TellAsync(invocation, cancellationToken).ConfigureAwait(false);
    if (result.Status != global::ULinkGame.Server.Actors.RemoteActorStatus.Accepted)
    {
        throw new global::ULinkGame.Server.Actors.RemoteActorException(
            result.Status,
            actorId,
            "room",
            "leave",
            result.Message ?? "Remote actor send failed.",
            _node,
            correlationId);
    }
}
```

- [ ] **Step 5: Run generator tests and verify pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit remote ref generation**

Run:

```powershell
git add src\ULinkGame.Server.Generators\TypedActorGenerator.cs Tests\ULinkGame.Server.Generators.Tests\TypedActorGeneratorTests.cs
git commit -m "Generate typed remote actor refs"
```

---

## Task 7: Attribute Support and Diagnostics

**Files:**
- Modify: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Modify: `src/ULinkGame.Server.Generators/TypedActorGeneratorDiagnostics.cs`
- Modify: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`

- [ ] **Step 1: Add tests for actor and method name attributes**

Append:

```csharp
[Fact]
public void Uses_explicit_actor_and_method_names()
{
    var result = GeneratorTestHost.Run("""
        using System.Threading;
        using System.Threading.Tasks;
        using ULinkGame.Server.Actors;

        namespace Game.Server;

        public readonly record struct RoomId(string Value);
        public sealed record JoinRoomRequest(string PlayerId);
        public sealed record JoinRoomReply(bool Accepted);

        [ActorName("battle-room")]
        public sealed class BattleRoomActor : Actor<RoomId>
        {
            [ActorMethod("join")]
            public ValueTask<JoinRoomReply> EnterAsync(
                JoinRoomRequest request,
                CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(new JoinRoomReply(true));
            }
        }
        """);

    Assert.Empty(result.ErrorDiagnostics);
    Assert.Contains("\"battle-room/\" + _id.Value", result.GeneratedSource);
    Assert.Contains("\"join\"", result.GeneratedSource);
}
```

- [ ] **Step 2: Add tests for ignore and local-only attributes**

Append:

```csharp
[Fact]
public void ActorIgnore_skips_public_method()
{
    var result = GeneratorTestHost.Run("""
        using System.Threading;
        using System.Threading.Tasks;
        using ULinkGame.Server.Actors;

        namespace Game.Server;

        public readonly record struct RoomId(string Value);
        public sealed record PingRequest;

        public sealed class RoomActor : Actor<RoomId>
        {
            [ActorIgnore]
            public ValueTask HiddenAsync(PingRequest request, CancellationToken cancellationToken = default)
            {
                return ValueTask.CompletedTask;
            }
        }
        """);

    Assert.Empty(result.ErrorDiagnostics);
    Assert.DoesNotContain("HiddenAsync", result.GeneratedSource);
}

[Fact]
public void ActorLocalOnly_skips_remote_ref()
{
    var result = GeneratorTestHost.Run("""
        using System.Threading;
        using System.Threading.Tasks;
        using ULinkGame.Server.Actors;

        namespace Game.Server;

        public readonly record struct MetricsId(string Value);
        public sealed record PingRequest;

        [ActorLocalOnly]
        public sealed class MetricsActor : Actor<MetricsId>
        {
            public ValueTask PingAsync(PingRequest request, CancellationToken cancellationToken = default)
            {
                return ValueTask.CompletedTask;
            }
        }
        """);

    Assert.Empty(result.ErrorDiagnostics);
    Assert.Contains("public MetricsLocalRef Local(MetricsId id)", result.GeneratedSource);
    Assert.DoesNotContain("MetricsRemoteRef", result.GeneratedSource);
}
```

- [ ] **Step 3: Add tests for unsupported signatures**

Append:

```csharp
[Fact]
public void Unsupported_public_method_reports_warning()
{
    var result = GeneratorTestHost.Run("""
        using ULinkGame.Server.Actors;

        namespace Game.Server;

        public readonly record struct RoomId(string Value);

        public sealed class RoomActor : Actor<RoomId>
        {
            public int Count()
            {
                return 1;
            }
        }
        """);

    Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ULINKACTOR001");
}
```

- [ ] **Step 4: Run generator tests and verify failure**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: FAIL until attributes and diagnostics are implemented.

- [ ] **Step 5: Implement attribute handling and diagnostics**

Update generator logic:

```csharp
// ActorNameAttribute:
// read ConstructorArguments[0].Value as actor wire name.

// ActorMethodAttribute:
// read ConstructorArguments[0].Value as method wire id.

// ActorIgnoreAttribute:
// skip the method before signature validation.

// ActorLocalOnlyAttribute:
// generate RoomActors.Local and RoomLocalRef only.

// Unsupported public method:
// report TypedActorGeneratorDiagnostics.UnsupportedMethodSignature
// at the method declaration location.
```

- [ ] **Step 6: Run generator tests and verify pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit attribute support**

Run:

```powershell
git add src\ULinkGame.Server.Generators Tests\ULinkGame.Server.Generators.Tests
git commit -m "Support typed actor generator attributes"
```

---

## Task 8: Generated Dispatcher Foundation

**Files:**
- Modify: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Modify: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`
- Create: `Tests/ULinkGame.Server.Tests/TypedActorDispatcherTests.cs`

- [ ] **Step 1: Add generator assertions for dispatcher output**

Append to the smoke test:

```csharp
Assert.Contains("public sealed class RoomActorClusterHandler", result.GeneratedSource);
Assert.Contains("public async global::System.Threading.Tasks.ValueTask<global::ULinkGame.Cluster.ClusterSendStatus> HandleAsync", result.GeneratedSource);
Assert.Contains("case \"join\":", result.GeneratedSource);
Assert.Contains("RemoteActorGateway.SendReplyAsync", result.GeneratedSource);
```

- [ ] **Step 2: Run generator tests and verify failure**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj --filter "FullyQualifiedName~Generates_accessor_group_for_actor_key_base_type"
```

Expected: FAIL because dispatcher output is not generated.

- [ ] **Step 3: Generate cluster handler for actor methods**

Generate a handler equivalent to:

```csharp
public sealed class RoomActorClusterHandler : global::ULinkGame.Cluster.IClusterMessageHandler
{
    private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;
    private readonly global::ULinkGame.Server.Actors.IRemoteActorSerializer _serializer;
    private readonly global::ULinkGame.Cluster.IClusterRouter _router;

    public RoomActorClusterHandler(
        global::ULinkGame.Server.Actors.IActorRuntime runtime,
        global::ULinkGame.Server.Actors.IRemoteActorSerializer serializer,
        global::ULinkGame.Cluster.IClusterRouter router)
    {
        _runtime = runtime;
        _serializer = serializer;
        _router = router;
    }

    public async global::System.Threading.Tasks.ValueTask<global::ULinkGame.Cluster.ClusterSendStatus> HandleAsync(
        global::ULinkGame.Cluster.ClusterMessage message,
        global::System.Threading.CancellationToken cancellationToken = default)
    {
        if (!global::ULinkGame.Cluster.ClusterActorEnvelope.TryFromClusterMessage(message, out var envelope) || envelope is null)
        {
            return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;
        }

        if (!envelope.ActorId.StartsWith("room/", global::System.StringComparison.Ordinal))
        {
            return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;
        }

        var actorId = global::ULinkGame.Server.Actors.ActorId.From(envelope.ActorId);
        switch (envelope.Kind)
        {
            case "join":
            {
                var request = _serializer.Deserialize<JoinRoomRequest>(envelope.Payload);
                var reply = await _runtime.AskAsync<global::Game.Server.RoomActor, JoinRoomReply>(
                    actorId,
                    (actor, ct) => actor.JoinAsync(request, ct),
                    cancellationToken).ConfigureAwait(false);
                if (envelope.ReplyCorrelationId is not null)
                {
                    await global::ULinkGame.Server.Actors.RemoteActorGateway.SendReplyAsync(
                        _router,
                        envelope.SourceNode,
                        envelope.ReplyCorrelationId,
                        _serializer.Serialize(reply),
                        cancellationToken).ConfigureAwait(false);
                }

                return global::ULinkGame.Cluster.ClusterSendStatus.Accepted;
            }

            default:
                return global::ULinkGame.Cluster.ClusterSendStatus.RouteNotFound;
        }
    }
}
```

- [ ] **Step 4: Add runtime test for generated dispatcher behavior**

Create `Tests/ULinkGame.Server.Tests/TypedActorDispatcherTests.cs` after generator compilation is wired into a sample test project. Use a hand-written equivalent of `RoomActorClusterHandler` if generator output is not directly available to this test project. Assert:

```csharp
// A "join" ClusterActorEnvelope dispatches to RoomActor through IActorRuntime.
// The handler returns ClusterSendStatus.Accepted.
// Unknown method kind returns ClusterSendStatus.RouteNotFound.
```

Implement the test with the same `RoomActor` shape from generator tests and existing `ClusterActorDispatcherTests` patterns.

- [ ] **Step 5: Run server and generator tests**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter "FullyQualifiedName~TypedActorDispatcher"
```

Expected: PASS.

- [ ] **Step 6: Commit dispatcher generation**

Run:

```powershell
git add src\ULinkGame.Server.Generators Tests\ULinkGame.Server.Generators.Tests Tests\ULinkGame.Server.Tests\TypedActorDispatcherTests.cs
git commit -m "Generate typed actor cluster handlers"
```

---

## Task 9: Service Registration Extensions for Generated Types

**Files:**
- Modify: `src/ULinkGame.Server.Generators/TypedActorGenerator.cs`
- Modify: `Tests/ULinkGame.Server.Generators.Tests/TypedActorGeneratorTests.cs`

- [ ] **Step 1: Add failing assertion for DI registration extension**

Append:

```csharp
Assert.Contains("public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddRoomActors", result.GeneratedSource);
Assert.Contains("services.TryAddSingleton<RoomActors>();", result.GeneratedSource);
Assert.Contains("services.TryAddEnumerable", result.GeneratedSource);
Assert.Contains("RoomActorClusterHandler", result.GeneratedSource);
```

- [ ] **Step 2: Add package references needed by generated extension**

Modify `src/ULinkGame.Server.Generators/ULinkGame.Server.Generators.csproj` only if the generator itself needs compile-time references. The generated code can reference `Microsoft.Extensions.DependencyInjection` because consuming server projects already reference hosting abstractions through `ULinkGame.Server`.

- [ ] **Step 3: Generate registration extension**

For `RoomActor`, generate:

```csharp
public static class RoomActorServiceCollectionExtensions
{
    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddRoomActors(
        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<RoomActors>(services);
        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<
                global::ULinkGame.Cluster.IClusterMessageHandler,
                RoomActorClusterHandler>());
        return services;
    }
}
```

- [ ] **Step 4: Run generator tests and verify pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit service registration generation**

Run:

```powershell
git add src\ULinkGame.Server.Generators Tests\ULinkGame.Server.Generators.Tests
git commit -m "Generate typed actor service registration"
```

---

## Task 10: Package and Tool Integration

**Files:**
- Modify: `src/ULinkGame.Tool/ULinkGame.Tool.csproj`
- Modify: `src/ULinkGame.Tool/Scaffolding/ProjectScaffolder.cs`
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Modify: `src/ULinkGame.Tool/README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add generator version extraction to tool project**

In `src/ULinkGame.Tool/ULinkGame.Tool.csproj`, add an `XmlPeek` entry parallel to the existing package-version generation:

```xml
<XmlPeek
  XmlInputPath="$(MSBuildProjectDirectory)\..\ULinkGame.Server.Generators\ULinkGame.Server.Generators.csproj"
  Query="/Project/PropertyGroup/Version/text()">
  <Output TaskParameter="Result" PropertyName="ULinkGameServerGeneratorsPackageVersion" />
</XmlPeek>
```

Add generated constant line:

```xml
<GeneratedToolPackageVersionsLine Include="    public const string ULinkGameServerGenerators = &quot;$(ULinkGameServerGeneratorsPackageVersion)&quot;%3B" />
```

- [ ] **Step 2: Add server generator package reference to scaffolding**

In `src/ULinkGame.Tool/Scaffolding/ProjectScaffolder.cs`, add:

```csharp
EnsurePackageReference(
    project,
    "ULinkGame.Server.Generators",
    ToolPackageVersions.ULinkGameServerGenerators,
    privateAssets: "all",
    outputItemType: "Analyzer");
```

If the helper does not support `PrivateAssets` and `OutputItemType`, extend it to emit:

```xml
<PackageReference Include="ULinkGame.Server.Generators" Version="..." PrivateAssets="all" OutputItemType="Analyzer" />
```

- [ ] **Step 3: Update generated server template to use typed actor accessors**

In `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`, update the sample actor shape to:

```csharp
public readonly record struct PlayerActorId(string Value);

public sealed class PlayerActor : Actor<PlayerActorId>
{
    public ValueTask<PingReply> PingAsync(PingRequest request, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new PingReply($"pong:{request.Message}"));
    }
}
```

Update generated `Program.cs` service registration to include the generated extension:

```csharp
builder.Services.AddPlayerActors();
```

- [ ] **Step 4: Update docs and changelog**

In `src/ULinkGame.Tool/README.md`, add a short note:

```markdown
Generated server projects reference `ULinkGame.Server.Generators` as an analyzer so `Actor<TKey>` classes get typed `Local(id)` and `Remote(nodeId, id)` accessors at build time.
```

In `CHANGELOG.md`, add an unreleased entry dated `2026-06-01`:

```markdown
## 2026-06-01

### Added

- Added the typed remote actor API plan and generator integration direction for `Actor<TKey>` local/remote accessors.
```

- [ ] **Step 5: Run tool tests**

Run:

```powershell
dotnet test Tests\ULinkGame.Tool.Tests\ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit tool integration**

Run:

```powershell
git add src\ULinkGame.Tool src\ULinkGame.Tool\README.md CHANGELOG.md
git commit -m "Integrate typed actor generator into scaffolding"
```

---

## Task 11: Full Verification

**Files:**
- Verify all changed files.

- [ ] **Step 1: Run generator tests**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Generators.Tests\ULinkGame.Server.Generators.Tests.csproj
```

Expected: PASS.

- [ ] **Step 2: Run server tests**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Run full suite**

Run:

```powershell
dotnet test Tests\tests.slnx
```

Expected: PASS.

- [ ] **Step 4: Inspect public API and docs**

Run:

```powershell
rg "AskRemoteAsync|TellRemoteAsync|Actor<TKey>|RoomActors|RemoteActorException" docs src\ULinkGame.Server src\ULinkGame.Server.Generators
```

Expected: docs recommend typed `Local/Remote` refs; current low-level methods are described as lower-level or compatibility APIs.

- [ ] **Step 5: Commit final docs adjustments**

If Step 4 reveals stale docs, update them and commit:

```powershell
git add docs src\ULinkGame.Server\README.md
git commit -m "Update docs for typed actor accessors"
```

Skip this commit when Step 4 finds no stale documentation.

---

## Self-Review

**Spec coverage:** The plan covers `Actor<TKey>`, default generation for actor classes, typed `Local(id)` and `Remote(nodeId, id)` refs, no generated `TryXxxAsync` methods, `RemoteActorException`, server-only generator placement, optional naming attributes, remote invoker layering, dispatcher generation, and tooling integration.

**Scope control:** `Route(id)` is intentionally outside this implementation plan because the spec says automatic route lookup should wait until route-directory policy is mature. This plan leaves the API open for adding `Route(id)` later without changing `Local(id)` or `Remote(nodeId, id)`.

**Type consistency:** The plan consistently uses `Actor<TKey>`, `RoomActors`, `RoomLocalRef`, `RoomRemoteRef`, `IRemoteActorInvoker`, `IRemoteActorSerializer`, `RemoteActorInvocation`, `RemoteActorInvocationResult`, `RemoteActorException`, and `RemoteActorStatus`.

**Verification:** Each implementation task includes a focused failing test, a pass check, and a commit point. Full suite verification is the final task.
