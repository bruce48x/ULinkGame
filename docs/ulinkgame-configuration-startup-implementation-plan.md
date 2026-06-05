# ULinkGame Configuration Startup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the canonical `ULinkGame` configuration and startup model from `docs/ulinkgame-configuration-startup.md`.

**Architecture:** Add a resolved runtime model in `ULinkGame.Server` that supports `Endpoints[]`, minimal `Cluster`, and Feature Catalog selection. Keep endpoint transport hosting framework-owned. Update generated projects and Agar sample to use the same schema.

**Tech Stack:** .NET 10, Microsoft.Extensions.Configuration/DependencyInjection/Hosting, xUnit, ULinkRPC transports, existing ULinkGame guardrails and Feature APIs.

---

## File Structure

Create or modify these files:

- Modify `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`: replace single endpoint with endpoint collection and add resolved Feature/Cluster data needed by validators.
- Modify `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`: add endpoint URI helpers or introduce a collection-friendly endpoint record.
- Create `src/ULinkGame.Server/Configuration/ULinkGameRuntimeOptions.cs`: bind `ULinkGame:Node`, `ULinkGame:Endpoints`, `ULinkGame:Feature`, and `ULinkGame:Cluster` from `IConfiguration`.
- Create `src/ULinkGame.Server/Configuration/ULinkGameEndpointOptions.cs`: raw endpoint options and validation-friendly parsing helpers.
- Create `src/ULinkGame.Server/Configuration/ULinkGameClusterOptions.cs`: raw minimal cluster options.
- Create `src/ULinkGame.Server/Features/ULinkGameFeatureCatalog.cs`: registration store for named Feature definitions.
- Create `src/ULinkGame.Server/Features/ULinkGameFeatureDefinition.cs`: Feature metadata, ordering, transport, cluster, and Feature dependencies.
- Create `src/ULinkGame.Server/Features/ULinkGameFeatureContext.cs`: framework context passed to active Features.
- Create `src/ULinkGame.Server/Features/ULinkGameFeature.cs`: base class for Feature implementations.
- Create `src/ULinkGame.Server/Features/ULinkGameFeatureCatalogBuilder.cs`: fluent API used by `Program.cs`.
- Modify `src/ULinkGame.Server/Features/FeatureServiceCollectionExtensions.cs`: add `AddULinkGame(configuration, configureCatalog)` while keeping old `AddFeatures` only until callers are migrated in the same implementation.
- Modify `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`: validate endpoint array rules.
- Create `src/ULinkGame.Server/Guardrails/Rules/FeatureCatalogRule.cs`: validate active Feature names and dependencies.
- Create `src/ULinkGame.Server/Guardrails/Rules/ClusterEndpointRule.cs`: validate minimal cluster config.
- Modify `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`: register new validation rules.
- Modify `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`: update guardrail tests for endpoints, cluster, and Feature dependencies.
- Create `Tests/ULinkGame.Server.Tests/Configuration/ULinkGameRuntimeOptionsTests.cs`: configuration binding tests.
- Modify `Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs`: migrate or replace role/filter tests with Feature Catalog tests.
- Modify `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`: generate `ULinkGame:Endpoints[]`, use runtime options from `ULinkGame.Server`, and remove generated single-endpoint compatibility options.
- Modify `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`: update template assertions for `Endpoints[]`, no `Endpoint`, no top-level `ControlPlane`/`Realtime`, no `Services`.
- Modify `Tests/ULinkGame.Tool.Tests/ToolTextTests.cs`: update any generated text expectations that refer to old runtime options.
- Modify `samples/Agar.Unity/Server/Gateway/appsettings.json`: move WebSocket and KCP endpoints under `ULinkGame:Endpoints`.
- Modify `samples/Agar.Unity/Server/Gateway/Program.cs`: use `AddULinkGame` Feature Catalog.
- Modify `samples/Agar.Unity/Server/Gateway/Features/GatewayCoreFeature.cs`: convert to a `ULinkGameFeature`.
- Modify `samples/Agar.Unity/Server/Gateway/Features/GatewayBusinessFeature.cs`: convert to Feature Catalog declarations and resolved endpoint access.
- Modify `samples/Agar.Unity/Server/Gateway/Services/GatewayNodeIdentity.cs`: read resolved endpoint catalog instead of `Gateway:NodeId` and `RealtimeRpcServerOptions`.
- Modify `CONTRIBUTING.md`: add the configuration/startup design doc to the required reading list.

## Task 1: Runtime Configuration Binding

**Files:**
- Create: `src/ULinkGame.Server/Configuration/ULinkGameRuntimeOptions.cs`
- Create: `src/ULinkGame.Server/Configuration/ULinkGameEndpointOptions.cs`
- Create: `src/ULinkGame.Server/Configuration/ULinkGameClusterOptions.cs`
- Create: `Tests/ULinkGame.Server.Tests/Configuration/ULinkGameRuntimeOptionsTests.cs`

- [ ] **Step 1: Write failing binding tests**

Add `Tests/ULinkGame.Server.Tests/Configuration/ULinkGameRuntimeOptionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using ULinkGame.Server.Configuration;

namespace ULinkGame.Server.Tests.Configuration;

public sealed class ULinkGameRuntimeOptionsTests
{
    [Fact]
    public void FromConfiguration_binds_node_endpoints_feature_and_cluster()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "game-c",
            ["ULinkGame:Endpoints:0:Transport"] = "websocket",
            ["ULinkGame:Endpoints:0:Host"] = "0.0.0.0",
            ["ULinkGame:Endpoints:0:Port"] = "20000",
            ["ULinkGame:Endpoints:0:Path"] = "/ws",
            ["ULinkGame:Endpoints:1:Transport"] = "kcp",
            ["ULinkGame:Endpoints:1:Host"] = "0.0.0.0",
            ["ULinkGame:Endpoints:1:Port"] = "20001",
            ["ULinkGame:Feature:0"] = "battle",
            ["ULinkGame:Feature:1"] = "battle-settlement",
            ["ULinkGame:Cluster:Endpoint"] = "tcp://10.0.0.3:21003",
            ["ULinkGame:Cluster:Seeds:0"] = "tcp://10.0.0.1:21001"
        });

        var options = ULinkGameRuntimeOptions.FromConfiguration(configuration);

        Assert.Equal("game-c", options.Node.Id);
        Assert.Collection(
            options.Endpoints,
            endpoint =>
            {
                Assert.Equal("websocket", endpoint.Transport);
                Assert.Equal("0.0.0.0", endpoint.Host);
                Assert.Equal(20000, endpoint.Port);
                Assert.Equal("/ws", endpoint.Path);
            },
            endpoint =>
            {
                Assert.Equal("kcp", endpoint.Transport);
                Assert.Equal("0.0.0.0", endpoint.Host);
                Assert.Equal(20001, endpoint.Port);
                Assert.Equal("", endpoint.Path);
            });
        Assert.Equal(["battle", "battle-settlement"], options.Feature);
        Assert.NotNull(options.Cluster);
        Assert.Equal("tcp://10.0.0.3:21003", options.Cluster.Endpoint);
        Assert.Equal(["tcp://10.0.0.1:21001"], options.Cluster.Seeds);
    }

    [Fact]
    public void FromConfiguration_defaults_feature_to_null_and_cluster_to_null()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "dev-1"
        });

        var options = ULinkGameRuntimeOptions.FromConfiguration(configuration);

        Assert.Equal("dev-1", options.Node.Id);
        Assert.Empty(options.Endpoints);
        Assert.Null(options.Feature);
        Assert.Null(options.Cluster);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeOptionsTests
```

Expected: FAIL because `ULinkGame.Server.Configuration` and `ULinkGameRuntimeOptions` do not exist.

- [ ] **Step 3: Implement raw options types**

Create `src/ULinkGame.Server/Configuration/ULinkGameEndpointOptions.cs`:

```csharp
namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameEndpointOptions
{
    public string Transport { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Path { get; init; } = "";
    public string AdvertisedHost { get; init; } = "";

    public string ToAdvertisedEndpoint()
    {
        var scheme = Transport.ToLowerInvariant() switch
        {
            "websocket" => "ws",
            "tcp" => "tcp",
            "kcp" => "kcp",
            _ => Transport.ToLowerInvariant()
        };
        var host = string.IsNullOrWhiteSpace(AdvertisedHost) ? Host : AdvertisedHost;
        return string.IsNullOrWhiteSpace(Path)
            ? $"{scheme}://{host}:{Port}"
            : $"{scheme}://{host}:{Port}{Path}";
    }
}
```

Create `src/ULinkGame.Server/Configuration/ULinkGameClusterOptions.cs`:

```csharp
namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameClusterOptions
{
    public string Endpoint { get; init; } = "";
    public IReadOnlyList<string> Seeds { get; init; } = Array.Empty<string>();
}
```

Create `src/ULinkGame.Server/Configuration/ULinkGameRuntimeOptions.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameRuntimeOptions
{
    public ULinkGameNodeOptions Node { get; init; } = new();
    public IReadOnlyList<ULinkGameEndpointOptions> Endpoints { get; init; } = Array.Empty<ULinkGameEndpointOptions>();
    public IReadOnlyList<string>? Feature { get; init; }
    public ULinkGameClusterOptions? Cluster { get; init; }

    public static ULinkGameRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("ULinkGame");
        return new ULinkGameRuntimeOptions
        {
            Node = section.GetSection("Node").Get<ULinkGameNodeOptions>() ?? new ULinkGameNodeOptions(),
            Endpoints = section.GetSection("Endpoints").Get<ULinkGameEndpointOptions[]>() ?? Array.Empty<ULinkGameEndpointOptions>(),
            Feature = section.GetSection("Feature").Exists()
                ? section.GetSection("Feature").Get<string[]>() ?? Array.Empty<string>()
                : null,
            Cluster = section.GetSection("Cluster").Exists()
                ? section.GetSection("Cluster").Get<ULinkGameClusterOptions>() ?? new ULinkGameClusterOptions()
                : null
        };
    }
}

public sealed class ULinkGameNodeOptions
{
    public string Id { get; init; } = "";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeOptionsTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ULinkGame.Server/Configuration Tests/ULinkGame.Server.Tests/Configuration
git commit -m "Add ULinkGame runtime configuration options"
```

## Task 2: Resolved Runtime And Guardrails

**Files:**
- Modify: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`
- Modify: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedFeature.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedClusterEndpoint.cs`
- Modify: `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`
- Create: `src/ULinkGame.Server/Guardrails/Rules/ClusterEndpointRule.cs`
- Modify: `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`
- Modify: `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`

- [ ] **Step 1: Write failing endpoint and cluster guardrail tests**

Add these tests to `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`:

```csharp
[Fact]
public void EndpointRule_rejects_duplicate_transports()
{
    var runtime = TestRuntime() with
    {
        Endpoints =
        [
            TestEndpoint("kcp", "127.0.0.1", 20000),
            TestEndpoint("kcp", "127.0.0.1", 20001)
        ]
    };

    var result = Validate(runtime);

    Assert.Contains(result.Diagnostics, d => d.Code == "ULINK024");
}

[Fact]
public void EndpointRule_rejects_websocket_without_path()
{
    var runtime = TestRuntime() with
    {
        Endpoints = [TestEndpoint("websocket", "127.0.0.1", 20000, path: "")]
    };

    var result = Validate(runtime);

    Assert.Contains(result.Diagnostics, d => d.Code == "ULINK023");
}

[Fact]
public void EndpointRule_rejects_kcp_with_path()
{
    var runtime = TestRuntime() with
    {
        Endpoints = [TestEndpoint("kcp", "127.0.0.1", 20000, path: "/bad")]
    };

    var result = Validate(runtime);

    Assert.Contains(result.Diagnostics, d => d.Code == "ULINK025");
}

[Fact]
public void ClusterEndpointRule_rejects_missing_endpoint_when_cluster_is_configured()
{
    var runtime = TestRuntime() with
    {
        ClusterEndpoint = new ULinkGameResolvedClusterEndpoint(
            Endpoint: new ULinkGameResolvedValue<string>("", ULinkGameValueSource.Configuration, "ULinkGame:Cluster:Endpoint"),
            Seeds: [])
    };

    var result = Validate(runtime);

    Assert.Contains(result.Diagnostics, d => d.Code == "ULINK040");
}

private static ULinkGameResolvedEndpoint TestEndpoint(
    string transport,
    string host,
    int port,
    string path = "",
    string advertisedHost = "")
{
    return new ULinkGameResolvedEndpoint(
        Transport: new ULinkGameResolvedValue<string>(transport, ULinkGameValueSource.Configuration),
        Host: new ULinkGameResolvedValue<string>(host, ULinkGameValueSource.Configuration),
        Port: new ULinkGameResolvedValue<int>(port, ULinkGameValueSource.Configuration),
        Path: new ULinkGameResolvedValue<string>(path, ULinkGameValueSource.Configuration),
        AdvertisedHost: new ULinkGameResolvedValue<string>(advertisedHost, ULinkGameValueSource.Configuration),
        AdvertisedEndpoint: new ULinkGameResolvedValue<string>($"{transport}://{host}:{port}{path}", ULinkGameValueSource.GeneratedConvention));
}
```

Update the existing `TestRuntime()` helper to set `Endpoints = [TestEndpoint("kcp", "127.0.0.1", 20000)]` instead of `Endpoint = ...`.

- [ ] **Step 2: Run guardrail tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter Guardrails
```

Expected: FAIL because resolved runtime shape and new rules are not implemented.

- [ ] **Step 3: Implement resolved runtime shape**

Modify `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedEndpoint(
    ULinkGameResolvedValue<string> Transport,
    ULinkGameResolvedValue<string> Host,
    ULinkGameResolvedValue<int> Port,
    ULinkGameResolvedValue<string> Path,
    ULinkGameResolvedValue<string> AdvertisedHost,
    ULinkGameResolvedValue<string> AdvertisedEndpoint);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedClusterEndpoint.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedClusterEndpoint(
    ULinkGameResolvedValue<string> Endpoint,
    IReadOnlyList<string> Seeds);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedFeature.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedFeature(
    IReadOnlyList<string>? Configured,
    IReadOnlyList<string> Active,
    IReadOnlyList<string> StartupOrder);
```

Modify `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedRuntime(
    ULinkGameResolvedValue<string> NodeId,
    IReadOnlyList<ULinkGameResolvedEndpoint> Endpoints,
    ULinkGameResolvedCluster Cluster,
    ULinkGameResolvedClusterEndpoint? ClusterEndpoint,
    ULinkGameResolvedFeature Feature,
    ULinkGameResolvedHotfix Hotfix,
    ULinkGameResolvedReliablePush ReliablePush,
    ULinkGameRuntimeProfile Profile);
```

- [ ] **Step 4: Implement endpoint and cluster rules**

Modify `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class EndpointRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        var transports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bindAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in runtime.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Transport.Value))
            {
                yield return Error("ULINK020", "Endpoint transport is required.", endpoint.Transport.Path);
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host.Value))
            {
                yield return Error("ULINK021", "Endpoint host is required.", endpoint.Host.Path);
            }

            if (endpoint.Port.Value <= 0 || endpoint.Port.Value > 65535)
            {
                yield return Error("ULINK022", "Endpoint port must be between 1 and 65535.", endpoint.Port.Path);
            }

            if (string.Equals(endpoint.Transport.Value, "websocket", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(endpoint.Path.Value))
            {
                yield return Error("ULINK023", "WebSocket endpoint requires Path.", endpoint.Path.Path, "Set Path to a path such as /ws.");
            }

            if (string.Equals(endpoint.Transport.Value, "kcp", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(endpoint.Path.Value))
            {
                yield return Error("ULINK025", "KCP endpoint must not set Path.", endpoint.Path.Path, "Remove Path from the KCP endpoint.");
            }

            if (!string.IsNullOrWhiteSpace(endpoint.Transport.Value) &&
                !transports.Add(endpoint.Transport.Value))
            {
                yield return Error("ULINK024", $"Endpoint transport '{endpoint.Transport.Value}' is configured more than once.", endpoint.Transport.Path);
            }

            var bind = $"{endpoint.Host.Value}:{endpoint.Port.Value}";
            if (!string.IsNullOrWhiteSpace(endpoint.Host.Value) &&
                endpoint.Port.Value > 0 &&
                !bindAddresses.Add(bind))
            {
                yield return Error("ULINK026", $"Endpoint bind address '{bind}' is configured more than once.", endpoint.Port.Path);
            }
        }
    }

    private static ULinkGameDiagnostic Error(string code, string message, string? path, string? repair = null)
    {
        var fullMessage = string.IsNullOrWhiteSpace(path) ? message : $"{path}: {message}";
        return new ULinkGameDiagnostic(code, ULinkGameDiagnosticSeverity.Error, fullMessage, repair);
    }
}
```

Create `src/ULinkGame.Server/Guardrails/Rules/ClusterEndpointRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class ClusterEndpointRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (runtime.ClusterEndpoint is null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(runtime.ClusterEndpoint.Endpoint.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK040",
                ULinkGameDiagnosticSeverity.Error,
                "ULinkGame:Cluster:Endpoint is required when Cluster is configured.",
                "Set ULinkGame:Cluster:Endpoint to a URI such as tcp://127.0.0.1:21001.");
            yield break;
        }

        if (!Uri.TryCreate(runtime.ClusterEndpoint.Endpoint.Value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK041",
                ULinkGameDiagnosticSeverity.Error,
                "ULinkGame:Cluster:Endpoint must be a tcp URI.",
                "Use a value such as tcp://127.0.0.1:21001.");
        }

        foreach (var endpoint in runtime.Endpoints)
        {
            if (Uri.TryCreate(runtime.ClusterEndpoint.Endpoint.Value, UriKind.Absolute, out var clusterUri) &&
                endpoint.Port.Value == clusterUri.Port)
            {
                yield return new ULinkGameDiagnostic(
                    "ULINK042",
                    ULinkGameDiagnosticSeverity.Error,
                    $"Cluster endpoint port {clusterUri.Port} conflicts with a business endpoint.",
                    "Use a different port for ULinkGame:Cluster:Endpoint.");
            }
        }
    }
}
```

Modify `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs` to register `ClusterEndpointRule`.

- [ ] **Step 5: Run guardrail tests to verify they pass**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter Guardrails
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ULinkGame.Server/Guardrails Tests/ULinkGame.Server.Tests/Guardrails
git commit -m "Validate ULinkGame endpoint and cluster configuration"
```

## Task 3: Feature Catalog API

**Files:**
- Create: `src/ULinkGame.Server/Features/ULinkGameFeature.cs`
- Create: `src/ULinkGame.Server/Features/ULinkGameFeatureContext.cs`
- Create: `src/ULinkGame.Server/Features/ULinkGameFeatureDefinition.cs`
- Create: `src/ULinkGame.Server/Features/ULinkGameFeatureCatalog.cs`
- Create: `src/ULinkGame.Server/Features/ULinkGameFeatureCatalogBuilder.cs`
- Create: `src/ULinkGame.Server/Features/ULinkGameEndpointCatalog.cs`
- Modify: `src/ULinkGame.Server/Features/FeatureServiceCollectionExtensions.cs`
- Modify: `Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs`

- [ ] **Step 1: Write failing Feature Catalog tests**

Append tests to `Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs`:

```csharp
[Fact]
public void FeatureCatalog_enables_all_features_when_config_is_omitted()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "dev-1"
        })
        .Build();

    services.AddULinkGame(configuration, game =>
    {
        game.Feature<MarkerFeatureA>("login");
        game.Feature<MarkerFeatureB>("chat");
    });

    using var provider = services.BuildServiceProvider();
    var catalog = provider.GetRequiredService<ULinkGameFeatureCatalog>();

    Assert.Equal(["login", "chat"], catalog.ActiveNames);
}

[Fact]
public void FeatureCatalog_rejects_unknown_configured_feature()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "dev-1",
            ["ULinkGame:Feature:0"] = "missing"
        })
        .Build();

    var ex = Assert.Throws<InvalidOperationException>(() =>
        services.AddULinkGame(configuration, game => game.Feature<MarkerFeatureA>("login")));

    Assert.Contains("missing", ex.Message, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void FeatureCatalog_sorts_after_dependency()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "dev-1",
            ["ULinkGame:Feature:0"] = "battle",
            ["ULinkGame:Feature:1"] = "settlement"
        })
        .Build();

    services.AddULinkGame(configuration, game =>
    {
        game.Feature<MarkerFeatureA>("settlement").After("battle");
        game.Feature<MarkerFeatureB>("battle");
    });

    using var provider = services.BuildServiceProvider();
    var catalog = provider.GetRequiredService<ULinkGameFeatureCatalog>();

    Assert.Equal(["battle", "settlement"], catalog.ActiveNames);
}
```

- [ ] **Step 2: Run Feature tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter FeatureBuilderTests
```

Expected: FAIL because Feature Catalog API is not implemented.

- [ ] **Step 3: Implement Feature base/context/catalog**

Create `src/ULinkGame.Server/Features/ULinkGameFeature.cs`:

```csharp
namespace ULinkGame.Server.Features;

public abstract class ULinkGameFeature
{
    public virtual void ConfigureServices(ULinkGameFeatureContext context)
    {
    }
}
```

Create `src/ULinkGame.Server/Features/ULinkGameFeatureContext.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureContext
{
    public ULinkGameFeatureContext(
        IServiceCollection services,
        IConfiguration configuration,
        ULinkGameEndpointCatalog endpoints)
    {
        Services = services;
        Configuration = configuration;
        Endpoints = endpoints;
    }

    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }
    public ULinkGameEndpointCatalog Endpoints { get; }
}
```

Create `src/ULinkGame.Server/Features/ULinkGameEndpointCatalog.cs`:

```csharp
using ULinkGame.Server.Configuration;

namespace ULinkGame.Server.Features;

public sealed class ULinkGameEndpointCatalog
{
    private readonly IReadOnlyList<ULinkGameEndpointOptions> _endpoints;

    public ULinkGameEndpointCatalog(IReadOnlyList<ULinkGameEndpointOptions> endpoints)
    {
        _endpoints = endpoints;
    }

    public ULinkGameEndpointOptions RequireTransport(string transport)
    {
        var endpoint = _endpoints.FirstOrDefault(e => string.Equals(e.Transport, transport, StringComparison.OrdinalIgnoreCase));
        return endpoint ?? throw new InvalidOperationException($"Feature requires endpoint transport '{transport}', but it is not configured.");
    }
}
```

Create `src/ULinkGame.Server/Features/ULinkGameFeatureDefinition.cs`:

```csharp
namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureDefinition
{
    internal ULinkGameFeatureDefinition(string name, Type implementationType)
    {
        Name = name;
        ImplementationType = implementationType;
    }

    public string Name { get; }
    public Type ImplementationType { get; }
    public List<string> After { get; } = new();
    public List<string> RequiredFeatures { get; } = new();
    public List<string> RequiredTransports { get; } = new();
    public bool RequiresCluster { get; internal set; }
}
```

Create `src/ULinkGame.Server/Features/ULinkGameFeatureCatalog.cs`:

```csharp
namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureCatalog
{
    public ULinkGameFeatureCatalog(IReadOnlyList<ULinkGameFeatureDefinition> activeDefinitions)
    {
        ActiveDefinitions = activeDefinitions;
        ActiveNames = activeDefinitions.Select(feature => feature.Name).ToArray();
    }

    public IReadOnlyList<ULinkGameFeatureDefinition> ActiveDefinitions { get; }
    public IReadOnlyList<string> ActiveNames { get; }
}
```

- [ ] **Step 4: Implement catalog builder and AddULinkGame**

Create `src/ULinkGame.Server/Features/ULinkGameFeatureCatalogBuilder.cs`:

```csharp
namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureCatalogBuilder
{
    private readonly List<ULinkGameFeatureDefinition> _definitions = new();

    public ULinkGameFeatureRegistration Feature<TFeature>(string name)
        where TFeature : ULinkGameFeature
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Feature name is required.", nameof(name));
        }

        var definition = new ULinkGameFeatureDefinition(name, typeof(TFeature));
        _definitions.Add(definition);
        return new ULinkGameFeatureRegistration(definition);
    }

    internal IReadOnlyList<ULinkGameFeatureDefinition> Build(IReadOnlyList<string>? configured)
    {
        var allByName = _definitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        var activeNames = configured ?? _definitions.Select(d => d.Name).ToArray();
        var missing = activeNames.Where(name => !allByName.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Unknown ULinkGame Feature(s): {string.Join(", ", missing)}.");
        }

        var activeSet = activeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var active = activeNames.Select(name => allByName[name]).ToArray();
        foreach (var definition in active)
        {
            foreach (var required in definition.RequiredFeatures)
            {
                if (!activeSet.Contains(required))
                {
                    throw new InvalidOperationException($"Feature '{definition.Name}' requires Feature '{required}'.");
                }
            }
        }

        return Sort(active);
    }

    private static IReadOnlyList<ULinkGameFeatureDefinition> Sort(IReadOnlyList<ULinkGameFeatureDefinition> active)
    {
        var result = new List<ULinkGameFeatureDefinition>();
        var pending = active.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            var ready = pending.Values.FirstOrDefault(d => d.After.All(after => !pending.ContainsKey(after)));
            if (ready is null)
            {
                throw new InvalidOperationException("Feature startup order contains a cycle.");
            }

            result.Add(ready);
            pending.Remove(ready.Name);
        }

        return result;
    }
}

public sealed class ULinkGameFeatureRegistration
{
    private readonly ULinkGameFeatureDefinition _definition;

    internal ULinkGameFeatureRegistration(ULinkGameFeatureDefinition definition)
    {
        _definition = definition;
    }

    public ULinkGameFeatureRegistration After(string name)
    {
        _definition.After.Add(name);
        return this;
    }

    public ULinkGameFeatureRegistration RequiresFeature(string name)
    {
        _definition.RequiredFeatures.Add(name);
        return this;
    }

    public ULinkGameFeatureRegistration RequiresTransport(string transport)
    {
        _definition.RequiredTransports.Add(transport);
        return this;
    }

    public ULinkGameFeatureRegistration RequiresCluster()
    {
        _definition.RequiresCluster = true;
        return this;
    }
}
```

Modify `src/ULinkGame.Server/Features/FeatureServiceCollectionExtensions.cs` with:

```csharp
public static IServiceCollection AddULinkGame(
    this IServiceCollection services,
    IConfiguration config,
    Action<ULinkGameFeatureCatalogBuilder> configure)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(configure);

    var options = ULinkGame.Server.Configuration.ULinkGameRuntimeOptions.FromConfiguration(config);
    var builder = new ULinkGameFeatureCatalogBuilder();
    configure(builder);
    var active = builder.Build(options.Feature);
    var catalog = new ULinkGameFeatureCatalog(active);
    var endpointCatalog = new ULinkGameEndpointCatalog(options.Endpoints);
    var context = new ULinkGameFeatureContext(services, config, endpointCatalog);

    services.AddSingleton(options);
    services.AddSingleton(catalog);
    services.AddSingleton(endpointCatalog);

    foreach (var definition in active)
    {
        var feature = (ULinkGameFeature)Activator.CreateInstance(definition.ImplementationType)!;
        feature.ConfigureServices(context);
    }

    return services;
}
```

Add `using Microsoft.Extensions.Configuration;` and `using Microsoft.Extensions.DependencyInjection;` if missing.

- [ ] **Step 5: Run Feature tests to verify they pass**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter FeatureBuilderTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ULinkGame.Server/Features Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs
git commit -m "Add ULinkGame Feature Catalog startup API"
```

## Task 4: Feature Dependency Guardrails

**Files:**
- Create: `src/ULinkGame.Server/Guardrails/Rules/FeatureCatalogRule.cs`
- Modify: `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`
- Modify: `src/ULinkGame.Server/Features/FeatureServiceCollectionExtensions.cs`
- Modify: `Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs`

- [ ] **Step 1: Write failing dependency tests**

Add tests:

```csharp
[Fact]
public void AddULinkGame_rejects_feature_missing_required_transport()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "game-c",
            ["ULinkGame:Feature:0"] = "battle"
        })
        .Build();

    var ex = Assert.Throws<InvalidOperationException>(() =>
        services.AddULinkGame(configuration, game =>
        {
            game.Feature<MarkerFeatureA>("battle").RequiresTransport("kcp");
        }));

    Assert.Contains("kcp", ex.Message, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void AddULinkGame_rejects_feature_missing_cluster()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "game-b",
            ["ULinkGame:Feature:0"] = "chat"
        })
        .Build();

    var ex = Assert.Throws<InvalidOperationException>(() =>
        services.AddULinkGame(configuration, game =>
        {
            game.Feature<MarkerFeatureA>("chat").RequiresCluster();
        }));

    Assert.Contains("Cluster", ex.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run Feature tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter FeatureBuilderTests
```

Expected: FAIL because dependencies are not checked.

- [ ] **Step 3: Add dependency checks in `AddULinkGame`**

Before Feature instances are created in `FeatureServiceCollectionExtensions.AddULinkGame`, add:

```csharp
var transports = options.Endpoints
    .Select(endpoint => endpoint.Transport)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

foreach (var definition in active)
{
    foreach (var transport in definition.RequiredTransports)
    {
        if (!transports.Contains(transport))
        {
            throw new InvalidOperationException(
                $"Feature '{definition.Name}' requires endpoint transport '{transport}', but it is not configured.");
        }
    }

    if (definition.RequiresCluster && options.Cluster is null)
    {
        throw new InvalidOperationException(
            $"Feature '{definition.Name}' requires Cluster, but ULinkGame:Cluster is not configured.");
    }
}
```

- [ ] **Step 4: Run Feature tests to verify they pass**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter FeatureBuilderTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/ULinkGame.Server/Features Tests/ULinkGame.Server.Tests/FeatureBuilderTests.cs
git commit -m "Validate Feature Catalog dependencies"
```

## Task 5: Tool Template Schema Migration

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Modify: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`
- Modify: `Tests/ULinkGame.Tool.Tests/ToolTextTests.cs`

- [ ] **Step 1: Update tool template tests first**

Modify `RenderServerAppSettings_DefaultClusterProject_UsesCompactULinkGameSection` to assert:

```csharp
Assert.Contains("\"Endpoints\"", json);
Assert.Contains("\"Transport\": \"kcp\"", json);
Assert.DoesNotContain("\"Endpoint\"", json);
Assert.DoesNotContain("\"Deployment\"", json);
Assert.DoesNotContain("\"Services\"", json);
Assert.DoesNotContain("\"ControlPlane\"", json);
Assert.DoesNotContain("\"Realtime\"", json);
```

Modify `RenderServerAppSettings_WebSocketProject_IncludesEndpointPath` to assert:

```csharp
Assert.Contains("\"Endpoints\"", json);
Assert.Contains("\"Transport\": \"websocket\"", json);
Assert.Contains("\"Path\": \"/ws\"", json);
Assert.DoesNotContain("\"Endpoint\"", json);
```

Delete or rewrite `RenderGeneratedServerApplication_RealtimeProfile_ConfiguresNamedRpcServers` so the realtime profile expects `ULinkGame:Endpoints[]` instead of top-level `ControlPlane` and `Realtime`.

- [ ] **Step 2: Run tool tests to verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter ToolTemplateTests
```

Expected: FAIL because templates still emit `ULinkGame:Endpoint` and named top-level sections.

- [ ] **Step 3: Update `RenderServerAppSettings`**

Modify `ToolTemplates.RenderServerAppSettings` so default non-realtime emits:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoints": [
      {
        "Transport": "kcp",
        "Host": "127.0.0.1",
        "Port": 20000
      }
    ]
  }
}
```

For WebSocket, emit:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoints": [
      {
        "Transport": "websocket",
        "Host": "127.0.0.1",
        "Port": 20000,
        "Path": "/ws"
      }
    ]
  }
}
```

For realtime profile, emit both transports in the same array:

```json
"Endpoints": [
  {
    "Transport": "websocket",
    "Host": "127.0.0.1",
    "Port": 20000,
    "Path": "/ws"
  },
  {
    "Transport": "kcp",
    "Host": "127.0.0.1",
    "Port": 20001
  }
]
```

- [ ] **Step 4: Update generated hosting code**

In `ToolTemplates.RenderGeneratedServerApplication`, replace single endpoint `runtimeOptions.ToServerRpcServerOptions()` flow with endpoint collection flow:

```csharp
var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(runtimeOptions.ToServerRpcServerOptions("kcp"));
```

For generated realtime profile, remove `ControlPlane` and `Realtime` section reads. Resolve the WebSocket and KCP server options from `runtimeOptions`:

```csharp
builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
    runtimeOptions.ToServerRpcServerOptions("websocket")));
builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
    runtimeOptions.ToServerRpcServerOptions("kcp")));
```

Update generated `ULinkGameRuntimeOptions` in `RenderClusterOptions` to use `Endpoints`:

```csharp
public IReadOnlyList<ULinkGameEndpointOptions> Endpoints { get; init; } = Array.Empty<ULinkGameEndpointOptions>();

public ServerRpcServerOptions ToServerRpcServerOptions(string transport)
{
    var endpoint = Endpoints.FirstOrDefault(endpoint =>
        string.Equals(endpoint.Transport, transport, StringComparison.OrdinalIgnoreCase));
    if (endpoint is null)
    {
        throw new InvalidOperationException($"ULinkGame endpoint transport '{transport}' is not configured.");
    }

    return new ServerRpcServerOptions
    {
        Transport = endpoint.Transport,
        Host = endpoint.Host,
        Port = endpoint.Port,
        Path = endpoint.Path
    };
}
```

- [ ] **Step 5: Run tool tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ULinkGame.Tool Tests/ULinkGame.Tool.Tests
git commit -m "Generate canonical ULinkGame endpoint configuration"
```

## Task 6: Agar Sample Migration

**Files:**
- Modify: `samples/Agar.Unity/Server/Gateway/appsettings.json`
- Modify: `samples/Agar.Unity/Server/Gateway/Program.cs`
- Modify: `samples/Agar.Unity/Server/Gateway/Features/GatewayCoreFeature.cs`
- Modify: `samples/Agar.Unity/Server/Gateway/Features/GatewayBusinessFeature.cs`
- Modify: `samples/Agar.Unity/Server/Gateway/Services/GatewayNodeIdentity.cs`
- Modify: `samples/Agar.Unity/Server/Gateway/Hosting/ControlPlaneRpcServerOptions.cs`
- Modify: `samples/Agar.Unity/Server/Gateway/Hosting/RealtimeRpcServerOptions.cs`
- Modify: `samples/Agar.Unity/README.md` if it documents old top-level endpoint sections.

- [ ] **Step 1: Update Agar appsettings**

Replace `samples/Agar.Unity/Server/Gateway/appsettings.json` with:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "gateway-dev-1"
    },
    "Endpoints": [
      {
        "Transport": "websocket",
        "Host": "127.0.0.1",
        "Port": 20000,
        "Path": "/ws"
      },
      {
        "Transport": "kcp",
        "Host": "127.0.0.1",
        "Port": 20001
      }
    ]
  },
  "Hotfix": {
    "Source": "current-directory",
    "Directory": "../../../../Hotfix/bin/Debug/net10.0",
    "Assembly": "Agar.Sample.Hotfix.dll"
  }
}
```

- [ ] **Step 2: Update Agar startup to use Feature Catalog**

Modify `samples/Agar.Unity/Server/Gateway/Program.cs`:

```csharp
builder.Services.AddULinkGame(builder.Configuration, game =>
{
    game.Feature<GatewayCoreFeature>("gateway-core");
    game.Feature<GatewayBusinessFeature>("gateway-business")
        .After("gateway-core")
        .RequiresTransport("websocket")
        .RequiresTransport("kcp");
});
```

Remove `features.FromAssembly(typeof(GatewayRole).Assembly);` after the new catalog is wired.

- [ ] **Step 3: Convert Agar Features**

Change `GatewayCoreFeature` and `GatewayBusinessFeature` from `IFeature` to `ULinkGameFeature`.

`GatewayBusinessFeature.ConfigureServices` should get endpoints from context:

```csharp
var websocket = context.Endpoints.RequireTransport("websocket");
var realtime = context.Endpoints.RequireTransport("kcp");
services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
    new GatewayRpcServerOptions
    {
        Transport = websocket.Transport,
        Host = websocket.Host,
        Port = websocket.Port,
        Path = websocket.Path
    }));
services.AddSingleton(_ => new RealtimeRpcServerOptions(
    new GatewayRpcServerOptions
    {
        Transport = realtime.Transport,
        Host = realtime.Host,
        Port = realtime.Port,
        Path = realtime.Path
    }));
```

- [ ] **Step 4: Update GatewayNodeIdentity**

Modify `GatewayNodeIdentity` to use `ULinkGameRuntimeOptions`:

```csharp
public GatewayNodeIdentity(ULinkGameRuntimeOptions runtimeOptions)
{
    InstanceId = runtimeOptions.Node.Id;
    var realtime = runtimeOptions.Endpoints.First(endpoint =>
        string.Equals(endpoint.Transport, "kcp", StringComparison.OrdinalIgnoreCase));
    RealtimeEndpoint = new GatewayEndpointDescriptor
    {
        InstanceId = InstanceId,
        Transport = RealtimeTransportToString(realtime.Transport),
        Host = string.IsNullOrWhiteSpace(realtime.AdvertisedHost) ? realtime.Host : realtime.AdvertisedHost,
        Port = realtime.Port,
        Path = realtime.Path
    };
}
```

Add `using ULinkGame.Server.Configuration;`.

- [ ] **Step 5: Build Agar server**

Run:

```powershell
dotnet build samples/Agar.Unity/Server/Gateway/Gateway.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add samples/Agar.Unity/Server/Gateway samples/Agar.Unity/README.md
git commit -m "Migrate Agar sample to canonical ULinkGame configuration"
```

## Task 7: Documentation And Contributor Cross-Links

**Files:**
- Modify: `CONTRIBUTING.md`
- Modify: `docs/ulinkgame-tool-default-experience.md`
- Modify: `docs/ulinkgame-runtime-guardrails.md`
- Modify: `docs/ulinkgame-configuration-startup.md` if implementation reveals naming corrections.

- [ ] **Step 1: Update required reading list**

Add `docs/ulinkgame-configuration-startup.md` to `CONTRIBUTING.md` design documentation list:

```markdown
- **[ULinkGame Configuration And Startup Model](docs/ulinkgame-configuration-startup.md)** — The canonical `ULinkGame` configuration schema, Feature Catalog startup model, endpoint rules, and local validation boundary.
```

- [ ] **Step 2: Update tool default experience docs**

In `docs/ulinkgame-tool-default-experience.md`, replace single `Endpoint` examples with `Endpoints` array examples:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoints": [
      {
        "Transport": "kcp",
        "Host": "127.0.0.1",
        "Port": 20000
      }
    ]
  }
}
```

- [ ] **Step 3: Update guardrails docs**

In `docs/ulinkgame-runtime-guardrails.md`, replace references to `ULinkGame:Endpoint:*` with `ULinkGame:Endpoints:*` and add duplicate transport validation.

- [ ] **Step 4: Run docs grep checks**

Run:

```powershell
rg -n "ULinkGame:Endpoint|\"Endpoint\"|ControlPlane|Realtime|Services.Enabled|Deployment" docs src/ULinkGame.Tool samples/Agar.Unity/Server/Gateway
```

Expected: Any remaining matches are either historical context explicitly marked as removed, generated C# type names that remain valid, or sample business code where `ControlPlane`/`Realtime` are C# wrapper names rather than JSON sections.

- [ ] **Step 5: Commit**

```powershell
git add CONTRIBUTING.md docs
git commit -m "Document canonical ULinkGame configuration schema"
```

## Task 8: Full Verification

**Files:**
- No new source files. This task verifies all prior tasks together.

- [ ] **Step 1: Run server tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj
```

Expected: PASS.

- [ ] **Step 2: Run tool tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Run full test suite**

Run:

```powershell
dotnet test Tests/tests.slnx
```

Expected: PASS.

- [ ] **Step 4: Generate a sample project and inspect config**

Run:

```powershell
dotnet run --project src/ULinkGame.Tool/ULinkGame.Tool.csproj -- new --name VerifyConfig --output .tmp/VerifyConfig
```

Expected: command succeeds and `.tmp/VerifyConfig/Server/Server/appsettings.json` contains `ULinkGame.Endpoints` as an array and does not contain `ULinkGame.Endpoint`.

- [ ] **Step 5: Run generated check command**

Run:

```powershell
dotnet run --project .tmp/VerifyConfig/Server/Server/Server.csproj -- --ulinkgame-check
```

Expected: command prints node, endpoints, feature/startup-order, cluster when configured, hotfix status, and returns the expected status based on whether hotfix output has been built.

- [ ] **Step 6: Commit verification-only fixes if any**

If verification exposes compile, test, or documentation drift, make the smallest fix and commit it:

```powershell
git add <changed-files>
git commit -m "Stabilize ULinkGame configuration startup migration"
```

## Self-Review

Spec coverage:

- `Endpoints[]` only: covered by Tasks 1, 2, 5, 6, and 7.
- `Feature` selection and omitted-all behavior: covered by Tasks 3 and 4.
- Minimal `Cluster`: covered by Tasks 1 and 2.
- Framework-owned endpoint transport management: covered by Tasks 3, 5, and 6.
- Tool and Agar sample migration: covered by Tasks 5 and 6.
- No deployment-level validation: explicitly out of scope in Task 2 and docs.

Red-flag scan:

- This plan contains no unfinished markers or unspecified test-writing steps. Each code-changing task starts with concrete failing tests and includes commands.

Type consistency:

- Runtime options use `ULinkGameRuntimeOptions`, `ULinkGameEndpointOptions`, and `ULinkGameClusterOptions`.
- Feature API uses `ULinkGameFeature`, `ULinkGameFeatureContext`, `ULinkGameFeatureCatalog`, and `ULinkGameFeatureCatalogBuilder`.
- Resolved runtime uses `Endpoints` as a collection, not the old singular `Endpoint`.
