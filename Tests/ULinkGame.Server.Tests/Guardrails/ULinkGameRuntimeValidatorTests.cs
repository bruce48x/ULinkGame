using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Guardrails.Rules;
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
        Assert.Equal("kcp", runtime.Endpoints[0].Transport.Value);
        Assert.Equal("Server.Hotfix.dll", runtime.Hotfix.AssemblyFileName.Value);
        Assert.Equal(ULinkGameRuntimeProfile.Development, runtime.Profile);
    }

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
            Endpoints = [TestEndpoint("websocket", "127.0.0.1", 20000, path: "")]
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
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "ULINK071");
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
    public void EndpointRule_rejects_missing_transport()
    {
        var runtime = TestRuntime() with
        {
            Endpoints = [TestEndpoint("", "127.0.0.1", 20000)]
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK020");
    }

    [Fact]
    public void EndpointRule_rejects_missing_host()
    {
        var runtime = TestRuntime() with
        {
            Endpoints = [TestEndpoint("kcp", "", 20000)]
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK021");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void EndpointRule_rejects_invalid_port(int port)
    {
        var runtime = TestRuntime() with
        {
            Endpoints = [TestEndpoint("kcp", "127.0.0.1", port)]
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK022");
    }

    [Fact]
    public void EndpointRule_rejects_unknown_transport()
    {
        var runtime = TestRuntime() with
        {
            Endpoints = [TestEndpoint("quic", "127.0.0.1", 20000)]
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK020");
    }

    [Fact]
    public void EndpointRule_rejects_duplicate_bind_address()
    {
        var runtime = TestRuntime() with
        {
            Endpoints =
            [
                TestEndpoint("kcp", "127.0.0.1", 20000),
                TestEndpoint("tcp", "127.0.0.1", 20000)
            ]
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK026");
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

    [Theory]
    [InlineData("udp://127.0.0.1:21000")]
    [InlineData("tcp://127.0.0.1")]
    [InlineData("tcp://127.0.0.1:0")]
    [InlineData("tcp://:21000")]
    public void ClusterEndpointRule_rejects_unsupported_cluster_uri(string endpoint)
    {
        var runtime = TestRuntime() with
        {
            ClusterEndpoint = TestClusterEndpoint(endpoint)
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK041");
    }

    [Fact]
    public void ClusterEndpointRule_rejects_business_port_conflict()
    {
        var runtime = TestRuntime() with
        {
            Endpoints = [TestEndpoint("kcp", "127.0.0.1", 20000)],
            ClusterEndpoint = TestClusterEndpoint("tcp://127.0.0.1:20000")
        };

        var result = Validate(runtime);

        Assert.Contains(result.Diagnostics, d => d.Code == "ULINK042");
    }

    [Fact]
    public void AddULinkGameRuntimeValidation_RegistersDefaultValidator()
    {
        var services = new ServiceCollection();

        services.AddULinkGameRuntimeValidation();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<ULinkGameRuntimeValidator>();

        Assert.NotNull(validator);
    }

    private static ULinkGameResolvedRuntime TestRuntime()
    {
        return new ULinkGameResolvedRuntime(
            NodeId: new ULinkGameResolvedValue<string>("dev-1", ULinkGameValueSource.Configuration, "ULinkGame:Node:Id"),
            Endpoints: [TestEndpoint("kcp", "127.0.0.1", 20000)],
            Cluster: new ULinkGameResolvedCluster(
                Services: [new ULinkGameResolvedClusterService("gateway", "gateway")],
                AdvertisedEndpoints: new Dictionary<string, string> { ["client"] = "kcp://127.0.0.1:20000" }),
            ClusterEndpoint: null,
            Feature: new ULinkGameResolvedFeature(
                Configured: null,
                Active: [],
                StartupOrder: []),
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

    private static ULinkGameResolvedClusterEndpoint TestClusterEndpoint(string endpoint)
    {
        return new ULinkGameResolvedClusterEndpoint(
            Endpoint: new ULinkGameResolvedValue<string>(endpoint, ULinkGameValueSource.Configuration, "ULinkGame:Cluster:Endpoint"),
            Seeds: []);
    }

    private static ULinkGameValidationResult Validate(ULinkGameResolvedRuntime runtime)
    {
        var validator = new ULinkGameRuntimeValidator(
            [
                new NodeIdentityRule(),
                new EndpointRule(),
                new ClusterEndpointRule(),
                new HotfixSourceRule(),
                new ClusterServiceGraphRule()
            ]);

        return validator.Validate(runtime);
    }
}
