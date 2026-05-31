# ULinkGame.Tool Default Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ulinkgame-tool new` generate a smaller, safer default configuration while keeping Cluster, Hotfix, and Reliable Push as always-present ULinkGame defaults.

**Architecture:** Generated projects will read a compact `ULinkGame` configuration section and derive full runtime settings in generated server helper code. The appsettings file remains focused on node id and endpoint binding, while a generated `--ulinkgame-check` command explains the derived cluster, hotfix, reliable push, and RPC state.

**Tech Stack:** C#/.NET 10, ULinkGame.Tool template rendering, Microsoft.Extensions.Configuration, xUnit tests under `Tests/ULinkGame.Tool.Tests`.

---

## File Structure

- Modify `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`: render the smaller `appsettings.json`, generated runtime option helpers, and check command helpers.
- Modify `src/ULinkGame.Tool/Scaffolding/ToolModels.cs`: remove user-facing dependence on `NetworkProfile` in generated config decisions when possible, while preserving existing CLI compatibility.
- Modify `src/ULinkGame.Tool/Cli/ToolText.cs`: update generated next-step text to point at `--ulinkgame-check`.
- Modify `src/ULinkGame.Tool/README.md`: document the smaller generated configuration and default capabilities.
- Modify `Tests/ULinkGame.Tool.Tests/*.cs`: add tests for rendered appsettings and CLI output text.

### Task 1: Render Minimal Appsettings

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Test: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`

- [ ] **Step 1: Add failing tests for generated appsettings**

Create `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs` if it does not exist. Add tests that call `ToolTemplates.RenderServerAppSettings(...)` and assert that the default output contains only the compact `ULinkGame` section and does not contain old derived sections.

```csharp
using Xunit;

public sealed class ToolTemplateTests
{
    [Fact]
    public void RenderServerAppSettings_DefaultClusterProject_UsesCompactULinkGameSection()
    {
        var options = new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: ProjectConventions.DefaultClientEngine,
            Transport: "kcp",
            NetworkProfile: ProjectConventions.DefaultNetworkProfile,
            Serializer: ProjectConventions.DefaultSerializer,
            Persistence: ProjectConventions.DefaultPersistence,
            NuGetForUnitySource: ProjectConventions.DefaultNuGetForUnitySource,
            DeployProfile: ProjectConventions.DefaultDeployProfile);

        var json = ToolTemplates.RenderServerAppSettings(options);

        Assert.Contains("\"ULinkGame\"", json);
        Assert.Contains("\"Node\"", json);
        Assert.Contains("\"Id\": \"dev-1\"", json);
        Assert.Contains("\"Endpoint\"", json);
        Assert.Contains("\"Transport\": \"kcp\"", json);
        Assert.Contains("\"Host\": \"127.0.0.1\"", json);
        Assert.Contains("\"Port\": 20000", json);
        Assert.DoesNotContain("\"Cluster\"", json);
        Assert.DoesNotContain("\"Hotfix\"", json);
        Assert.DoesNotContain("\"ReliablePush\"", json);
        Assert.DoesNotContain("\"Bootstrap\"", json);
        Assert.DoesNotContain("\"Services\"", json);
        Assert.DoesNotContain("\"NodeDirectory\"", json);
    }

    [Fact]
    public void RenderServerAppSettings_WebSocketProject_IncludesEndpointPath()
    {
        var options = new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: ProjectConventions.DefaultClientEngine,
            Transport: "websocket",
            NetworkProfile: ProjectConventions.DefaultNetworkProfile,
            Serializer: ProjectConventions.DefaultSerializer,
            Persistence: ProjectConventions.DefaultPersistence,
            NuGetForUnitySource: ProjectConventions.DefaultNuGetForUnitySource,
            DeployProfile: ProjectConventions.DefaultDeployProfile);

        var json = ToolTemplates.RenderServerAppSettings(options);

        Assert.Contains("\"Transport\": \"websocket\"", json);
        Assert.Contains("\"Path\": \"/ws\"", json);
        Assert.DoesNotContain("\"AdvertisedEndpoints\"", json);
    }
}
```

- [ ] **Step 2: Run the targeted tests and verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter ToolTemplateTests
```

Expected: FAIL because `RenderServerAppSettings` still emits `Cluster`, `Hotfix`, and derived cluster fields.

- [ ] **Step 3: Implement compact appsettings rendering**

In `ToolTemplates.RenderServerAppSettings`, replace the branch-heavy generated JSON with compact output. Preserve WebSocket path only when the selected transport is WebSocket.

```csharp
public static string RenderServerAppSettings(NewCommandOptions options)
{
    var pathLine = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase)
        ? """
                "Path": "/ws"
        """
        : string.Empty;

    if (pathLine.Length > 0)
    {
        return $$"""
        {
          "ULinkGame": {
            "Node": {
              "Id": "dev-1"
            },
            "Endpoint": {
              "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
              "Host": "127.0.0.1",
              "Port": 20000,
              "Path": "/ws"
            }
          }
        }
        """;
    }

    return $$"""
    {
      "ULinkGame": {
        "Node": {
          "Id": "dev-1"
        },
        "Endpoint": {
          "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
          "Host": "127.0.0.1",
          "Port": 20000
        }
      }
    }
    """;
}
```

- [ ] **Step 4: Run the targeted tests and verify they pass**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter ToolTemplateTests
```

Expected: PASS.

- [ ] **Step 5: Commit appsettings rendering**

```powershell
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs
git commit -m "feat: simplify generated ULinkGame appsettings"
```

### Task 2: Generate Runtime Options From Compact Config

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Test: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`

- [ ] **Step 1: Add failing tests for generated runtime options**

Add tests that assert generated server templates include a ULinkGame runtime options helper and read from `ULinkGame`, not from old top-level `Endpoint` or `Cluster` sections.

```csharp
[Fact]
public void RenderClusterOptions_ReadsCompactULinkGameConfiguration()
{
    var source = ToolTemplates.RenderClusterOptions();

    Assert.Contains("ULinkGameRuntimeOptions", source);
    Assert.Contains("configuration.GetSection(\"ULinkGame\")", source);
    Assert.Contains("ToClusterOptions()", source);
    Assert.DoesNotContain("configuration.GetSection(\"Cluster\")", source);
}
```

- [ ] **Step 2: Run the targeted tests and verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter RenderClusterOptions
```

Expected: FAIL because the current generated `ClusterOptions` reads the old `Cluster` section directly.

- [ ] **Step 3: Replace generated cluster option helper**

Update `RenderClusterOptions()` so the generated code defines `ULinkGameRuntimeOptions`, `ULinkGameNodeOptions`, `ULinkGameEndpointOptions`, and derived `ClusterOptions`.

The generated helper must:

- read `ULinkGame:Node:Id`
- read `ULinkGame:Endpoint:Transport`
- read `ULinkGame:Endpoint:Host`
- read `ULinkGame:Endpoint:Port`
- read optional `ULinkGame:Endpoint:Path`
- derive `ClusterOptions.Services`
- derive `ClusterOptions.Bootstrap.NodeDirectoryEndpoints`
- derive advertised `cluster` and `client` endpoints

Use these default derivation rules:

```csharp
private const int DefaultClientPort = 20000;
private const int DefaultClusterPort = 21000;
private const int DefaultRouteLeaseSeconds = 30;
private const int DefaultSendTimeoutMilliseconds = 2000;
```

For client endpoint URI:

```csharp
private static string BuildClientEndpoint(ULinkGameEndpointOptions endpoint)
{
    var scheme = endpoint.Transport switch
    {
        "websocket" => "ws",
        "tcp" => "tcp",
        _ => "kcp"
    };

    return string.IsNullOrWhiteSpace(endpoint.Path)
        ? $"{scheme}://{endpoint.Host}:{endpoint.Port}"
        : $"{scheme}://{endpoint.Host}:{endpoint.Port}{endpoint.Path}";
}
```

- [ ] **Step 4: Update Program.cs template to use runtime options**

In `RenderServerProgram`, replace direct `ServerRpcServerOptions.FromConfiguration(builder.Configuration, "Endpoint", ...)` calls with generated runtime options:

```csharp
var runtime = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(runtime);
builder.Services.AddSingleton(runtime.Endpoint.ToServerRpcServerOptions());
builder.Services.AddSingleton(runtime.ToClusterOptions());
```

For realtime profiles retained for compatibility, derive both control and realtime options from the compact section plus known defaults.

- [ ] **Step 5: Run tool tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit runtime option derivation**

```powershell
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs
git commit -m "feat: derive generated runtime options from compact config"
```

### Task 3: Add ULinkGame Check Command

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`
- Test: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`

- [ ] **Step 1: Add failing tests for check command generation**

Add tests that assert generated `Program.cs` contains `--ulinkgame-check` and generated check output includes cluster, hotfix, reliable push, and rpc labels.

```csharp
[Fact]
public void RenderServerProgram_IncludesULinkGameCheckCommand()
{
    var options = new NewCommandOptions(
        Name: "MyGame",
        OutputPath: null,
        ClientEngine: ProjectConventions.DefaultClientEngine,
        Transport: "kcp",
        NetworkProfile: ProjectConventions.DefaultNetworkProfile,
        Serializer: ProjectConventions.DefaultSerializer,
        Persistence: ProjectConventions.DefaultPersistence,
        NuGetForUnitySource: ProjectConventions.DefaultNuGetForUnitySource,
        DeployProfile: ProjectConventions.DefaultDeployProfile);

    var source = ToolTemplates.RenderServerProgram(options);

    Assert.Contains("--ulinkgame-check", source);
    Assert.Contains("ULinkGameCheck", source);
    Assert.Contains("cluster:", source);
    Assert.Contains("hotfix:", source);
    Assert.Contains("reliable-push:", source);
    Assert.Contains("rpc:", source);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter IncludesULinkGameCheckCommand
```

Expected: FAIL because only `--health-check` currently exists for cluster mode.

- [ ] **Step 3: Generate check command branch**

In generated `Program.cs`, add before service registration:

```csharp
var runtime = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration);
if (args.Contains("--ulinkgame-check", StringComparer.Ordinal))
{
    return ULinkGameCheck.Run(runtime);
}
```

Keep existing `--health-check` temporarily if needed for compatibility, but have it delegate to the same check helper or mark it as legacy in generated docs.

- [ ] **Step 4: Generate ULinkGameCheck helper**

Add a generated helper from `ToolTemplates.cs` that prints:

```csharp
Console.WriteLine("cluster: ok single-node");
Console.WriteLine($"node: ok {runtime.Node.Id}");
Console.WriteLine("services: ok node-directory, route-directory, gateway, room");
Console.WriteLine($"hotfix: ok local-build {runtime.HotfixAssemblyName}");
Console.WriteLine("reliable-push: ok pending limit 256, replay window 120s");
Console.WriteLine($"rpc: ok {runtime.Endpoint.BuildClientEndpoint()}");
```

When the local hotfix assembly is missing, print:

```csharp
Console.Error.WriteLine("hotfix: failed local build output not found");
Console.Error.WriteLine("fix: dotnet build Server/Hotfix/Server.Hotfix.csproj");
return 1;
```

- [ ] **Step 5: Run tool tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit check command**

```powershell
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs
git commit -m "feat: add generated ULinkGame check command"
```

### Task 4: Update Tool Completion Text And Docs

**Files:**
- Modify: `src/ULinkGame.Tool/Cli/ToolText.cs`
- Modify: `src/ULinkGame.Tool/README.md`
- Modify: `README.md`
- Test: `Tests/ULinkGame.Tool.Tests/ToolTextTests.cs`

- [ ] **Step 1: Add failing text tests**

Add assertions that generated next steps mention `--ulinkgame-check`.

```csharp
[Fact]
public void NewProjectReadyText_PointsToULinkGameCheck()
{
    var text = ToolText.ForCulture(new System.Globalization.CultureInfo("en-US"));

    Assert.Contains("--ulinkgame-check", text.CheckProjectStep);
}
```

- [ ] **Step 2: Run text tests and verify they fail**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj --filter NewProjectReadyText
```

Expected: FAIL because `CheckProjectStep` does not exist yet.

- [ ] **Step 3: Add localized check step text**

In `ToolText`, add:

```csharp
public string CheckProjectStep => Language switch
{
    ToolLanguage.SimplifiedChinese => "  2) dotnet run --project \"Server/Server/Server.csproj\" -- --ulinkgame-check",
    ToolLanguage.TraditionalChinese => "  2) dotnet run --project \"Server/Server/Server.csproj\" -- --ulinkgame-check",
    _ => "  2) dotnet run --project \"Server/Server/Server.csproj\" -- --ulinkgame-check"
};
```

Adjust `StartServerStep` numbering to step 3 and `RebuildContractsStep` to step 4.

- [ ] **Step 4: Update README docs**

Update `src/ULinkGame.Tool/README.md` to state that generated projects include Cluster, Hotfix, and Reliable Push by default, and that default `appsettings.json` intentionally contains only node and endpoint settings.

Update root `README.md` "Create A Project" next-step text to mention the check command.

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit text and docs**

```powershell
git add src/ULinkGame.Tool/Cli/ToolText.cs src/ULinkGame.Tool/README.md README.md Tests/ULinkGame.Tool.Tests/ToolTextTests.cs
git commit -m "docs: explain generated ULinkGame defaults"
```

### Task 5: Verify Generated Project Build

**Files:**
- Modify only if previous tasks reveal generation issues.
- Test: generated temporary project under a local scratch path.

- [ ] **Step 1: Pack or run the tool project locally**

Run:

```powershell
dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj
```

Expected: PASS.

- [ ] **Step 2: Generate a local test project**

Run:

```powershell
dotnet run --project src/ULinkGame.Tool/ULinkGame.Tool.csproj -- new --name VerifyGame --output _verify
```

Expected: generated project under `_verify/VerifyGame`.

- [ ] **Step 3: Build generated server solution**

Run:

```powershell
dotnet build _verify/VerifyGame/Server/Server.slnx
```

Expected: PASS.

- [ ] **Step 4: Build hotfix project**

Run:

```powershell
dotnet build _verify/VerifyGame/Server/Hotfix/Server.Hotfix.csproj
```

Expected: PASS.

- [ ] **Step 5: Run generated check command**

Run:

```powershell
dotnet run --project _verify/VerifyGame/Server/Server/Server.csproj -- --ulinkgame-check
```

Expected output includes:

```txt
cluster: ok single-node
hotfix: ok local-build Server.Hotfix.dll
reliable-push: ok pending limit 256, replay window 120s
rpc: ok
```

- [ ] **Step 6: Commit any fixes from generated verification**

If generation fixes were needed:

```powershell
git add src/ULinkGame.Tool Tests/ULinkGame.Tool.Tests README.md
git commit -m "fix: make generated ULinkGame project pass checks"
```

If no fixes were needed, do not create an empty commit.

## Self-Review

Spec coverage:

- Compact appsettings is covered by Task 1.
- Derived runtime state is covered by Task 2.
- Check output is covered by Task 3.
- Tool/docs explanation is covered by Task 4.
- End-to-end generated project verification is covered by Task 5.

Placeholder scan:

- This plan intentionally contains no placeholder work items.
- Each task includes concrete file paths, commands, and expected results.

Type consistency:

- `ULinkGameRuntimeOptions`, `ULinkGameNodeOptions`, and `ULinkGameEndpointOptions` are introduced in Task 2 before later tasks reference them.
- `--ulinkgame-check` is introduced in Task 3 before docs and output text reference it in Task 4.
