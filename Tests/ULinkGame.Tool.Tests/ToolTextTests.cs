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
        Assert.Contains("正在自动安装", text.InstallingStarter("ULinkRPC.Starter", ToolPackageVersions.ULinkRpcStarter), StringComparison.Ordinal);
    }

    [Fact]
    public void PinsCurrentStarterPackageVersion()
    {
        Assert.Equal("0.3.4", ToolPackageVersions.ULinkRpcStarter);
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
    public void NewProjectReadyText_PointsToULinkGameCheck()
    {
        var english = ToolText.ForCulture(CultureInfo.GetCultureInfo("en-US"));
        var simplifiedChinese = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-CN"));

        Assert.Contains("--ulinkgame-check", english.CheckProjectStep, StringComparison.Ordinal);
        Assert.Contains("--ulinkgame-check", simplifiedChinese.CheckProjectStep, StringComparison.Ordinal);
        Assert.StartsWith("  2)", english.CheckProjectStep, StringComparison.Ordinal);
        Assert.StartsWith("  3)", english.StartServerStep, StringComparison.Ordinal);
        Assert.StartsWith("  4)", english.RebuildContractsStep, StringComparison.Ordinal);
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
    public void ParserDefaultsToClusterNetworkProfile()
    {
        var options = CliParser.ParseNewOptions([]);

        Assert.Equal("cluster", options.NetworkProfile);
    }

    [Fact]
    public void ParserAcceptsClusterNetworkProfileAsCompatibilityNoOp()
    {
        var options = CliParser.ParseNewOptions(["--network-profile", "cluster"]);

        Assert.Equal("cluster", options.NetworkProfile);
    }

    [Fact]
    public void ParserRejectsNonClusterNetworkProfile()
    {
        var exception = Assert.Throws<CliUsageException>(() =>
            CliParser.ParseNewOptions(["--network-profile", "simple"]));

        Assert.Contains("cluster", exception.Message, StringComparison.Ordinal);
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

        var appSettings = ToolTemplates.RenderServerAppSettings(options);
        var project = ToolTemplates.RenderServerProject(options);
        var program = ToolTemplates.RenderServerProgram(options);
        var clusterOptions = ToolTemplates.RenderClusterOptions();
        var clusterHealthCheck = ToolTemplates.RenderClusterHealthCheck();
        var compose = ToolTemplates.RenderClusterCompose(options);
        var env = ToolTemplates.RenderClusterEnvExample(options);
        var operations = ToolTemplates.RenderClusterOperationsGuide();

        Assert.Contains("\"ULinkGame\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Node\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Id\": \"dev-1\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Endpoint\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Transport\": \"tcp\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Host\": \"127.0.0.1\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Port\": 20000", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Cluster\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AdvertisedEndpoints\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Bootstrap\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NodeDirectory\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Services\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster", project, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster.ULinkRPC", project, StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>Server</RootNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("<ULinkRPCServerGeneratedNamespace>Server.Generated</ULinkRPCServerGeneratedNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("--health-check", program, StringComparison.Ordinal);
        Assert.Contains("ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration)", program, StringComparison.Ordinal);
        Assert.Contains("runtimeOptions.ToClusterOptions(builder.Configuration)", program, StringComparison.Ordinal);
        Assert.Contains("using Server.Hosting;", program, StringComparison.Ordinal);
        Assert.Contains("ULinkGameRuntimeOptions", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("ToClusterOptions()", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("AdvertisedEndpoints", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("[\"cluster\"] = \"tcp://127.0.0.1:21000\"", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("[\"client\"] = AdvertisedClientEndpoint", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("Services", clusterOptions, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeEpoch", clusterHealthCheck, StringComparison.Ordinal);
        Assert.Contains("cluster=healthy", clusterHealthCheck, StringComparison.Ordinal);
        Assert.Contains("healthcheck:", compose, StringComparison.Ordinal);
        Assert.Contains("dotnet Server.dll --health-check", compose, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_NODE_ID", env, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLUSTER", env, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT", env, StringComparison.Ordinal);
        Assert.Contains("Cluster__AdvertisedEndpoints__client", compose, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoint__Transport", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoint__Host", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoint__Port", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoint__Path", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Transport:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Host:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Port:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Path:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("ULinkRpcClusterDependencyProbe", operations, StringComparison.Ordinal);
        Assert.Contains("Cluster__AdvertisedEndpoints__client", operations, StringComparison.Ordinal);
        var generatedText = string.Concat(appSettings, project, program, clusterOptions, clusterHealthCheck, compose, env, operations);
        Assert.DoesNotContain("NodeEpoch", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalEndpoint", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteDirectoryEndpoint", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-rpc", generatedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public-ws", generatedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gateway.csproj", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway.Generated", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway.Hosting", generatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("password", compose, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", compose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultScaffoldIncludesServerHotfixInfrastructure()
    {
        var options = CliParser.ParseNewOptions([]);

        var solution = ToolTemplates.RenderServerSolution();
        var project = ToolTemplates.RenderServerProject(options);
        var sharedProject = ToolTemplates.RenderSharedProjectHotfixItemGroup();
        var sharedAssemblyInfo = ToolTemplates.RenderSharedHotfixAssemblyInfo();
        var sharedGameRules = ToolTemplates.RenderSharedGameRules();
        var hotfixProject = ToolTemplates.RenderHotfixProject();
        var hotfixGameRules = ToolTemplates.RenderHotfixGameRulesSystem();
        var appSettings = ToolTemplates.RenderServerAppSettings(options);
        var program = ToolTemplates.RenderServerProgram(options);
        var generatedText = string.Concat(
            solution,
            project,
            sharedProject,
            sharedAssemblyInfo,
            sharedGameRules,
            hotfixProject,
            hotfixGameRules,
            appSettings,
            program);

        Assert.Contains(@"<Project Path=""Hotfix/Server.Hotfix.csproj"" />", solution, StringComparison.Ordinal);
        Assert.Contains(@"<ProjectReference Include=""..\Hotfix\Server.Hotfix.csproj"" ReferenceOutputAssembly=""false"" />", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix""", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Generators""", project, StringComparison.Ordinal);
        Assert.Contains(@"PrivateAssets=""all"" OutputItemType=""Analyzer""", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Abstractions""", sharedProject, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Generators""", sharedProject, StringComparison.Ordinal);
        Assert.Contains(@"InternalsVisibleTo(""Server.Hotfix"")", sharedAssemblyInfo, StringComparison.Ordinal);
        Assert.Contains("namespace Shared.Gameplay\r\n{", sharedGameRules.Replace("\n", "\r\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Shared.Gameplay;", sharedGameRules, StringComparison.Ordinal);
        Assert.Contains("[HotfixState]", sharedGameRules, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class GameRulesState", sharedGameRules, StringComparison.Ordinal);
        Assert.Contains("HotfixDispatch.Invoke<GameRulesState, GameRuleInput, GameRuleResult>", sharedGameRules, StringComparison.Ordinal);
        Assert.Contains("EvaluateStable", sharedGameRules, StringComparison.Ordinal);
        Assert.Contains(@"ProjectReference Include=""..\..\Shared\Shared.csproj""", hotfixProject, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Abstractions""", hotfixProject, StringComparison.Ordinal);
        Assert.Contains("[FriendOf(typeof(GameRulesState))]", hotfixGameRules, StringComparison.Ordinal);
        Assert.Contains("[HotfixSystemOf(typeof(GameRulesState))]", hotfixGameRules, StringComparison.Ordinal);
        Assert.Contains("public static GameRuleResult Evaluate(this GameRulesState self, GameRuleInput input)", hotfixGameRules, StringComparison.Ordinal);
        Assert.DoesNotContain("[HotfixState]", hotfixGameRules, StringComparison.Ordinal);
        Assert.Contains("AddULinkGameHotfix", program, StringComparison.Ordinal);
        Assert.Contains("CurrentDirectoryHotfixAssemblySource", program, StringComparison.Ordinal);
        Assert.Contains("IHotfixManager", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Agar.Sample.Hotfix", generatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnityClientScaffoldPinsClientDependenciesAndAnalyzerImportGuard()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "ulinkgame-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "Server", "Server"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "Shared"));

            await new ProjectScaffolder().AugmentProjectWithULinkGameAsync(projectRoot, CliParser.ParseNewOptions([]));

            var packagesConfig = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Assets", "packages.config"),
                TestContext.Current.CancellationToken);
            var importGuard = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Assets", "Editor", "ULinkGameNuGetPackageImportGuard.cs"),
                TestContext.Current.CancellationToken);

            Assert.Contains("id=\"ULinkGame.Client\"", packagesConfig, StringComparison.Ordinal);
            Assert.Contains("id=\"ULinkGame.Abstractions\"", packagesConfig, StringComparison.Ordinal);
            Assert.Contains("AssetPostprocessor", importGuard, StringComparison.Ordinal);
            Assert.Contains("Assets/Packages/", importGuard, StringComparison.Ordinal);
            Assert.Contains("/analyzers/", importGuard, StringComparison.Ordinal);
            Assert.Contains("SetCompatibleWithAnyPlatform(false)", importGuard, StringComparison.Ordinal);
            Assert.Contains("SetCompatibleWithEditor(false)", importGuard, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ClusterEnvExampleUsesSelectedTransportForAdvertisedClientEndpoint()
    {
        var websocketOptions = new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: "unity",
            Transport: "websocket",
            NetworkProfile: "cluster",
            Serializer: "json",
            Persistence: "none",
            NuGetForUnitySource: "embedded",
            DeployProfile: "compose");
        var defaultOptions = CliParser.ParseNewOptions([]);

        var websocketEnv = ToolTemplates.RenderClusterEnvExample(websocketOptions);
        var defaultEnv = ToolTemplates.RenderClusterEnvExample(defaultOptions);

        Assert.Contains("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT=ws://gateway:20000/ws", websocketEnv, StringComparison.Ordinal);
        Assert.DoesNotContain("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT=tcp://gateway:20000", websocketEnv, StringComparison.Ordinal);
        Assert.Contains("ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT=kcp://gateway:20000", defaultEnv, StringComparison.Ordinal);
    }
}
