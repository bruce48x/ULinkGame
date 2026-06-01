internal static class ToolTemplates
{
    public static string RenderServerSolution()
    {
        return """
        <Solution>
          <Project Path="../Shared/Shared.csproj" />
          <Project Path="Hotfix/Server.Hotfix.csproj" />
          <Project Path="Server/Server.csproj" />
        </Solution>
        """;
    }

    public static string RenderServerProgram(NewCommandOptions options)
    {
        if (ProjectConventions.IsRealtimeNetworkProfile(options.NetworkProfile))
        {
            var controlPath = GetDefaultPath(options.Transport, "/ws");
            var realtimePath = GetDefaultPath(options.Transport, "/realtime");

            return $$"""
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using Microsoft.Extensions.Logging;
            using Server.Hosting;
            using ULinkGame.Server;
            using ULinkGame.Server.Hotfix;
            using ULinkGame.Server.Hotfix.Loading;
            using ULinkGame.Server.Hosting;

            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            {{RenderClusterHealthCheckExit(options)}}

            builder.Services.AddULinkGameServer();
            builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
                ServerRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "ControlPlane",
                    new ServerRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20000, Path = "{{TemplateText.SanitizeStringLiteral(controlPath)}}" })));
            builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
                ServerRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "Realtime",
                    new ServerRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20001, Path = "{{TemplateText.SanitizeStringLiteral(realtimePath)}}" })));
            builder.Services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
            builder.Services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
            {{RenderHotfixServiceRegistration()}}
            builder.Services.AddULinkGameServerGateway();

            var host = builder.Build();
            await LoadInitialHotfixAsync(host);
            await host.RunAsync();
            return 0;
            {{RenderHotfixHelpers()}}
            """;
        }

        return $$"""
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Server.Hosting;
        using ULinkGame.Server;
        using ULinkGame.Server.Hotfix;
        using ULinkGame.Server.Hotfix.Loading;
        using ULinkGame.Server.Hosting;

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration);
        {{RenderULinkGameCheckExit(options)}}
        {{RenderClusterHealthCheckExit(options)}}

        builder.Services.AddULinkGameServer();
        builder.Services.AddSingleton(runtimeOptions);
        {{RenderClusterServiceRegistration(options)}}
        builder.Services.AddSingleton(runtimeOptions.ToServerRpcServerOptions());
        builder.Services.AddULinkRpcServer<DefaultRpcServerConfigurator>();
        {{RenderHotfixServiceRegistration()}}
        builder.Services.AddULinkGameServerGateway();

        var host = builder.Build();
        await LoadInitialHotfixAsync(host);
        await host.RunAsync();
        return 0;
        {{RenderHotfixHelpers()}}
        """;
    }

    public static string RenderServerProject(NewCommandOptions options)
    {
        var persistenceReferences = RenderPersistencePackageReferences(options.Persistence, includeDapper: true);
        var clusterReferences = RenderClusterPackageReferences(options);

        return $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <RootNamespace>Server</RootNamespace>
            <BuildInParallel>false</BuildInParallel>
            <RestoreBuildInParallel>false</RestoreBuildInParallel>
            <ULinkRPCGenerateServer>true</ULinkRPCGenerateServer>
            <ULinkRPCServerGeneratedNamespace>Server.Generated</ULinkRPCServerGeneratedNamespace>
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="..\..\Shared\Shared.csproj" TargetFramework="net10.0">
              <SetTargetFramework>TargetFramework=net10.0</SetTargetFramework>
            </ProjectReference>
            <ProjectReference Include="..\Hotfix\Server.Hotfix.csproj" ReferenceOutputAssembly="false" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.Extensions.Hosting" Version="{{ToolPackageVersions.MicrosoftExtensionsHosting}}" />
            <PackageReference Include="ULinkGame.Server" Version="{{ToolPackageVersions.ULinkGameServer}}" />
            <PackageReference Include="ULinkGame.Server.Generators" Version="{{ToolPackageVersions.ULinkGameServerGenerators}}" PrivateAssets="all" OutputItemType="Analyzer" />
            <PackageReference Include="ULinkGame.Server.Hotfix" Version="{{ToolPackageVersions.ULinkGameServerHotfix}}" />
        {{clusterReferences}}
        {{persistenceReferences}}
          </ItemGroup>

          <ItemGroup>
            <None Update="appsettings.json">
              <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            </None>
          </ItemGroup>
        </Project>
        """;
    }

    public static string RenderServerAppSettings(NewCommandOptions options)
    {
        var pathLine = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase)
            ? "," + Environment.NewLine + "          \"Path\": \"/ws\""
            : string.Empty;

        return $$"""
        {
          "ULinkGame": {
            "Node": {
              "Id": "dev-1"
            },
            "Endpoint": {
              "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
              "Host": "127.0.0.1",
              "Port": 20000{{pathLine}}
            }
          }
        }
        """;
    }

    public static string RenderHotfixProject()
    {
        return $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <AssemblyName>Server.Hotfix</AssemblyName>
            <RootNamespace>Server.Hotfix</RootNamespace>
            <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="..\..\Shared\Shared.csproj" TargetFramework="net10.0">
              <SetTargetFramework>TargetFramework=net10.0</SetTargetFramework>
            </ProjectReference>
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Server.Hotfix.Abstractions" Version="{{ToolPackageVersions.ULinkGameServerHotfixAbstractions}}" />
          </ItemGroup>
        </Project>
        """;
    }

    public static string RenderSharedProjectHotfixItemGroup()
    {
        return $$"""
        <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
          <PackageReference Include="ULinkGame.Server.Hotfix.Abstractions" Version="{{ToolPackageVersions.ULinkGameServerHotfixAbstractions}}" />
          <PackageReference Include="ULinkGame.Server.Hotfix" Version="{{ToolPackageVersions.ULinkGameServerHotfix}}" />
          <PackageReference Include="ULinkGame.Server.Hotfix.Generators" Version="{{ToolPackageVersions.ULinkGameServerHotfixGenerators}}" PrivateAssets="all" />
        </ItemGroup>
        """;
    }

    public static string RenderSharedHotfixAssemblyInfo()
    {
        return """
        using System.Runtime.CompilerServices;

        [assembly: InternalsVisibleTo("Server.Hotfix")]
        """;
    }

    public static string RenderSharedGameRules()
    {
        return """
        #nullable enable

        #if NET10_0_OR_GREATER
        using ULinkGame.Server.Hotfix.Abstractions;
        using ULinkGame.Server.Hotfix.Dispatch;
        #endif

        namespace Shared.Gameplay
        {

            public sealed class GameRuleInput
            {
                public string PlayerId { get; set; } = string.Empty;
                public int Score { get; set; }
            }

            public sealed class GameRuleResult
            {
                public bool Accepted { get; set; }
                public string Reason { get; set; } = string.Empty;
            }

        #if NET10_0_OR_GREATER
            [HotfixState]
        #endif
            public sealed partial class GameRulesState
            {
                private int _minimumScore = 1;

                public GameRuleResult Evaluate(GameRuleInput input)
                {
        #if NET10_0_OR_GREATER
                    return HotfixDispatch.Invoke<GameRulesState, GameRuleInput, GameRuleResult>(
                        nameof(Evaluate),
                        this,
                        input);
        #else
                    return EvaluateStable(input);
        #endif
                }

                internal GameRuleResult EvaluateStable(GameRuleInput input)
                {
                    if (string.IsNullOrWhiteSpace(input.PlayerId))
                    {
                        return new GameRuleResult
                        {
                            Accepted = false,
                            Reason = "Player id is required."
                        };
                    }

                    if (input.Score < _minimumScore)
                    {
                        return new GameRuleResult
                        {
                            Accepted = false,
                            Reason = "Score is below the current rule threshold."
                        };
                    }

                    return new GameRuleResult
                    {
                        Accepted = true,
                        Reason = "Accepted by stable rules."
                    };
                }
            }
        }
        """;
    }

    public static string RenderHotfixGameRulesSystem()
    {
        return """
        using Shared.Gameplay;
        using ULinkGame.Server.Hotfix.Abstractions;

        namespace Server.Hotfix.Gameplay;

        [FriendOf(typeof(GameRulesState))]
        [HotfixSystemOf(typeof(GameRulesState))]
        public static class GameRulesSystem
        {
            public static GameRuleResult Evaluate(this GameRulesState self, GameRuleInput input)
            {
                return self.EvaluateStable(input);
            }
        }
        """;
    }

    public static string RenderUnityNuGetPackageImportGuard()
    {
        return """
        #if UNITY_EDITOR
        using System;
        using UnityEditor;

        internal sealed class ULinkGameNuGetPackageImportGuard : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                foreach (var assetPath in importedAssets)
                {
                    DisableAnalyzerPlugin(assetPath);
                }

                foreach (var assetPath in movedAssets)
                {
                    DisableAnalyzerPlugin(assetPath);
                }
            }

            private static void DisableAnalyzerPlugin(string assetPath)
            {
                var normalizedPath = assetPath.Replace('\\', '/');
                if (!normalizedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.IndexOf("Assets/Packages/", StringComparison.OrdinalIgnoreCase) < 0 ||
                    normalizedPath.IndexOf("/analyzers/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
                if (importer == null)
                {
                    return;
                }

                if (!importer.GetCompatibleWithAnyPlatform() && !importer.GetCompatibleWithEditor())
                {
                    return;
                }

                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SaveAndReimport();
            }
        }
        #endif
        """;
    }

    public static string RenderServerRpcServerOptions()
    {
        return @"using Microsoft.Extensions.Configuration;

namespace Server.Hosting;

internal sealed class ServerRpcServerOptions
{
    public string Transport { get; init; } = ""websocket"";
    public string Host { get; init; } = ""127.0.0.1"";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = """";

    public static ServerRpcServerOptions FromConfiguration(
        IConfiguration configuration,
        string sectionName,
        ServerRpcServerOptions defaults)
    {
        var section = configuration.GetSection(sectionName);
        var transport = NormalizeTransport(section[""Transport""], defaults.Transport);
        var host = section[""Host""];
        var path = section[""Path""];

        return new ServerRpcServerOptions
        {
            Transport = transport,
            Host = string.IsNullOrWhiteSpace(host) ? defaults.Host : host,
            Port = ParsePort(section[""Port""], defaults.Port),
            Path = string.IsNullOrWhiteSpace(path) ? defaults.Path : path
        };
    }

    private static string NormalizeTransport(string? rawValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? fallback
            : rawValue.Trim().ToLowerInvariant();
    }

    private static int ParsePort(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var port) && port > 0
            ? port
            : fallback;
    }
}";
    }

    public static string RenderNamedRpcServerOptions(string typeName)
    {
        return $@"namespace Server.Hosting;

internal sealed class {typeName}
{{
    public {typeName}(ServerRpcServerOptions endpoint)
    {{
        Endpoint = endpoint;
    }}

    public ServerRpcServerOptions Endpoint {{ get; }}
}}";
    }

    public static string RenderClusterOptions()
    {
        return @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Guardrails.Rules;

namespace Server.Hosting;

internal sealed class ULinkGameRuntimeOptions
{
    private const string NodeIdConfigurationKey = ""ULinkGame:Node:Id"";
    private const string EndpointTransportConfigurationKey = ""ULinkGame:Endpoint:Transport"";
    private const string EndpointHostConfigurationKey = ""ULinkGame:Endpoint:Host"";
    private const string EndpointPortConfigurationKey = ""ULinkGame:Endpoint:Port"";
    private const string EndpointPathConfigurationKey = ""ULinkGame:Endpoint:Path"";

    public ULinkGameNodeOptions Node { get; init; } = new();
    public ULinkGameEndpointOptions Endpoint { get; init; } = new();
    public string ClusterEndpoint { get; init; } = ""tcp://127.0.0.1:21000"";
    public string AdvertisedClientEndpoint => Endpoint.ToAdvertisedEndpoint();

    public static ULinkGameRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(""ULinkGame"");
        return new ULinkGameRuntimeOptions
        {
            Node = ULinkGameNodeOptions.FromConfiguration(section.GetSection(""Node"")),
            Endpoint = ULinkGameEndpointOptions.FromConfiguration(section.GetSection(""Endpoint""))
        };
    }

    public ServerRpcServerOptions ToServerRpcServerOptions()
    {
        return new ServerRpcServerOptions
        {
            Transport = Endpoint.Transport,
            Host = Endpoint.Host,
            Port = Endpoint.Port,
            Path = Endpoint.Path
        };
    }

    public ClusterOptions ToClusterOptions()
    {
        return new ClusterOptions
        {
            NodeId = Node.Id,
            AdvertisedEndpoints = new Dictionary<string, string>
            {
                [""cluster""] = ClusterEndpoint,
                [""client""] = AdvertisedClientEndpoint
            },
            Bootstrap = new ClusterBootstrapOptions
            {
                NodeDirectoryEndpoints = new[] { ClusterEndpoint }
            },
            Services = new[]
            {
                new ClusterServiceOptions { Kind = ""node-directory"", Name = ""node-directory"" },
                new ClusterServiceOptions { Kind = ""route-directory"", Name = ""route-directory"" },
                new ClusterServiceOptions { Kind = ""gateway"", Name = ""gateway"" }
            }
        };
    }

    public ClusterOptions ToClusterOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(""Cluster"");
        var defaults = ToClusterOptions();
        return new ClusterOptions
        {
            NodeId = ReadString(section, ""NodeId"", defaults.NodeId),
            AdvertisedEndpoints = ReadDictionary(section.GetSection(""AdvertisedEndpoints""), defaults.AdvertisedEndpoints),
            Bootstrap = ClusterBootstrapOptions.FromConfiguration(section.GetSection(""Bootstrap""), defaults.Bootstrap),
            NodeDirectory = ClusterNodeDirectoryOptions.FromConfiguration(section.GetSection(""NodeDirectory""), defaults.NodeDirectory),
            Services = ReadServices(section.GetSection(""Services""), defaults.Services),
            RouteLeaseSeconds = ReadInt(section, ""RouteLeaseSeconds"", defaults.RouteLeaseSeconds),
            SendTimeoutMilliseconds = ReadInt(section, ""SendTimeoutMilliseconds"", defaults.SendTimeoutMilliseconds)
        };
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadInt(IConfiguration section, string name, int fallback)
    {
        return int.TryParse(section[name], out var value) && value > 0 ? value : fallback;
    }

    private static IReadOnlyDictionary<string, string> ReadDictionary(
        IConfigurationSection section,
        IReadOnlyDictionary<string, string> fallback)
    {
        var values = new Dictionary<string, string>();
        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Key) &&
                !string.IsNullOrWhiteSpace(child.Value))
            {
                values[child.Key] = child.Value!;
            }
        }

        return values.Count == 0 ? fallback : values;
    }

    private static IReadOnlyList<ClusterServiceOptions> ReadServices(
        IConfigurationSection section,
        IReadOnlyList<ClusterServiceOptions> fallback)
    {
        var values = new List<ClusterServiceOptions>();
        foreach (var child in section.GetChildren())
        {
            var kind = child[""Kind""];
            if (string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            values.Add(new ClusterServiceOptions
            {
                Kind = kind,
                Name = ReadString(child, ""Name"", kind)
            });
        }

        return values.Count == 0 ? fallback : values;
    }
}

internal sealed class ULinkGameNodeOptions
{
    public string Id { get; init; } = ""dev-1"";

    public static ULinkGameNodeOptions FromConfiguration(IConfiguration section)
    {
        return new ULinkGameNodeOptions
        {
            Id = ReadString(section, ""Id"", ""dev-1"")
        };
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

internal sealed class ULinkGameEndpointOptions
{
    public string Transport { get; init; } = ""kcp"";
    public string Host { get; init; } = ""127.0.0.1"";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = """";

    public static ULinkGameEndpointOptions FromConfiguration(IConfiguration section)
    {
        var transport = NormalizeTransport(section[""Transport""], ""kcp"");
        return new ULinkGameEndpointOptions
        {
            Transport = transport,
            Host = ReadString(section, ""Host"", ""127.0.0.1""),
            Port = ReadInt(section, ""Port"", 20000),
            Path = ReadString(section, ""Path"", GetDefaultPath(transport))
        };
    }

    public string ToAdvertisedEndpoint()
    {
        var scheme = Transport switch
        {
            ""websocket"" => ""ws"",
            ""tcp"" => ""tcp"",
            _ => ""kcp""
        };

        return string.IsNullOrWhiteSpace(Path)
            ? $""{scheme}://{Host}:{Port}""
            : $""{scheme}://{Host}:{Port}{Path}"";
    }

    private static string NormalizeTransport(string? rawValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? fallback
            : rawValue.Trim().ToLowerInvariant();
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadInt(IConfiguration section, string name, int fallback)
    {
        return int.TryParse(section[name], out var value) && value > 0 ? value : fallback;
    }

    private static string GetDefaultPath(string transport)
    {
        return string.Equals(transport, ""websocket"", StringComparison.OrdinalIgnoreCase)
            ? ""/ws""
            : """";
    }
}

internal static class ULinkGameCheck
{
    public static int Run(ULinkGameRuntimeOptions runtime, ClusterOptions clusterOptions, string[] args)
    {
        var resolved = ToResolvedRuntime(runtime, clusterOptions);
        var validator = new ULinkGameRuntimeValidator(
            new IULinkGameValidationRule[]
            {
                new NodeIdentityRule(),
                new EndpointRule(),
                new HotfixSourceRule(),
                new ClusterServiceGraphRule()
            });
        var result = validator.Validate(resolved);

        if (args.Contains(""--json"", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    [""succeeded""] = result.Succeeded,
                    [""diagnostics""] = result.Diagnostics.Select(diagnostic => new
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
        var rpcEndpoint = clusterOptions.AdvertisedEndpoints.TryGetValue(""client"", out var clientEndpoint)
            ? clientEndpoint
            : runtime.Endpoint.ToAdvertisedEndpoint();

        Console.WriteLine(""cluster: ok single-node"");
        Console.WriteLine($""node: ok {clusterOptions.NodeId}"");
        Console.WriteLine($""services: ok {string.Join("", "", serviceNames)}"");
        var hotfixFailure = result.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == ""ULINK071"");
        if (hotfixFailure is not null)
        {
            Console.Error.WriteLine(""hotfix: failed local build output not found"");
            Console.Error.WriteLine($""fix: {hotfixFailure.Repair}"");
            return 1;
        }

        Console.WriteLine(""hotfix: ok local-build Server.Hotfix.dll"");
        Console.WriteLine(""reliable-push: ok pending limit 256, replay window 120s"");
        Console.WriteLine($""rpc: ok {rpcEndpoint}"");

        foreach (var diagnostic in result.Diagnostics.Where(diagnostic => diagnostic.Severity == ULinkGameDiagnosticSeverity.Error))
        {
            Console.Error.WriteLine($""{diagnostic.Code}: {diagnostic.Message}"");
            if (!string.IsNullOrWhiteSpace(diagnostic.Repair))
            {
                Console.Error.WriteLine($""fix: {diagnostic.Repair}"");
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
                ""../../../../Hotfix/bin/Debug/net10.0"",
                ""Server.Hotfix.dll""));

        return new ULinkGameResolvedRuntime(
            NodeId: new ULinkGameResolvedValue<string>(clusterOptions.NodeId, ULinkGameValueSource.Configuration, ""ULinkGame:Node:Id""),
            Endpoint: new ULinkGameResolvedEndpoint(
                Transport: new ULinkGameResolvedValue<string>(runtime.Endpoint.Transport, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Transport""),
                Host: new ULinkGameResolvedValue<string>(runtime.Endpoint.Host, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Host""),
                Port: new ULinkGameResolvedValue<int>(runtime.Endpoint.Port, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Port""),
                Path: new ULinkGameResolvedValue<string>(runtime.Endpoint.Path, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Path""),
                AdvertisedEndpoint: new ULinkGameResolvedValue<string>(runtime.Endpoint.ToAdvertisedEndpoint(), ULinkGameValueSource.GeneratedConvention)),
            Cluster: new ULinkGameResolvedCluster(
                Services: clusterOptions.Services
                    .Select(service => new ULinkGameResolvedClusterService(service.Kind, service.Name))
                    .ToArray(),
                AdvertisedEndpoints: clusterOptions.AdvertisedEndpoints),
            Hotfix: new ULinkGameResolvedHotfix(
                AssemblyPath: new ULinkGameResolvedValue<string>(hotfixPath, ULinkGameValueSource.GeneratedConvention),
                AssemblyFileName: new ULinkGameResolvedValue<string>(""Server.Hotfix.dll"", ULinkGameValueSource.GeneratedConvention)),
            ReliablePush: new ULinkGameResolvedReliablePush(
                StorageMode: new ULinkGameResolvedValue<string>(""InMemory"", ULinkGameValueSource.Default),
                PendingLimit: new ULinkGameResolvedValue<int>(256, ULinkGameValueSource.Default),
                ReplayWindowSeconds: new ULinkGameResolvedValue<int>(120, ULinkGameValueSource.Default),
                HasSessionIdentityResolver: true),
            Profile: ULinkGameRuntimeProfile.Development);
    }
}

internal sealed class ClusterOptions
{
    public string NodeId { get; init; } = ""gateway-1"";
    public IReadOnlyDictionary<string, string> AdvertisedEndpoints { get; init; } =
        new Dictionary<string, string>
        {
            [""cluster""] = ""tcp://127.0.0.1:21000"",
            [""client""] = ""tcp://127.0.0.1:20000""
        };
    public ClusterBootstrapOptions Bootstrap { get; init; } = new();
    public ClusterNodeDirectoryOptions NodeDirectory { get; init; } = new();
    public IReadOnlyList<ClusterServiceOptions> Services { get; init; } =
        new[]
        {
            new ClusterServiceOptions { Kind = ""node-directory"", Name = ""node-directory"" },
            new ClusterServiceOptions { Kind = ""route-directory"", Name = ""route-directory"" },
            new ClusterServiceOptions { Kind = ""gateway"", Name = ""gateway"" }
        };
    public int RouteLeaseSeconds { get; init; } = 30;
    public int SendTimeoutMilliseconds { get; init; } = 2000;

    public static ClusterOptions FromConfiguration(IConfiguration configuration)
    {
        return ULinkGameRuntimeOptions
            .FromConfiguration(configuration)
            .ToClusterOptions(configuration);
    }
}

internal sealed class ClusterBootstrapOptions
{
    public IReadOnlyList<string> NodeDirectoryEndpoints { get; init; } =
        new[] { ""tcp://127.0.0.1:21000"" };

    public static ClusterBootstrapOptions FromConfiguration(
        IConfigurationSection section,
        ClusterBootstrapOptions defaults)
    {
        return new ClusterBootstrapOptions
        {
            NodeDirectoryEndpoints = ReadList(section.GetSection(""NodeDirectoryEndpoints""), defaults.NodeDirectoryEndpoints)
        };
    }

    private static IReadOnlyList<string> ReadList(
        IConfigurationSection section,
        IReadOnlyList<string> fallback)
    {
        var values = new List<string>();
        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                values.Add(child.Value!);
            }
        }

        return values.Count == 0 ? fallback : values;
    }
}

internal sealed class ClusterNodeDirectoryOptions
{
    public bool Enabled { get; init; } = true;
    public ClusterNodeDirectoryStorageOptions Storage { get; init; } = new();

    public static ClusterNodeDirectoryOptions FromConfiguration(
        IConfigurationSection section,
        ClusterNodeDirectoryOptions defaults)
    {
        return new ClusterNodeDirectoryOptions
        {
            Enabled = ReadBool(section, ""Enabled"", defaults.Enabled),
            Storage = ClusterNodeDirectoryStorageOptions.FromConfiguration(section.GetSection(""Storage""), defaults.Storage)
        };
    }

    private static bool ReadBool(IConfiguration section, string name, bool fallback)
    {
        return bool.TryParse(section[name], out var value) ? value : fallback;
    }
}

internal sealed class ClusterNodeDirectoryStorageOptions
{
    public string Mode { get; init; } = ""InMemory"";
    public string Provider { get; init; } = """";
    public string ConnectionStringName { get; init; } = """";

    public static ClusterNodeDirectoryStorageOptions FromConfiguration(
        IConfigurationSection section,
        ClusterNodeDirectoryStorageOptions defaults)
    {
        return new ClusterNodeDirectoryStorageOptions
        {
            Mode = ReadString(section, ""Mode"", defaults.Mode),
            Provider = ReadString(section, ""Provider"", defaults.Provider),
            ConnectionStringName = ReadString(section, ""ConnectionStringName"", defaults.ConnectionStringName)
        };
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

internal sealed class ClusterServiceOptions
{
    public string Kind { get; init; } = """";
    public string Name { get; init; } = """";
}";
    }

    public static string RenderClusterHealthCheck()
    {
        return @"namespace Server.Hosting;

internal static class ClusterHealthCheck
{
    public static int Run(ClusterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.NodeId))
        {
            Console.Error.WriteLine(""Cluster health check failed: NodeId is required."");
            return 1;
        }

        if (options.AdvertisedEndpoints.Count == 0)
        {
            Console.Error.WriteLine(""Cluster health check failed: at least one advertised endpoint is required."");
            return 1;
        }

        if (options.Services.Count == 0)
        {
            Console.Error.WriteLine(""Cluster health check failed: at least one service is required."");
            return 1;
        }

        foreach (var endpoint in options.AdvertisedEndpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Key) ||
                string.IsNullOrWhiteSpace(endpoint.Value))
            {
                Console.Error.WriteLine(""Cluster health check failed: advertised endpoint keys and values are required."");
                return 1;
            }
        }

        Console.WriteLine(""cluster=healthy"");
        return 0;
    }
}";
    }

    public static string RenderDefaultConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = PackageCatalog.GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = PackageCatalog.GetTransportArtifacts(options.Transport);

        return $@"using Server.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Server.Hosting;

internal sealed class DefaultRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly ServerRpcServerOptions _options;

    public DefaultRpcServerConfigurator(ServerRpcServerOptions options)
    {{
        _options = options;
    }}

    public string Name => ""default"";

    public void Configure(ULinkGameServerRpcContext context)
    {{
        var builder = context.Builder;
        builder.UseSerializer(new {serializerType}());
{TemplateText.IndentBlock(RenderDefaultAcceptor(options.Transport), 2)}
        AllServicesBinder.BindAll(builder.ServiceRegistry);
    }}
}}";
    }

    public static string RenderControlPlaneConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = PackageCatalog.GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = PackageCatalog.GetTransportArtifacts(options.Transport);

        return $@"using Server.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Server.Hosting;

internal sealed class DefaultControlPlaneRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly ServerRpcServerOptions _options;

    public DefaultControlPlaneRpcServerConfigurator(ControlPlaneRpcServerOptions options)
    {{
        _options = options.Endpoint;
    }}

    public string Name => ""control"";

    public void Configure(ULinkGameServerRpcContext context)
    {{
        var builder = context.Builder;
        builder.UseSerializer(new {serializerType}());
{TemplateText.IndentBlock(RenderControlPlaneAcceptor(options.Transport), 2)}
        AllServicesBinder.BindAll(builder.ServiceRegistry);
    }}
}}";
    }

    public static string RenderRealtimeConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = PackageCatalog.GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = PackageCatalog.GetTransportArtifacts(options.Transport);

        return $@"using Server.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Server.Hosting;

internal sealed class DefaultRealtimeRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly ServerRpcServerOptions _options;

    public DefaultRealtimeRpcServerConfigurator(RealtimeRpcServerOptions options)
    {{
        _options = options.Endpoint;
    }}

    public string Name => ""realtime"";

    public void Configure(ULinkGameServerRpcContext context)
    {{
        var builder = context.Builder;
        builder.UseSerializer(new {serializerType}());
{TemplateText.IndentBlock(RenderRealtimeAcceptor(options.Transport), 2)}
        AllServicesBinder.BindAll(builder.ServiceRegistry);
    }}
}}";
    }

    private static string RenderHotfixServiceRegistration()
    {
        return """
        var hotfixDirectory = ResolveHotfixDirectory("../../../../Hotfix/bin/Debug/net10.0");
        builder.Services.AddULinkGameHotfix(
            new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, "Server.Hotfix.dll"),
            sharedAssemblyNames: ["Shared"]);
        """;
    }

    private static string RenderHotfixHelpers()
    {
        return """

        static async Task LoadInitialHotfixAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var hotfix = scope.ServiceProvider.GetRequiredService<IHotfixManager>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Server.Hotfix");
            var result = await hotfix.ReloadAsync();
            if (result.Succeeded)
            {
                logger.LogInformation(
                    "Initial hotfix load succeeded from {HotfixPath} with {MethodCount} method(s).",
                    result.Current.SourcePath,
                    result.Current.Methods.Count);
                return;
            }

            logger.LogWarning(
                "Initial hotfix load failed for {HotfixPath}: {ErrorMessage}",
                result.RequestedPath,
                result.ErrorMessage);
            foreach (var diagnostic in result.Diagnostics)
            {
                logger.LogWarning("Hotfix diagnostic: {Diagnostic}", diagnostic);
            }
        }

        static string ResolveHotfixDirectory(string configuredDirectory)
        {
            if (Path.IsPathFullyQualified(configuredDirectory))
            {
                return configuredDirectory;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredDirectory));
        }
        """;
    }

    private static string GetDefaultPath(string transport, string websocketPath)
    {
        return string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase) ? websocketPath : "";
    }

    private static string RenderPersistencePackageReferences(string persistence, bool includeDapper)
    {
        if (!ProjectConventions.UsesExternalPersistence(persistence))
        {
            return string.Empty;
        }

        var references = new List<string>();
        if (includeDapper)
        {
            references.Add($"""<PackageReference Include="Dapper" Version="{ToolPackageVersions.Dapper}" />""");
        }

        references.Add(string.Equals(persistence, "mysql", StringComparison.OrdinalIgnoreCase)
            ? $"""<PackageReference Include="MySqlConnector" Version="{ToolPackageVersions.MySqlConnector}" />"""
            : $"""<PackageReference Include="Npgsql" Version="{ToolPackageVersions.Npgsql}" />""");

        return TemplateText.IndentBlock(string.Join(Environment.NewLine, references), 3);
    }

    private static string RenderClusterPackageReferences(NewCommandOptions options)
    {
        if (!ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile))
        {
            return string.Empty;
        }

        var references = new[]
        {
            $"""<PackageReference Include="ULinkGame.Cluster" Version="{ToolPackageVersions.ULinkGameCluster}" />""",
            $"""<PackageReference Include="ULinkGame.Cluster.ULinkRPC" Version="{ToolPackageVersions.ULinkGameClusterULinkRpc}" />"""
        };

        return TemplateText.IndentBlock(string.Join(Environment.NewLine, references), 3);
    }

    private static string RenderClusterServiceRegistration(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? "builder.Services.AddSingleton(runtimeOptions.ToClusterOptions(builder.Configuration));"
            : string.Empty;
    }

    private static string RenderULinkGameCheckExit(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? """
              if (args.Contains("--ulinkgame-check", StringComparer.Ordinal))
              {
                  return ULinkGameCheck.Run(runtimeOptions, runtimeOptions.ToClusterOptions(builder.Configuration), args);
              }
              """
            : string.Empty;
    }

    private static string RenderClusterHealthCheckExit(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? """
              if (args.Contains("--health-check", StringComparer.Ordinal))
              {
                  return ClusterHealthCheck.Run(runtimeOptions.ToClusterOptions(builder.Configuration));
              }
              """
            : string.Empty;
    }

    public static string RenderServerDockerfile()
    {
        return """
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet publish Server/Server/Server.csproj -c Release -o /app

        FROM mcr.microsoft.com/dotnet/runtime:10.0
        WORKDIR /app
        COPY --from=build /app .
        ENTRYPOINT ["dotnet", "Server.dll"]
        """;
    }

    public static string RenderClusterCompose(NewCommandOptions options)
    {
        var endpointPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var advertisedClientEndpoint = RenderAdvertisedClientEndpoint(options.Transport, "gateway", 20000, endpointPath);
        var healthCommand = "dotnet Server.dll --health-check";

        return $$"""
        services:
          gateway:
            build:
              context: .
              dockerfile: Server/Dockerfile
            environment:
              ULinkGame__Endpoint__Transport: "{{TemplateText.SanitizeStringLiteral(options.Transport)}}"
              ULinkGame__Endpoint__Host: "0.0.0.0"
              ULinkGame__Endpoint__Port: "20000"
              ULinkGame__Endpoint__Path: "{{TemplateText.SanitizeStringLiteral(endpointPath)}}"
              Cluster__NodeId: "${ULINKGAME_CLUSTER_NODE_ID:-gateway-1}"
              Cluster__AdvertisedEndpoints__cluster: "${ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLUSTER:-tcp://gateway:21000}"
              Cluster__AdvertisedEndpoints__client: "${ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT:-{{TemplateText.SanitizeStringLiteral(advertisedClientEndpoint)}}}"
              Cluster__Bootstrap__NodeDirectoryEndpoints__0: "${ULINKGAME_CLUSTER_BOOTSTRAP_NODE_DIRECTORY_ENDPOINT_0:-tcp://gateway:21000}"
              Cluster__NodeDirectory__Enabled: "${ULINKGAME_CLUSTER_NODE_DIRECTORY_ENABLED:-true}"
              Cluster__NodeDirectory__Storage__Mode: "${ULINKGAME_CLUSTER_NODE_DIRECTORY_STORAGE_MODE:-InMemory}"
              Cluster__Services__0__Kind: "node-directory"
              Cluster__Services__0__Name: "node-directory"
              Cluster__Services__1__Kind: "route-directory"
              Cluster__Services__1__Name: "route-directory"
              Cluster__Services__2__Kind: "gateway"
              Cluster__Services__2__Name: "gateway"
              Cluster__RouteLeaseSeconds: "${ULINKGAME_CLUSTER_ROUTE_LEASE_SECONDS:-30}"
              Cluster__SendTimeoutMilliseconds: "${ULINKGAME_CLUSTER_SEND_TIMEOUT_MILLISECONDS:-2000}"
            ports:
              - "20000:20000"
            healthcheck:
              test: ["CMD-SHELL", "{{TemplateText.SanitizeStringLiteral(healthCommand)}}"]
              interval: 10s
              timeout: 3s
              retries: 3
              start_period: 10s
        """;
    }

    public static string RenderClusterEnvExample(NewCommandOptions options)
    {
        var endpointPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var advertisedClientEndpoint = RenderAdvertisedClientEndpoint(options.Transport, "gateway", 20000, endpointPath);

        return $$"""
        # This file intentionally contains no production secrets.
        # Put node authentication and TLS material in your deployment platform secret store.
        ULINKGAME_CLUSTER_NODE_ID=gateway-1
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLUSTER=tcp://gateway:21000
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT={{advertisedClientEndpoint}}
        ULINKGAME_CLUSTER_BOOTSTRAP_NODE_DIRECTORY_ENDPOINT_0=tcp://gateway:21000
        ULINKGAME_CLUSTER_NODE_DIRECTORY_ENABLED=true
        ULINKGAME_CLUSTER_NODE_DIRECTORY_STORAGE_MODE=InMemory
        ULINKGAME_CLUSTER_ROUTE_LEASE_SECONDS=30
        ULINKGAME_CLUSTER_SEND_TIMEOUT_MILLISECONDS=2000
        """;
    }

    public static string RenderClusterOperationsGuide()
    {
        return """
        # Cluster Operations

        This scaffold is an opt-in starting point for local cluster deployment rehearsal.

        It intentionally does not define production secrets. Node authentication keys, TLS certificates, database credentials, and deployment tokens must come from the deployment platform secret store or a project-owned secret management flow.

        Generated cluster settings can be overridden with environment variables:

        - `Cluster__NodeId`
        - `Cluster__AdvertisedEndpoints__cluster`
        - `Cluster__AdvertisedEndpoints__client`
        - `Cluster__Bootstrap__NodeDirectoryEndpoints__0`
        - `Cluster__NodeDirectory__Enabled`
        - `Cluster__NodeDirectory__Storage__Mode`
        - `Cluster__Services__0__Kind`
        - `Cluster__Services__0__Name`
        - `Cluster__RouteLeaseSeconds`
        - `Cluster__SendTimeoutMilliseconds`

        Health check:

        ```bash
        dotnet Server.dll --health-check
        ```

        The generated health check validates that local cluster configuration has a node id, at least one advertised endpoint, and at least one configured service. Remote node-directory, route-directory, and node-messenger dependency checks should be wired by the project host using `ULinkRpcClusterDependencyProbe` once the project chooses its concrete topology and secret policy.
        """;
    }

    private static string RenderDefaultAcceptor(string transport)
    {
        return transport switch
        {
            "websocket" => """
                var path = string.IsNullOrWhiteSpace(_options.Path) ? "/ws" : _options.Path;
                builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
                    builder.ResolvePort(_options.Port),
                    path,
                    builder.Limits.MaxPendingAcceptedConnections,
                    ct));
                """,
            "tcp" => """
                builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(_options.Port)));
                """,
            _ => """
                builder.UseAcceptor(new KcpConnectionAcceptor(
                    builder.ResolvePort(_options.Port),
                    builder.Limits.MaxPendingAcceptedConnections));
                """
        };
    }

    private static string RenderControlPlaneAcceptor(string transport)
    {
        return transport switch
        {
            "websocket" => """
                var path = string.IsNullOrWhiteSpace(_options.Path) ? "/ws" : _options.Path;
                builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
                    builder.ResolvePort(_options.Port),
                    path,
                    builder.Limits.MaxPendingAcceptedConnections,
                    ct));
                """,
            "tcp" => """
                builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(_options.Port)));
                """,
            _ => """
                builder.UseAcceptor(new KcpConnectionAcceptor(
                    builder.ResolvePort(_options.Port),
                    builder.Limits.MaxPendingAcceptedConnections));
                """
        };
    }

    private static string RenderRealtimeAcceptor(string transport)
    {
        return transport switch
        {
            "websocket" => """
                var path = string.IsNullOrWhiteSpace(_options.Path) ? "/realtime" : _options.Path;
                builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
                    builder.ResolvePort(_options.Port),
                    path,
                    builder.Limits.MaxPendingAcceptedConnections,
                    ct));
                """,
            "tcp" => """
                builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(_options.Port)));
                """,
            _ => """
                builder.UseAcceptor(new KcpConnectionAcceptor(
                    builder.ResolvePort(_options.Port),
                    builder.Limits.MaxPendingAcceptedConnections));
                """
        };
    }

    private static string RenderAdvertisedClientEndpoint(
        string transport,
        string host,
        int port,
        string path)
    {
        var scheme = transport switch
        {
            "websocket" => "ws",
            "tcp" => "tcp",
            _ => "kcp"
        };
        return string.IsNullOrWhiteSpace(path)
            ? $"{scheme}://{host}:{port}"
            : $"{scheme}://{host}:{port}{path}";
    }
}

internal static class PackageCatalog
{
    public static (PackageArtifact PackageId, string SerializerType) GetSerializerArtifacts(string serializer)
    {
        return serializer switch
        {
            "json" => (new PackageArtifact("ULinkRPC.Serializer.Json", "", "ULinkRPC.Serializer.Json"), "JsonRpcSerializer"),
            _ => (new PackageArtifact("ULinkRPC.Serializer.MemoryPack", "", "ULinkRPC.Serializer.MemoryPack"), "MemoryPackRpcSerializer")
        };
    }

    public static (PackageArtifact PackageId, string AcceptorType) GetTransportArtifacts(string transport)
    {
        return transport switch
        {
            "tcp" => (new PackageArtifact("ULinkRPC.Transport.Tcp", "", "ULinkRPC.Transport.Tcp"), "TcpConnectionAcceptor"),
            "websocket" => (new PackageArtifact("ULinkRPC.Transport.WebSocket", "", "ULinkRPC.Transport.WebSocket"), "WsConnectionAcceptor"),
            _ => (new PackageArtifact("ULinkRPC.Transport.Kcp", "", "ULinkRPC.Transport.Kcp"), "KcpConnectionAcceptor")
        };
    }
}

internal static class TemplateText
{
    public static string SanitizeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static string SanitizeCSharpIdentifier(string value)
    {
        var sanitized = new string(value.Select(static c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "Game";
        }

        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }

    public static string IndentBlock(string block, int level)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return string.Empty;
        }

        var indent = new string(' ', level * 4);
        var lines = block.Replace("\r\n", "\n").Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : indent + line));
    }

}
