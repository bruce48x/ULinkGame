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
}
