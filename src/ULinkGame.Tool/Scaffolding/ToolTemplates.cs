internal static class ToolTemplates
{
    public static string RenderServerSolution()
    {
        return """
        <Solution>
          <Project Path="../Shared/Shared.csproj" />
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
            builder.Services.AddULinkGameServerGateway();

            var host = builder.Build();
            await host.RunAsync();
            return 0;
            """;
        }

        var endpointPath = GetDefaultPath(options.Transport, "/ws");

        return $$"""
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Server.Hosting;
        using ULinkGame.Server;
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
        {{RenderClusterServiceRegistration(options)}}
        builder.Services.AddSingleton(_ =>
            ServerRpcServerOptions.FromConfiguration(
                builder.Configuration,
                "Endpoint",
                new ServerRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20000, Path = "{{TemplateText.SanitizeStringLiteral(endpointPath)}}" }));
        builder.Services.AddULinkRpcServer<DefaultRpcServerConfigurator>();
        builder.Services.AddULinkGameServerGateway();

        var host = builder.Build();
        await host.RunAsync();
        return 0;
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
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Server" Version="{{ToolPackageVersions.ULinkGameServer}}" />
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
        var realtimePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/realtime" : "";
        var controlPlanePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var clientEndpoint = RenderAdvertisedClientEndpoint(options.Transport, "127.0.0.1", 20000, controlPlanePath);
        if (!ProjectConventions.IsRealtimeNetworkProfile(options.NetworkProfile))
        {
            if (ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile))
            {
                return $$"""
                {
                  "Endpoint": {
                    "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
                    "Host": "127.0.0.1",
                    "Port": 20000,
                    "Path": "{{TemplateText.SanitizeStringLiteral(controlPlanePath)}}"
                  },
                  "Cluster": {
                    "NodeId": "gateway-1",
                    "AdvertisedEndpoints": {
                      "cluster": "tcp://127.0.0.1:21000",
                      "client": "{{TemplateText.SanitizeStringLiteral(clientEndpoint)}}"
                    },
                    "Bootstrap": {
                      "NodeDirectoryEndpoints": [
                        "tcp://127.0.0.1:21000"
                      ]
                    },
                    "NodeDirectory": {
                      "Enabled": true,
                      "Storage": {
                        "Mode": "InMemory"
                      }
                    },
                    "Services": [
                      { "Kind": "node-directory", "Name": "node-directory" },
                      { "Kind": "route-directory", "Name": "route-directory" },
                      { "Kind": "gateway", "Name": "gateway" }
                    ],
                    "RouteLeaseSeconds": 30,
                    "SendTimeoutMilliseconds": 2000
                  }
                }
                """;
            }

            return $$"""
            {
              "Endpoint": {
                "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
                "Host": "127.0.0.1",
                "Port": 20000,
                "Path": "{{TemplateText.SanitizeStringLiteral(controlPlanePath)}}"
              }
            }
            """;
        }

        return $$"""
        {
          "ControlPlane": {
            "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
            "Host": "127.0.0.1",
            "Port": 20000,
            "Path": "{{TemplateText.SanitizeStringLiteral(controlPlanePath)}}"
          },
          "Realtime": {
            "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
            "Host": "127.0.0.1",
            "Port": 20001,
            "Path": "{{TemplateText.SanitizeStringLiteral(realtimePath)}}"
          }
        }
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
        return @"using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Server.Hosting;

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
        var section = configuration.GetSection(""Cluster"");
        var defaults = new ClusterOptions();
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
            ? "builder.Services.AddSingleton(_ => ClusterOptions.FromConfiguration(builder.Configuration));"
            : string.Empty;
    }

    private static string RenderClusterHealthCheckExit(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? """
              if (args.Contains("--health-check", StringComparer.Ordinal))
              {
                  return ClusterHealthCheck.Run(ClusterOptions.FromConfiguration(builder.Configuration));
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
              Endpoint__Transport: "{{TemplateText.SanitizeStringLiteral(options.Transport)}}"
              Endpoint__Host: "0.0.0.0"
              Endpoint__Port: "20000"
              Endpoint__Path: "{{TemplateText.SanitizeStringLiteral(endpointPath)}}"
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

    public static string RenderClusterEnvExample()
    {
        return """
        # This file intentionally contains no production secrets.
        # Put node authentication and TLS material in your deployment platform secret store.
        ULINKGAME_CLUSTER_NODE_ID=gateway-1
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLUSTER=tcp://gateway:21000
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT=tcp://gateway:20000
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
