# ULinkGame Runtime Guardrails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first runtime guardrails loop so generated projects and server startup can validate ULinkGame runtime invariants with shared framework diagnostics.

**Architecture:** Add small diagnostic and validation primitives to `ULinkGame.Server`, introduce a resolved runtime model that records final values and provenance, implement the first low-risk validation rules, then make generated `--ulinkgame-check` consume the framework validation model. Keep rule ownership in runtime packages and keep generated code responsible only for project-specific presentation.

**Tech Stack:** C#/.NET 10, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Configuration, xUnit v3, ULinkGame.Server, ULinkGame.Tool templates.

---

## File Structure

- Create `src/ULinkGame.Server/Guardrails/ULinkGameDiagnosticSeverity.cs`: diagnostic severity enum.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameDiagnostic.cs`: stable diagnostic record with code, severity, message, and optional repair.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameValidationResult.cs`: aggregate validation result.
- Create `src/ULinkGame.Server/Guardrails/IULinkGameValidationRule.cs`: small rule interface.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeProfile.cs`: framework-owned profile values.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameValueSource.cs`: provenance enum.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedValue.cs`: value plus source/path.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`: resolved endpoint data.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedHotfix.cs`: resolved hotfix source data.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedCluster.cs`: resolved cluster service data.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedReliablePush.cs`: resolved reliable push data.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`: aggregate runtime model.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeValidator.cs`: rule aggregator.
- Create `src/ULinkGame.Server/Guardrails/Rules/NodeIdentityRule.cs`: node id validation.
- Create `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`: transport/path/advertised endpoint validation.
- Create `src/ULinkGame.Server/Guardrails/Rules/HotfixSourceRule.cs`: hotfix assembly presence validation.
- Create `src/ULinkGame.Server/Guardrails/Rules/ClusterServiceGraphRule.cs`: duplicate service validation.
- Create `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`: DI registration.
- Modify `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`: generated check command calls framework validation model and supports `--json`.
- Modify `Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj`: add configuration/DI package references only if tests require them.
- Create `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`: unit tests for rules.
- Modify `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`: generated check command expectations.
- Modify `src/ULinkGame.Server/ULinkGame.Server.csproj`: bump package version before shipping runtime changes.
- Modify `src/ULinkGame.Tool/ULinkGame.Tool.csproj`: bump package version before shipping template changes.
- Modify `CHANGELOG.md`: note package changes and versions.

## Scope Boundary

This plan implements the first usable guardrails loop only. It does not implement full production-readiness validation, durable Reliable Push policy, or split-node topology validation. Those are later phases once the resolved runtime model and check integration are stable.

The first loop covers:

- diagnostic result types
- resolved runtime model with value provenance
- node id validation
- endpoint transport/path validation
- duplicate cluster service validation
- hotfix assembly presence validation
- `--ulinkgame-check --json`
- generated check command reusing the framework validation result

## Task 1: Add Guardrail Diagnostic Primitives

**Files:**
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameDiagnosticSeverity.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameDiagnostic.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameValidationResult.cs`
- Test: `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`

- [ ] **Step 1: Write failing tests for diagnostic success and failure**

Create `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`:

```csharp
using ULinkGame.Server.Guardrails;
using Xunit;

namespace ULinkGame.Server.Tests.Guardrails;

public sealed class ULinkGameRuntimeValidatorTests
{
    [Fact]
    public void ValidationResult_Succeeds_WhenNoErrorDiagnosticsExist()
    {
        var result = new ULinkGameValidationResult(
            [
                new ULinkGameDiagnostic("ULINK000", ULinkGameDiagnosticSeverity.Info, "ok"),
                new ULinkGameDiagnostic("ULINK050", ULinkGameDiagnosticSeverity.Warning, "local default")
            ]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidationResult_Fails_WhenAnyErrorDiagnosticExists()
    {
        var result = new ULinkGameValidationResult(
            [
                new ULinkGameDiagnostic("ULINK001", ULinkGameDiagnosticSeverity.Error, "Node id is required.")
            ]);

        Assert.False(result.Succeeded);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path .).Path
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_NOLOGO='1'
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: FAIL because `ULinkGame.Server.Guardrails` types do not exist.

- [ ] **Step 3: Add diagnostic primitives**

Create `src/ULinkGame.Server/Guardrails/ULinkGameDiagnosticSeverity.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public enum ULinkGameDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameDiagnostic.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameDiagnostic(
    string Code,
    ULinkGameDiagnosticSeverity Severity,
    string Message,
    string? Repair = null);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameValidationResult.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameValidationResult(
    IReadOnlyList<ULinkGameDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.All(static diagnostic =>
        diagnostic.Severity != ULinkGameDiagnosticSeverity.Error);

    public static ULinkGameValidationResult Success { get; } = new([]);
}
```

- [ ] **Step 4: Run the test and verify it passes**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit diagnostic primitives**

Run:

```powershell
git add src/ULinkGame.Server/Guardrails Tests/ULinkGame.Server.Tests/Guardrails
git commit -m "feat: add runtime guardrail diagnostics"
```

## Task 2: Add Resolved Runtime Model

**Files:**
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeProfile.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameValueSource.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedValue.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedHotfix.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedCluster.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedReliablePush.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`
- Test: `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`

- [ ] **Step 1: Add failing tests for value provenance and default runtime construction**

Append to `ULinkGameRuntimeValidatorTests`:

```csharp
[Fact]
public void ResolvedValue_PreservesValueSourceAndPath()
{
    var value = new ULinkGameResolvedValue<string>(
        "dev-1",
        ULinkGameValueSource.Configuration,
        "ULinkGame:Node:Id");

    Assert.Equal("dev-1", value.Value);
    Assert.Equal(ULinkGameValueSource.Configuration, value.Source);
    Assert.Equal("ULinkGame:Node:Id", value.Path);
}

[Fact]
public void ResolvedRuntime_CarriesCoreRuntimeSections()
{
    var runtime = TestRuntime();

    Assert.Equal("dev-1", runtime.NodeId.Value);
    Assert.Equal("kcp", runtime.Endpoint.Transport.Value);
    Assert.Equal("Server.Hotfix.dll", runtime.Hotfix.AssemblyFileName.Value);
    Assert.Equal(ULinkGameRuntimeProfile.Development, runtime.Profile);
}

private static ULinkGameResolvedRuntime TestRuntime()
{
    return new ULinkGameResolvedRuntime(
        NodeId: new ULinkGameResolvedValue<string>("dev-1", ULinkGameValueSource.Configuration, "ULinkGame:Node:Id"),
        Endpoint: new ULinkGameResolvedEndpoint(
            Transport: new ULinkGameResolvedValue<string>("kcp", ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Transport"),
            Host: new ULinkGameResolvedValue<string>("127.0.0.1", ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Host"),
            Port: new ULinkGameResolvedValue<int>(20000, ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Port"),
            Path: new ULinkGameResolvedValue<string>("", ULinkGameValueSource.Default),
            AdvertisedEndpoint: new ULinkGameResolvedValue<string>("kcp://127.0.0.1:20000", ULinkGameValueSource.GeneratedConvention)),
        Cluster: new ULinkGameResolvedCluster(
            Services: [new ULinkGameResolvedClusterService("gateway", "gateway")],
            AdvertisedEndpoints: new Dictionary<string, string> { ["client"] = "kcp://127.0.0.1:20000" }),
        Hotfix: new ULinkGameResolvedHotfix(
            AssemblyPath: new ULinkGameResolvedValue<string>("Server.Hotfix.dll", ULinkGameValueSource.GeneratedConvention),
            AssemblyFileName: new ULinkGameResolvedValue<string>("Server.Hotfix.dll", ULinkGameValueSource.GeneratedConvention)),
        ReliablePush: new ULinkGameResolvedReliablePush(
            StorageMode: new ULinkGameResolvedValue<string>("InMemory", ULinkGameValueSource.Default),
            PendingLimit: new ULinkGameResolvedValue<int>(256, ULinkGameValueSource.Default),
            ReplayWindowSeconds: new ULinkGameResolvedValue<int>(120, ULinkGameValueSource.Default),
            HasSessionIdentityResolver: true),
        Profile: ULinkGameRuntimeProfile.Development);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: FAIL because resolved runtime model types do not exist.

- [ ] **Step 3: Add resolved runtime model types**

Create `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeProfile.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public enum ULinkGameRuntimeProfile
{
    Development,
    Compose,
    Production
}
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameValueSource.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public enum ULinkGameValueSource
{
    Default,
    Configuration,
    Environment,
    GeneratedConvention,
    Code
}
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedValue.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedValue<T>(
    T Value,
    ULinkGameValueSource Source,
    string? Path = null);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedEndpoint.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedEndpoint(
    ULinkGameResolvedValue<string> Transport,
    ULinkGameResolvedValue<string> Host,
    ULinkGameResolvedValue<int> Port,
    ULinkGameResolvedValue<string> Path,
    ULinkGameResolvedValue<string> AdvertisedEndpoint);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedHotfix.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedHotfix(
    ULinkGameResolvedValue<string> AssemblyPath,
    ULinkGameResolvedValue<string> AssemblyFileName);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedCluster.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedCluster(
    IReadOnlyList<ULinkGameResolvedClusterService> Services,
    IReadOnlyDictionary<string, string> AdvertisedEndpoints);

public sealed record ULinkGameResolvedClusterService(
    string Kind,
    string Name);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedReliablePush.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedReliablePush(
    ULinkGameResolvedValue<string> StorageMode,
    ULinkGameResolvedValue<int> PendingLimit,
    ULinkGameResolvedValue<int> ReplayWindowSeconds,
    bool HasSessionIdentityResolver);
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameResolvedRuntime.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedRuntime(
    ULinkGameResolvedValue<string> NodeId,
    ULinkGameResolvedEndpoint Endpoint,
    ULinkGameResolvedCluster Cluster,
    ULinkGameResolvedHotfix Hotfix,
    ULinkGameResolvedReliablePush ReliablePush,
    ULinkGameRuntimeProfile Profile);
```

- [ ] **Step 4: Run the test and verify it passes**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit resolved runtime model**

Run:

```powershell
git add src/ULinkGame.Server/Guardrails Tests/ULinkGame.Server.Tests/Guardrails
git commit -m "feat: add resolved ULinkGame runtime model"
```

## Task 3: Add First Validation Rules

**Files:**
- Create: `src/ULinkGame.Server/Guardrails/IULinkGameValidationRule.cs`
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeValidator.cs`
- Create: `src/ULinkGame.Server/Guardrails/Rules/NodeIdentityRule.cs`
- Create: `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`
- Create: `src/ULinkGame.Server/Guardrails/Rules/HotfixSourceRule.cs`
- Create: `src/ULinkGame.Server/Guardrails/Rules/ClusterServiceGraphRule.cs`
- Test: `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`

- [ ] **Step 1: Add failing tests for validation rules**

Append to `ULinkGameRuntimeValidatorTests`:

```csharp
[Fact]
public void RuntimeValidator_Fails_WhenNodeIdIsMissing()
{
    var runtime = TestRuntime() with
    {
        NodeId = new ULinkGameResolvedValue<string>("", ULinkGameValueSource.Configuration, "ULinkGame:Node:Id")
    };
    var result = Validate(runtime);

    Assert.False(result.Succeeded);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ULINK001");
}

[Fact]
public void RuntimeValidator_Fails_WhenWebSocketPathIsMissing()
{
    var runtime = TestRuntime() with
    {
        Endpoint = TestRuntime().Endpoint with
        {
            Transport = new ULinkGameResolvedValue<string>("websocket", ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Transport"),
            Path = new ULinkGameResolvedValue<string>("", ULinkGameValueSource.Default)
        }
    };
    var result = Validate(runtime);

    Assert.False(result.Succeeded);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ULINK023");
}

[Fact]
public void RuntimeValidator_Fails_WhenHotfixAssemblyIsMissing()
{
    var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Server.Hotfix.dll");
    var runtime = TestRuntime() with
    {
        Hotfix = TestRuntime().Hotfix with
        {
            AssemblyPath = new ULinkGameResolvedValue<string>(missingPath, ULinkGameValueSource.GeneratedConvention)
        }
    };
    var result = Validate(runtime);

    Assert.False(result.Succeeded);
    var diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == "ULINK071"));
    Assert.Equal(ULinkGameDiagnosticSeverity.Error, diagnostic.Severity);
    Assert.Equal("dotnet build Server/Hotfix/Server.Hotfix.csproj", diagnostic.Repair);
}

[Fact]
public void RuntimeValidator_Fails_WhenClusterServiceNameIsDuplicated()
{
    var runtime = TestRuntime() with
    {
        Cluster = TestRuntime().Cluster with
        {
            Services =
            [
                new ULinkGameResolvedClusterService("gateway", "gateway"),
                new ULinkGameResolvedClusterService("gateway", "gateway")
            ]
        }
    };
    var result = Validate(runtime);

    Assert.False(result.Succeeded);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ULINK041");
}

private static ULinkGameValidationResult Validate(ULinkGameResolvedRuntime runtime)
{
    var validator = new ULinkGameRuntimeValidator(
        [
            new NodeIdentityRule(),
            new EndpointRule(),
            new HotfixSourceRule(),
            new ClusterServiceGraphRule()
        ]);

    return validator.Validate(runtime);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: FAIL because validation rules do not exist.

- [ ] **Step 3: Add validation rule interface and aggregator**

Create `src/ULinkGame.Server/Guardrails/IULinkGameValidationRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public interface IULinkGameValidationRule
{
    IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime);
}
```

Create `src/ULinkGame.Server/Guardrails/ULinkGameRuntimeValidator.cs`:

```csharp
namespace ULinkGame.Server.Guardrails;

public sealed class ULinkGameRuntimeValidator
{
    private readonly IReadOnlyList<IULinkGameValidationRule> _rules;

    public ULinkGameRuntimeValidator(IEnumerable<IULinkGameValidationRule> rules)
    {
        _rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    }

    public ULinkGameValidationResult Validate(ULinkGameResolvedRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var diagnostics = new List<ULinkGameDiagnostic>();
        foreach (var rule in _rules)
        {
            diagnostics.AddRange(rule.Validate(runtime));
        }

        return new ULinkGameValidationResult(diagnostics);
    }
}
```

- [ ] **Step 4: Add node and endpoint rules**

Create `src/ULinkGame.Server/Guardrails/Rules/NodeIdentityRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class NodeIdentityRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime.NodeId.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK001",
                ULinkGameDiagnosticSeverity.Error,
                "Node id is required.",
                "Set ULinkGame:Node:Id to a stable node id.");
        }
    }
}
```

Create `src/ULinkGame.Server/Guardrails/Rules/EndpointRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class EndpointRule : IULinkGameValidationRule
{
    private static readonly HashSet<string> KnownTransports = new(StringComparer.OrdinalIgnoreCase)
    {
        "kcp",
        "tcp",
        "websocket"
    };

    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        var transport = runtime.Endpoint.Transport.Value;
        if (!KnownTransports.Contains(transport))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK020",
                ULinkGameDiagnosticSeverity.Error,
                $"Endpoint transport '{transport}' is unknown.",
                "Use kcp, tcp, or websocket.");
            yield break;
        }

        if (string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(runtime.Endpoint.Path.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK023",
                ULinkGameDiagnosticSeverity.Error,
                "WebSocket endpoint path is required.",
                "Set ULinkGame:Endpoint:Path to /ws or another explicit WebSocket path.");
        }
    }
}
```

- [ ] **Step 5: Add hotfix and cluster rules**

Create `src/ULinkGame.Server/Guardrails/Rules/HotfixSourceRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class HotfixSourceRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (!File.Exists(runtime.Hotfix.AssemblyPath.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK071",
                ULinkGameDiagnosticSeverity.Error,
                "Hotfix assembly was not found.",
                "dotnet build Server/Hotfix/Server.Hotfix.csproj");
        }
    }
}
```

Create `src/ULinkGame.Server/Guardrails/Rules/ClusterServiceGraphRule.cs`:

```csharp
namespace ULinkGame.Server.Guardrails.Rules;

public sealed class ClusterServiceGraphRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        var duplicated = runtime.Cluster.Services
            .GroupBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            yield return new ULinkGameDiagnostic(
                "ULINK041",
                ULinkGameDiagnosticSeverity.Error,
                $"Cluster service name '{duplicated.Key}' is duplicated.",
                "Use unique service names in the resolved cluster service list.");
        }
    }
}
```

- [ ] **Step 6: Fix test usings**

Add to the top of `ULinkGameRuntimeValidatorTests.cs`:

```csharp
using ULinkGame.Server.Guardrails.Rules;
```

- [ ] **Step 7: Run the tests and verify they pass**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter ULinkGameRuntimeValidatorTests --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit validation rules**

Run:

```powershell
git add src/ULinkGame.Server/Guardrails Tests/ULinkGame.Server.Tests/Guardrails
git commit -m "feat: add initial runtime guardrail rules"
```

## Task 4: Add DI Registration For Guardrails

**Files:**
- Create: `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`
- Test: `Tests/ULinkGame.Server.Tests/Guardrails/ULinkGameRuntimeValidatorTests.cs`

- [ ] **Step 1: Add failing DI registration test**

Append to `ULinkGameRuntimeValidatorTests.cs`:

```csharp
[Fact]
public void AddULinkGameRuntimeValidation_RegistersDefaultValidator()
{
    var services = new ServiceCollection();

    services.AddULinkGameRuntimeValidation();

    using var provider = services.BuildServiceProvider();
    var validator = provider.GetRequiredService<ULinkGameRuntimeValidator>();

    Assert.NotNull(validator);
}
```

Add required usings:

```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter AddULinkGameRuntimeValidation --no-restore
```

Expected: FAIL because `AddULinkGameRuntimeValidation` does not exist.

- [ ] **Step 3: Add package reference for DI test helpers**

Add this package to `Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj` so `ServiceCollection` and `BuildServiceProvider()` are directly available to the tests:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

- [ ] **Step 4: Add DI extension**

Create `src/ULinkGame.Server/Guardrails/ULinkGameGuardrailServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ULinkGame.Server.Guardrails.Rules;

namespace ULinkGame.Server.Guardrails;

public static class ULinkGameGuardrailServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameRuntimeValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, NodeIdentityRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, EndpointRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, HotfixSourceRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, ClusterServiceGraphRule>());
        services.TryAddSingleton<ULinkGameRuntimeValidator>();

        return services;
    }
}
```

- [ ] **Step 5: Run the test and verify it passes**

Run:

```powershell
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --filter AddULinkGameRuntimeValidation --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit DI registration**

Run:

```powershell
git add src/ULinkGame.Server/Guardrails Tests/ULinkGame.Server.Tests/Guardrails Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj
git commit -m "feat: register runtime guardrail validation"
```

## Task 5: Update Generated Check Command To Use Validation Result

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Test: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`

- [ ] **Step 1: Add failing template tests for framework guardrail usage and JSON output**

Update `RenderClusterOptions_IncludesULinkGameCheckOutputLabels` in `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs` to include:

```csharp
Assert.Contains("using ULinkGame.Server.Guardrails;", source);
Assert.Contains("ULinkGameValidationResult", source);
Assert.Contains("--json", source);
Assert.Contains("JsonSerializer.Serialize", source);
Assert.Contains("\"succeeded\"", source);
Assert.Contains("ULINK071", source);
```

- [ ] **Step 2: Run the template test and verify it fails**

Run:

```powershell
dotnet test Tests\ULinkGame.Tool.Tests\ULinkGame.Tool.Tests.csproj --filter RenderClusterOptions_IncludesULinkGameCheckOutputLabels --no-restore
```

Expected: FAIL because the generated check command does not yet reference the framework guardrail result or JSON output.

- [ ] **Step 3: Update generated cluster options usings**

In `ToolTemplates.RenderClusterOptions()`, add generated usings:

```csharp
using System.Text.Json;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Guardrails.Rules;
```

- [ ] **Step 4: Update generated check command helper**

Replace generated `ULinkGameCheck.Run(...)` with a version that builds a resolved runtime model, invokes the framework rules, and formats text or JSON:

```csharp
internal static class ULinkGameCheck
{
    public static int Run(ULinkGameRuntimeOptions runtime, ClusterOptions clusterOptions, string[] args)
    {
        var resolved = ToResolvedRuntime(runtime, clusterOptions);
        var validator = new ULinkGameRuntimeValidator(
            [
                new NodeIdentityRule(),
                new EndpointRule(),
                new HotfixSourceRule(),
                new ClusterServiceGraphRule()
            ]);
        var result = validator.Validate(resolved);

        if (args.Contains("--json", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new
                {
                    succeeded = result.Succeeded,
                    diagnostics = result.Diagnostics.Select(diagnostic => new
                    {
                        code = diagnostic.Code,
                        severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                        message = diagnostic.Message,
                        repair = diagnostic.Repair
                    })
                },
                new JsonSerializerOptions { WriteIndented = true }));
            return result.Succeeded ? 0 : 1;
        }

        return WriteText(runtime, clusterOptions, result);
    }

    private static int WriteText(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions clusterOptions,
        ULinkGameValidationResult result)
    {
        var serviceNames = clusterOptions.Services.Select(service => service.Name);
        var rpcEndpoint = clusterOptions.AdvertisedEndpoints.TryGetValue("client", out var clientEndpoint)
            ? clientEndpoint
            : runtime.Endpoint.ToAdvertisedEndpoint();

        Console.WriteLine("cluster: ok single-node");
        Console.WriteLine($"node: ok {clusterOptions.NodeId}");
        Console.WriteLine($"services: ok {string.Join(", ", serviceNames)}");

        var hotfixFailure = result.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == "ULINK071");
        if (hotfixFailure is not null)
        {
            Console.Error.WriteLine("hotfix: failed local build output not found");
            Console.Error.WriteLine($"fix: {hotfixFailure.Repair}");
            return 1;
        }

        Console.WriteLine("hotfix: ok local-build Server.Hotfix.dll");
        Console.WriteLine("reliable-push: ok pending limit 256, replay window 120s");
        Console.WriteLine($"rpc: ok {rpcEndpoint}");

        foreach (var diagnostic in result.Diagnostics.Where(diagnostic => diagnostic.Severity == ULinkGameDiagnosticSeverity.Error))
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            if (!string.IsNullOrWhiteSpace(diagnostic.Repair))
            {
                Console.Error.WriteLine($"fix: {diagnostic.Repair}");
            }
        }

        return result.Succeeded ? 0 : 1;
    }

    private static ULinkGameResolvedRuntime ToResolvedRuntime(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions clusterOptions)
    {
        var hotfixPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "../../../../Hotfix/bin/Debug/net10.0",
                "Server.Hotfix.dll"));

        return new ULinkGameResolvedRuntime(
            NodeId: new ULinkGameResolvedValue<string>(clusterOptions.NodeId, ULinkGameValueSource.Configuration, "ULinkGame:Node:Id"),
            Endpoint: new ULinkGameResolvedEndpoint(
                Transport: new ULinkGameResolvedValue<string>(runtime.Endpoint.Transport, ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Transport"),
                Host: new ULinkGameResolvedValue<string>(runtime.Endpoint.Host, ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Host"),
                Port: new ULinkGameResolvedValue<int>(runtime.Endpoint.Port, ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Port"),
                Path: new ULinkGameResolvedValue<string>(runtime.Endpoint.Path, ULinkGameValueSource.Configuration, "ULinkGame:Endpoint:Path"),
                AdvertisedEndpoint: new ULinkGameResolvedValue<string>(runtime.Endpoint.ToAdvertisedEndpoint(), ULinkGameValueSource.GeneratedConvention)),
            Cluster: new ULinkGameResolvedCluster(
                Services: clusterOptions.Services
                    .Select(service => new ULinkGameResolvedClusterService(service.Kind, service.Name))
                    .ToArray(),
                AdvertisedEndpoints: clusterOptions.AdvertisedEndpoints),
            Hotfix: new ULinkGameResolvedHotfix(
                AssemblyPath: new ULinkGameResolvedValue<string>(hotfixPath, ULinkGameValueSource.GeneratedConvention),
                AssemblyFileName: new ULinkGameResolvedValue<string>("Server.Hotfix.dll", ULinkGameValueSource.GeneratedConvention)),
            ReliablePush: new ULinkGameResolvedReliablePush(
                StorageMode: new ULinkGameResolvedValue<string>("InMemory", ULinkGameValueSource.Default),
                PendingLimit: new ULinkGameResolvedValue<int>(256, ULinkGameValueSource.Default),
                ReplayWindowSeconds: new ULinkGameResolvedValue<int>(120, ULinkGameValueSource.Default),
                HasSessionIdentityResolver: true),
            Profile: ULinkGameRuntimeProfile.Development);
    }
}
```

- [ ] **Step 5: Update generated Program invocation**

In `RenderULinkGameCheckExit`, change the generated invocation to:

```csharp
return ULinkGameCheck.Run(runtimeOptions, runtimeOptions.ToClusterOptions(builder.Configuration), args);
```

- [ ] **Step 6: Run template tests and fix string expectations**

Run:

```powershell
dotnet test Tests\ULinkGame.Tool.Tests\ULinkGame.Tool.Tests.csproj --filter ToolTemplateTests --no-restore
```

Expected: PASS after updating existing assertions from the old `ULinkGameCheck.Run(runtimeOptions, ...)` signature to include `args`.

- [ ] **Step 7: Commit generated check integration**

Run:

```powershell
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs
git commit -m "feat: use runtime guardrails in generated check"
```

## Task 6: Verify Generated Project End To End

**Files:**
- Modify only if verification exposes template issues.

- [ ] **Step 1: Build tool and server tests**

Run:

```powershell
dotnet build src\ULinkGame.Server\ULinkGame.Server.csproj --no-restore
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --no-restore
dotnet build src\ULinkGame.Tool\ULinkGame.Tool.csproj --no-restore
dotnet test Tests\ULinkGame.Tool.Tests\ULinkGame.Tool.Tests.csproj --no-restore
```

Expected: all commands pass.

- [ ] **Step 2: Generate a fresh verification project**

Run:

```powershell
dotnet run --project src\ULinkGame.Tool\ULinkGame.Tool.csproj -- new --name VerifyGuardrails --output VerifyOut
```

Expected: `VerifyOut\VerifyGuardrails` is created.

- [ ] **Step 3: Run human-readable check**

Run:

```powershell
dotnet run --project VerifyOut\VerifyGuardrails\Server\Server\Server.csproj -- --ulinkgame-check
```

Expected output includes:

```txt
cluster: ok single-node
node: ok dev-1
services: ok node-directory, route-directory, gateway
hotfix: ok local-build Server.Hotfix.dll
reliable-push: ok pending limit 256, replay window 120s
rpc: ok kcp://127.0.0.1:20000
```

- [ ] **Step 4: Run JSON check**

Run:

```powershell
dotnet run --project VerifyOut\VerifyGuardrails\Server\Server\Server.csproj -- --ulinkgame-check --json
```

Expected output includes:

```json
{
  "succeeded": true,
  "diagnostics": []
}
```

- [ ] **Step 5: Verify missing Hotfix remains an error**

Move the generated hotfix DLL aside, run the already-built server DLL, and restore the DLL:

```powershell
$dll = Resolve-Path 'VerifyOut\VerifyGuardrails\Server\Hotfix\bin\Debug\net10.0\Server.Hotfix.dll'
$bak = "$dll.bak"
Move-Item -LiteralPath $dll.Path -Destination $bak
try {
    dotnet "VerifyOut\VerifyGuardrails\Server\Server\bin\Debug\net10.0\Server.dll" --ulinkgame-check
    $code = $LASTEXITCODE
} finally {
    Move-Item -LiteralPath $bak -Destination $dll.Path
}
exit $code
```

Expected: exit code `1`, output includes:

```txt
hotfix: failed local build output not found
fix: dotnet build Server/Hotfix/Server.Hotfix.csproj
```

- [ ] **Step 6: Clean verification output**

Run:

```powershell
$target = Resolve-Path 'VerifyOut'
if ($target.Path -eq (Join-Path (Resolve-Path '.').Path 'VerifyOut')) {
    Remove-Item -LiteralPath $target.Path -Recurse -Force
} else {
    throw "Refusing to remove unexpected path $($target.Path)"
}
```

Expected: `VerifyOut` is removed.

- [ ] **Step 7: Commit any fixes from verification**

If verification required code or template changes, commit them:

```powershell
git add src Tests CHANGELOG.md
git commit -m "fix: make runtime guardrails pass generated checks"
```

If no fixes were required, do not create an empty commit.

## Task 7: Version And Documentation Updates

**Files:**
- Modify: `src/ULinkGame.Server/ULinkGame.Server.csproj`
- Modify: `src/ULinkGame.Tool/ULinkGame.Tool.csproj`
- Modify: `CHANGELOG.md`
- Modify: `src/ULinkGame.Server/README.md`
- Modify: `src/ULinkGame.Tool/README.md`

- [ ] **Step 1: Bump package versions**

Update `src/ULinkGame.Server/ULinkGame.Server.csproj`:

```xml
<Version>0.1.10</Version>
```

Update `src/ULinkGame.Tool/ULinkGame.Tool.csproj` to the next patch version from its current value. Preserve the existing major/minor version.

- [ ] **Step 2: Update changelog**

Add entries to `CHANGELOG.md`:

```markdown
## Unreleased

- ULinkGame.Server: Added runtime guardrail diagnostics, resolved runtime model, and initial validation rules for node id, endpoints, hotfix presence, and duplicate cluster services.
- ULinkGame.Tool: Updated generated `--ulinkgame-check` to reuse runtime guardrail diagnostics and support `--json` output.
```

If `CHANGELOG.md` already has an `Unreleased` section, append these bullets there instead of creating a duplicate section.

- [ ] **Step 3: Update package READMEs**

In `src/ULinkGame.Server/README.md`, add a short section:

```markdown
## Runtime Guardrails

ULinkGame.Server provides runtime guardrail diagnostics for framework invariants such as node identity, endpoint shape, hotfix assembly presence, and cluster service graph consistency. Generated projects use these diagnostics through `--ulinkgame-check`; server hosts can also register the default rules with `AddULinkGameRuntimeValidation()`.
```

In `src/ULinkGame.Tool/README.md`, mention:

```markdown
The generated `--ulinkgame-check --json` output is suitable for CI and deployment scripts that need machine-readable validation results.
```

- [ ] **Step 4: Run relevant verification**

Run:

```powershell
dotnet build src\ULinkGame.Server\ULinkGame.Server.csproj --no-restore
dotnet test Tests\ULinkGame.Server.Tests\ULinkGame.Server.Tests.csproj --no-restore
dotnet build src\ULinkGame.Tool\ULinkGame.Tool.csproj --no-restore
dotnet test Tests\ULinkGame.Tool.Tests\ULinkGame.Tool.Tests.csproj --no-restore
```

Expected: all commands pass.

- [ ] **Step 5: Commit version and docs**

Run:

```powershell
git add src\ULinkGame.Server\ULinkGame.Server.csproj src\ULinkGame.Tool\ULinkGame.Tool.csproj CHANGELOG.md src\ULinkGame.Server\README.md src\ULinkGame.Tool\README.md
git commit -m "docs: document runtime guardrails release"
```

## Self-Review

Spec coverage:

- Diagnostic result types are covered by Task 1.
- Resolved runtime model with provenance is covered by Task 2.
- Small deterministic rules are covered by Task 3.
- DI registration is covered by Task 4.
- Generated check command reuse and `--json` output are covered by Task 5.
- End-to-end generated project verification is covered by Task 6.
- Version and package docs are covered by Task 7.

Deferred work:

- Production-readiness profile rules beyond the first local checks.
- Durable Reliable Push policy.
- Split-node route-directory and node-directory dependency validation.
- Moving all current generated runtime option derivation into framework defaults.

Placeholder scan:

- This plan has no TBD or placeholder steps.
- Every code-writing step includes concrete code or concrete assertions.

Type consistency:

- `ULinkGameResolvedRuntime` is introduced before rules consume it.
- `IULinkGameValidationRule` is introduced before DI registration.
- `ULinkGameRuntimeValidator` is used consistently by tests and generated check code.
