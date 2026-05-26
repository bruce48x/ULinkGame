using System.Globalization;
using Xunit;

namespace ULinkGame.Tool.Tests;

public sealed class ToolTextTests
{
    [Theory]
    [InlineData("zh-CN", "SimplifiedChinese")]
    [InlineData("zh-Hans", "SimplifiedChinese")]
    [InlineData("zh-TW", "TraditionalChinese")]
    [InlineData("zh-Hant", "TraditionalChinese")]
    [InlineData("zh-HK", "TraditionalChinese")]
    [InlineData("en-US", "English")]
    public void DetectLanguageMatchesStarterRules(string cultureName, string expected)
    {
        Assert.Equal(expected, ToolText.DetectLanguage(CultureInfo.GetCultureInfo(cultureName)).ToString());
    }

    [Fact]
    public void SimplifiedChineseTextLocalizesHelpAndNextSteps()
    {
        var text = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-CN"));

        Assert.Contains("命令:", text.HelpText, StringComparison.Ordinal);
        Assert.Equal("ULinkGame 项目已就绪。下一步:", text.NewProjectReadyHeader);
        Assert.Contains("修改 Shared 合约后", text.RebuildContractsStep, StringComparison.Ordinal);
    }

    [Fact]
    public void TraditionalChineseTextLocalizesHelpAndNextSteps()
    {
        var text = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-TW"));

        Assert.Contains("命令:", text.HelpText, StringComparison.Ordinal);
        Assert.Equal("ULinkGame 專案已就緒。下一步:", text.NewProjectReadyHeader);
        Assert.Contains("修改 Shared 合約後", text.RebuildContractsStep, StringComparison.Ordinal);
    }

    [Fact]
    public void ParserUsesLocalizedUnsupportedValueMessage()
    {
        var text = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-CN"));

        var exception = Assert.Throws<CliUsageException>(() =>
            CliParser.ParseNewOptions(["--transport", "websockt"], text));

        Assert.Contains("--transport 不支持值 'websockt'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("你是否想输入 'websocket'?", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParserAcceptsClusterNetworkProfile()
    {
        var options = CliParser.ParseNewOptions(["--network-profile", "cluster"]);

        Assert.Equal("cluster", options.NetworkProfile);
    }

    [Fact]
    public void ClusterNetworkProfileGeneratesExplicitClusterConfiguration()
    {
        var options = new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: "unity",
            Transport: "tcp",
            NetworkProfile: "cluster",
            Serializer: "json",
            Persistence: "none",
            NuGetForUnitySource: "embedded",
            DeployProfile: "compose");

        var appSettings = ToolTemplates.RenderEdgeAppSettings(options);
        var project = ToolTemplates.RenderEdgeProject(options);
        var program = ToolTemplates.RenderEdgeProgram(options);
        var clusterOptions = ToolTemplates.RenderClusterOptions();
        var clusterHealthCheck = ToolTemplates.RenderClusterHealthCheck();
        var compose = ToolTemplates.RenderClusterCompose(options);
        var env = ToolTemplates.RenderClusterEnvExample();
        var operations = ToolTemplates.RenderClusterOperationsGuide();

        Assert.Contains("\"Cluster\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"NodeId\": \"edge-1\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"InternalEndpoint\": \"tcp://127.0.0.1:21000\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster", project, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster.ULinkRPC", project, StringComparison.Ordinal);
        Assert.Contains("--health-check", program, StringComparison.Ordinal);
        Assert.Contains("ClusterOptions.FromConfiguration", program, StringComparison.Ordinal);
        Assert.Contains("RouteDirectoryEndpoint", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("cluster=healthy", clusterHealthCheck, StringComparison.Ordinal);
        Assert.Contains("healthcheck:", compose, StringComparison.Ordinal);
        Assert.Contains("dotnet Edge.dll --health-check", compose, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_NODE_ID", env, StringComparison.Ordinal);
        Assert.Contains("ULinkRpcClusterDependencyProbe", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("password", compose, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", compose, StringComparison.OrdinalIgnoreCase);
    }
}
