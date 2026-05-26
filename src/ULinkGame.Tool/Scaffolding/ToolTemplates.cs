internal static class ToolTemplates
{
    public static string RenderServerSolution()
    {
        return """
        <Solution>
          <Project Path="../Shared/Shared.csproj" />
          <Project Path="Edge/Edge.csproj" />
        </Solution>
        """;
    }

    public static string RenderEdgeProgram(NewCommandOptions options)
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
            using Edge.Hosting;
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
                EdgeRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "ControlPlane",
                    new EdgeRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20000, Path = "{{TemplateText.SanitizeStringLiteral(controlPath)}}" })));
            builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
                EdgeRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "Realtime",
                    new EdgeRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20001, Path = "{{TemplateText.SanitizeStringLiteral(realtimePath)}}" })));
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
        using Edge.Hosting;
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
            EdgeRpcServerOptions.FromConfiguration(
                builder.Configuration,
                "Endpoint",
                new EdgeRpcServerOptions { Transport = "{{TemplateText.SanitizeStringLiteral(options.Transport)}}", Port = 20000, Path = "{{TemplateText.SanitizeStringLiteral(endpointPath)}}" }));
        builder.Services.AddULinkRpcServer<DefaultRpcServerConfigurator>();
        builder.Services.AddULinkGameServerGateway();

        var host = builder.Build();
        await host.RunAsync();
        return 0;
        """;
    }

    public static string RenderEdgeProject(NewCommandOptions options)
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
            <RootNamespace>Edge</RootNamespace>
            <BuildInParallel>false</BuildInParallel>
            <RestoreBuildInParallel>false</RestoreBuildInParallel>
            <ULinkRPCGenerateServer>true</ULinkRPCGenerateServer>
            <ULinkRPCServerGeneratedNamespace>Edge.Generated</ULinkRPCServerGeneratedNamespace>
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

    public static string RenderEdgeAppSettings(NewCommandOptions options)
    {
        var realtimePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/realtime" : "";
        var controlPlanePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
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
                    "NodeId": "edge-1",
                    "NodeEpoch": 1,
                    "InternalEndpoint": "tcp://127.0.0.1:21000",
                    "RouteDirectoryEndpoint": "tcp://127.0.0.1:21001",
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

    public static string RenderEdgeRpcServerOptions()
    {
        return @"using Microsoft.Extensions.Configuration;

namespace Edge.Hosting;

internal sealed class EdgeRpcServerOptions
{
    public string Transport { get; init; } = ""websocket"";
    public string Host { get; init; } = ""127.0.0.1"";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = """";

    public static EdgeRpcServerOptions FromConfiguration(
        IConfiguration configuration,
        string sectionName,
        EdgeRpcServerOptions defaults)
    {
        var section = configuration.GetSection(sectionName);
        var transport = NormalizeTransport(section[""Transport""], defaults.Transport);
        var host = section[""Host""];
        var path = section[""Path""];

        return new EdgeRpcServerOptions
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
        return $@"namespace Edge.Hosting;

internal sealed class {typeName}
{{
    public {typeName}(EdgeRpcServerOptions endpoint)
    {{
        Endpoint = endpoint;
    }}

    public EdgeRpcServerOptions Endpoint {{ get; }}
}}";
    }

    public static string RenderClusterOptions()
    {
        return @"using Microsoft.Extensions.Configuration;

namespace Edge.Hosting;

internal sealed class ClusterOptions
{
    public string NodeId { get; init; } = ""edge-1"";
    public long NodeEpoch { get; init; } = 1;
    public string InternalEndpoint { get; init; } = ""tcp://127.0.0.1:21000"";
    public string RouteDirectoryEndpoint { get; init; } = ""tcp://127.0.0.1:21001"";
    public int RouteLeaseSeconds { get; init; } = 30;
    public int SendTimeoutMilliseconds { get; init; } = 2000;

    public static ClusterOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(""Cluster"");
        var defaults = new ClusterOptions();
        return new ClusterOptions
        {
            NodeId = ReadString(section, ""NodeId"", defaults.NodeId),
            NodeEpoch = ReadLong(section, ""NodeEpoch"", defaults.NodeEpoch),
            InternalEndpoint = ReadString(section, ""InternalEndpoint"", defaults.InternalEndpoint),
            RouteDirectoryEndpoint = ReadString(section, ""RouteDirectoryEndpoint"", defaults.RouteDirectoryEndpoint),
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

    private static long ReadLong(IConfiguration section, string name, long fallback)
    {
        return long.TryParse(section[name], out var value) && value >= 0 ? value : fallback;
    }
}";
    }

    public static string RenderClusterHealthCheck()
    {
        return @"namespace Edge.Hosting;

internal static class ClusterHealthCheck
{
    public static int Run(ClusterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.NodeId))
        {
            Console.Error.WriteLine(""Cluster health check failed: NodeId is required."");
            return 1;
        }

        if (options.NodeEpoch < 0)
        {
            Console.Error.WriteLine(""Cluster health check failed: NodeEpoch cannot be negative."");
            return 1;
        }

        if (!IsTcpEndpoint(options.InternalEndpoint))
        {
            Console.Error.WriteLine(""Cluster health check failed: InternalEndpoint must be a tcp:// endpoint."");
            return 1;
        }

        if (!IsTcpEndpoint(options.RouteDirectoryEndpoint))
        {
            Console.Error.WriteLine(""Cluster health check failed: RouteDirectoryEndpoint must be a tcp:// endpoint."");
            return 1;
        }

        Console.WriteLine(""cluster=healthy"");
        return 0;
    }

    private static bool IsTcpEndpoint(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, ""tcp"", StringComparison.OrdinalIgnoreCase) &&
            uri.Port > 0;
    }
}";
    }

    public static string RenderDefaultConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = PackageCatalog.GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = PackageCatalog.GetTransportArtifacts(options.Transport);

        return $@"using Edge.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Edge.Hosting;

internal sealed class DefaultRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly EdgeRpcServerOptions _options;

    public DefaultRpcServerConfigurator(EdgeRpcServerOptions options)
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

        return $@"using Edge.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Edge.Hosting;

internal sealed class DefaultControlPlaneRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly EdgeRpcServerOptions _options;

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

        return $@"using Edge.Generated;
using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Edge.Hosting;

internal sealed class DefaultRealtimeRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly EdgeRpcServerOptions _options;

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
        RUN dotnet publish Server/Edge/Edge.csproj -c Release -o /app

        FROM mcr.microsoft.com/dotnet/runtime:10.0
        WORKDIR /app
        COPY --from=build /app .
        ENTRYPOINT ["dotnet", "Edge.dll"]
        """;
    }

    public static string RenderClusterCompose(NewCommandOptions options)
    {
        var endpointPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var healthCommand = "dotnet Edge.dll --health-check";

        return $$"""
        services:
          edge:
            build:
              context: .
              dockerfile: Server/Dockerfile
            environment:
              Endpoint__Transport: "{{TemplateText.SanitizeStringLiteral(options.Transport)}}"
              Endpoint__Host: "0.0.0.0"
              Endpoint__Port: "20000"
              Endpoint__Path: "{{TemplateText.SanitizeStringLiteral(endpointPath)}}"
              Cluster__NodeId: "${ULINKGAME_CLUSTER_NODE_ID:-edge-1}"
              Cluster__NodeEpoch: "${ULINKGAME_CLUSTER_NODE_EPOCH:-1}"
              Cluster__InternalEndpoint: "${ULINKGAME_CLUSTER_INTERNAL_ENDPOINT:-tcp://edge:21000}"
              Cluster__RouteDirectoryEndpoint: "${ULINKGAME_CLUSTER_ROUTE_DIRECTORY_ENDPOINT:-tcp://edge:21001}"
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
        ULINKGAME_CLUSTER_NODE_ID=edge-1
        ULINKGAME_CLUSTER_NODE_EPOCH=1
        ULINKGAME_CLUSTER_INTERNAL_ENDPOINT=tcp://edge:21000
        ULINKGAME_CLUSTER_ROUTE_DIRECTORY_ENDPOINT=tcp://edge:21001
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
        - `Cluster__NodeEpoch`
        - `Cluster__InternalEndpoint`
        - `Cluster__RouteDirectoryEndpoint`
        - `Cluster__RouteLeaseSeconds`
        - `Cluster__SendTimeoutMilliseconds`

        Health check:

        ```bash
        dotnet Edge.dll --health-check
        ```

        The generated health check validates local cluster configuration. Remote route-directory and node-messenger dependency checks should be wired by the project host using `ULinkRpcClusterDependencyProbe` once the project chooses its concrete topology and secret policy.
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
