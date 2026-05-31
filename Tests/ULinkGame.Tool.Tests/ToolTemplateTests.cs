using Xunit;

namespace ULinkGame.Tool.Tests;

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

    [Fact]
    public void RenderServerProgram_DefaultSingleEndpoint_UsesRuntimeOptionsFromCompactSection()
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
        var normalizedSource = source.Replace("\r\n", "\n");

        Assert.Contains("var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration)", source);
        Assert.Contains("builder.Services.AddSingleton(runtimeOptions)", source);
        Assert.Contains("builder.Services.AddSingleton(runtimeOptions.ToServerRpcServerOptions())", source);
        Assert.Contains("builder.Services.AddSingleton(runtimeOptions.ToClusterOptions(builder.Configuration))", source);
        Assert.DoesNotContain("\"ULinkGame:Endpoint\"", source);
        Assert.DoesNotContain("\n            \"Endpoint\",\n", normalizedSource);
    }

    [Fact]
    public void RenderServerProgram_DefaultSingleEndpoint_IncludesULinkGameCheckCommand()
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
        Assert.True(
            source.IndexOf("ULinkGameCheck.Run(runtimeOptions)", StringComparison.Ordinal) >
            source.IndexOf("var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration)", StringComparison.Ordinal));
        Assert.True(
            source.IndexOf("ULinkGameCheck.Run(runtimeOptions)", StringComparison.Ordinal) <
            source.IndexOf("builder.Services.AddULinkGameServer()", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderServerProgram_RealtimeProfile_DoesNotReferenceRuntimeOptionsHelper()
    {
        var options = new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: ProjectConventions.DefaultClientEngine,
            Transport: "websocket",
            NetworkProfile: "realtime",
            Serializer: ProjectConventions.DefaultSerializer,
            Persistence: ProjectConventions.DefaultPersistence,
            NuGetForUnitySource: ProjectConventions.DefaultNuGetForUnitySource,
            DeployProfile: ProjectConventions.DefaultDeployProfile);

        var source = ToolTemplates.RenderServerProgram(options);

        Assert.DoesNotContain("ULinkGameRuntimeOptions", source);
        Assert.Contains("ServerRpcServerOptions.FromConfiguration", source);
        Assert.Contains("\"ControlPlane\"", source);
        Assert.Contains("\"Realtime\"", source);
    }

    [Fact]
    public void RenderClusterOptions_DefinesRuntimeOptionsFromCompactULinkGameSection()
    {
        var source = ToolTemplates.RenderClusterOptions();

        Assert.Contains("ULinkGameRuntimeOptions", source);
        Assert.Contains("ULinkGameNodeOptions", source);
        Assert.Contains("ULinkGameEndpointOptions", source);
        Assert.Contains("configuration.GetSection(\"ULinkGame\")", source);
        Assert.Contains("ToClusterOptions()", source);
        Assert.Contains("ToServerRpcServerOptions()", source);
        Assert.Contains("ULinkGame:Node:Id", source);
        Assert.Contains("ULinkGame:Endpoint:Transport", source);
        Assert.Contains("ULinkGame:Endpoint:Host", source);
        Assert.Contains("ULinkGame:Endpoint:Port", source);
        Assert.Contains("ULinkGame:Endpoint:Path", source);
        Assert.Contains("[\"cluster\"] = ClusterEndpoint", source);
        Assert.Contains("[\"client\"] = AdvertisedClientEndpoint", source);
        Assert.Contains("NodeDirectoryEndpoints = new[] { ClusterEndpoint }", source);
        Assert.Contains("new ClusterServiceOptions { Kind = \"node-directory\", Name = \"node-directory\" }", source);
        Assert.Contains("new ClusterServiceOptions { Kind = \"route-directory\", Name = \"route-directory\" }", source);
        Assert.Contains("new ClusterServiceOptions { Kind = \"gateway\", Name = \"gateway\" }", source);
    }

    [Fact]
    public void RenderClusterOptions_PreservesClusterCompatibilityOverrides()
    {
        var source = ToolTemplates.RenderClusterOptions();

        Assert.Contains("configuration.GetSection(\"Cluster\")", source);
        Assert.Contains("NodeId = ReadString(section, \"NodeId\", defaults.NodeId)", source);
        Assert.Contains("AdvertisedEndpoints = ReadDictionary(section.GetSection(\"AdvertisedEndpoints\"), defaults.AdvertisedEndpoints)", source);
        Assert.Contains("Bootstrap = ClusterBootstrapOptions.FromConfiguration(section.GetSection(\"Bootstrap\"), defaults.Bootstrap)", source);
        Assert.Contains("NodeDirectory = ClusterNodeDirectoryOptions.FromConfiguration(section.GetSection(\"NodeDirectory\"), defaults.NodeDirectory)", source);
        Assert.Contains("Services = ReadServices(section.GetSection(\"Services\"), defaults.Services)", source);
        Assert.Contains("RouteLeaseSeconds = ReadInt(section, \"RouteLeaseSeconds\", defaults.RouteLeaseSeconds)", source);
        Assert.Contains("SendTimeoutMilliseconds = ReadInt(section, \"SendTimeoutMilliseconds\", defaults.SendTimeoutMilliseconds)", source);
    }

    [Fact]
    public void RenderClusterOptions_IncludesULinkGameCheckOutputLabels()
    {
        var source = ToolTemplates.RenderClusterOptions();

        Assert.Contains("ULinkGameCheck", source);
        Assert.Contains("cluster:", source);
        Assert.Contains("node:", source);
        Assert.Contains("services:", source);
        Assert.Contains("hotfix:", source);
        Assert.Contains("reliable-push:", source);
        Assert.Contains("rpc:", source);
    }
}
