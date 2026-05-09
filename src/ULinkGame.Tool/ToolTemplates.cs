internal static class ToolTemplates
{
    public static string RenderDotNetToolManifest()
    {
        return $$"""
        {
          "version": 1,
          "isRoot": true,
          "tools": {
            "ulinkrpc.starter": {
              "version": "{{ToolPackageVersions.ULinkRpcStarter}}",
              "commands": [
                "ulinkrpc-starter",
                "ulinkrpc-codegen"
              ],
              "rollForward": false
            }
          }
        }
        """;
    }

    public static string RenderServerSolution()
    {
        return """
        <Solution>
          <Project Path="../Shared/Shared.csproj" />
          <Project Path="Edge/Edge.csproj" />
          <Project Path="Silo/Silo.csproj" />
        </Solution>
        """;
    }

    public static string RenderEdgeProgram()
    {
        return """
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Edge.Hosting;
        using ULinkGame.Server.Hosting;

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        builder.AddULinkGameServerOrleansClient();
        builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
            EdgeRpcServerOptions.FromConfiguration(
                builder.Configuration,
                "ControlPlane",
                new EdgeRpcServerOptions { Transport = "websocket", Port = 20000, Path = "/ws" })));
        builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
            EdgeRpcServerOptions.FromConfiguration(
                builder.Configuration,
                "Realtime",
                new EdgeRpcServerOptions { Transport = "kcp", Port = 20001, Path = "" })));
        builder.Services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
        builder.Services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
        builder.Services.AddULinkGameServerGateway();

        var host = builder.Build();
        await host.RunAsync();
        """;
    }

    public static string RenderEdgeProject()
    {
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
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="..\..\Shared\Shared.csproj" TargetFramework="net10.0">
              <SetTargetFramework>TargetFramework=net10.0</SetTargetFramework>
            </ProjectReference>
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Server" Version="{{ToolPackageVersions.ULinkGameServer}}" />
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

        return $$"""
        {
          "Orleans": {
            "ClusterId": "dev",
            "ServiceId": "{{TemplateText.SanitizeStringLiteral(options.Name ?? ProjectConventions.DefaultProjectName)}}-Server"
          },
          "ControlPlane": {
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

    public static string RenderSiloProject()
    {
        return $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <BuildInParallel>false</BuildInParallel>
            <RestoreBuildInParallel>false</RestoreBuildInParallel>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Server" Version="{{ToolPackageVersions.ULinkGameServer}}" />
            <PackageReference Include="Microsoft.Orleans.Server" Version="{{ToolPackageVersions.Orleans}}" />
          </ItemGroup>

          <ItemGroup>
            <None Update="appsettings.json">
              <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            </None>
          </ItemGroup>
        </Project>
        """;
    }

    public static string RenderSiloProgram()
    {
        return """
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Orleans.Hosting;
        using ULinkGame.Server.Hosting;

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureAppConfiguration(configuration =>
            {
                configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
            })
            .UseULinkGameServerOrleansSilo((context, silo) =>
            {
                silo.AddMemoryGrainStorage("users");
                silo.AddMemoryGrainStorage("sessions");
                silo.AddMemoryGrainStorage("matchmaking");
                silo.AddMemoryGrainStorage("rooms");
            })
            .Build();

        await host.RunAsync();
        """;
    }

    public static string RenderSiloAppSettings()
    {
        return """
        {
          "Orleans": {
            "ClusterId": "dev",
            "ServiceId": "ULinkGame-Server",
            "SiloPort": 11111,
            "GatewayPort": 30000
          }
        }
        """;
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
