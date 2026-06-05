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
        Assert.Contains("正在自动安装", text.InstallingStarter("ULinkRPC.Starter", ToolPackageVersions.ULinkRpcStarter), StringComparison.Ordinal);
    }

    [Fact]
    public void PinsCurrentStarterPackageVersion()
    {
        Assert.Equal("0.4.1", ToolPackageVersions.ULinkRpcStarter);
    }

    [Fact]
    public void TraditionalChineseTextLocalizesHelpAndNextSteps()
    {
        var text = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-TW"));

        Assert.Contains("命令:", text.HelpText, StringComparison.Ordinal);
        Assert.Equal("ULinkGame 專案已就緒。下一步:", text.NewProjectReadyHeader);
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
    }

    [Fact]
    public void NewProjectReadyOutput_DoesNotPrintFourthStep()
    {
        var text = ToolText.ForCulture(CultureInfo.GetCultureInfo("zh-CN"));
        var app = new CliApplication(new ToolProcessRunner(text), new ProjectScaffolder(), new ToolConfigStore(), text);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            typeof(CliApplication)
                .GetMethod("PrintNewProjectNextSteps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(app, ["D:\\ULinkGame-Sample-Unity24"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("ULinkGame 项目已就绪。下一步:", output, StringComparison.Ordinal);
        Assert.Contains("  1) cd \"D:\\ULinkGame-Sample-Unity24\"", output, StringComparison.Ordinal);
        Assert.Contains("  2) dotnet run --project \"Server/Server/Server.csproj\" -- --ulinkgame-check", output, StringComparison.Ordinal);
        Assert.Contains("  3) dotnet run --project \"Server/Server/Server.csproj\"", output, StringComparison.Ordinal);
        Assert.DoesNotContain("  4)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("修改 Shared 合约后", output, StringComparison.Ordinal);
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
        var generatedApplication = ToolTemplates.RenderGeneratedServerApplication(options);
        var clusterOptions = ToolTemplates.RenderClusterOptions();
        var clusterHealthCheck = ToolTemplates.RenderClusterHealthCheck();
        var compose = ToolTemplates.RenderClusterCompose(options);
        var env = ToolTemplates.RenderClusterEnvExample(options);
        var operations = ToolTemplates.RenderClusterOperationsGuide();

        Assert.Contains("\"ULinkGame\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Node\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Id\": \"dev-1\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Endpoints\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Transport\": \"tcp\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Host\": \"127.0.0.1\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Port\": 20000", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Endpoint\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Cluster\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AdvertisedEndpoints\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Bootstrap\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NodeDirectory\"", appSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Services\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster", project, StringComparison.Ordinal);
        Assert.Contains("ULinkGame.Cluster.ULinkRPC", project, StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>Server</RootNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("<ULinkRPCServerGeneratedNamespace>Server.Generated</ULinkRPCServerGeneratedNamespace>", project, StringComparison.Ordinal);
        Assert.Contains("return await ULinkGameGeneratedApplication.RunAsync(args);", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ULinkGameRuntimeOptions", program, StringComparison.Ordinal);
        Assert.Contains("--health-check", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration)", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("runtimeOptions.ToClusterOptions(builder.Configuration, \"tcp\")", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("using Server.Hosting;", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("ULinkGameRuntimeOptions", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("ToClusterOptions(string transport)", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("AdvertisedEndpoints", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("[\"cluster\"] = \"tcp://127.0.0.1:21000\"", clusterOptions, StringComparison.Ordinal);
        Assert.Contains("[\"client\"] = clientEndpoint", clusterOptions, StringComparison.Ordinal);
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
        Assert.Contains("ULinkGame__Endpoints__0__Transport", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoints__0__Host", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoints__0__Port", compose, StringComparison.Ordinal);
        Assert.Contains("ULinkGame__Endpoints__0__Path", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Transport:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Host:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Port:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("\n              Endpoint__Path:", compose.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("ULinkRpcClusterDependencyProbe", operations, StringComparison.Ordinal);
        Assert.Contains("Cluster__AdvertisedEndpoints__client", operations, StringComparison.Ordinal);
        var generatedText = string.Concat(appSettings, project, program, generatedApplication, clusterOptions, clusterHealthCheck, compose, env, operations);
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
        var sharedProtocols = ToolTemplates.RenderSharedChatProtocols();
        var sharedMessages = ToolTemplates.RenderSharedChatMessages();
        var hotfixProject = ToolTemplates.RenderHotfixProject();
        var hotfixChatSystem = ToolTemplates.RenderHotfixChatSystem();
        var appSettings = ToolTemplates.RenderServerAppSettings(options);
        var program = ToolTemplates.RenderServerProgram(options);
        var generatedApplication = ToolTemplates.RenderGeneratedServerApplication(options);
        var chatRoom = ToolTemplates.RenderServerChatRoom();
        var chatServiceImpl = ToolTemplates.RenderServerChatServiceImpl();
        var generatedText = string.Concat(
            solution,
            project,
            sharedProject,
            sharedAssemblyInfo,
            sharedProtocols,
            sharedMessages,
            hotfixProject,
            hotfixChatSystem,
            appSettings,
            program,
            generatedApplication,
            chatRoom,
            chatServiceImpl);

        Assert.Contains(@"<Project Path=""Hotfix/Server.Hotfix.csproj"" />", solution, StringComparison.Ordinal);
        Assert.Contains(@"<ProjectReference Include=""..\Hotfix\Server.Hotfix.csproj"" ReferenceOutputAssembly=""false"" />", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix""", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Generators""", project, StringComparison.Ordinal);
        Assert.Contains(@"PrivateAssets=""all"" OutputItemType=""Analyzer""", project, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Abstractions""", sharedProject, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Generators""", sharedProject, StringComparison.Ordinal);
        Assert.Contains(@"InternalsVisibleTo(""Server.Hotfix"")", sharedAssemblyInfo, StringComparison.Ordinal);
        Assert.Contains("[RpcService(2, NotificationContract = typeof(IChatCallback))]", sharedProtocols, StringComparison.Ordinal);
        Assert.Contains("interface IChatService", sharedProtocols, StringComparison.Ordinal);
        Assert.Contains("interface IChatCallback", sharedProtocols, StringComparison.Ordinal);
        Assert.Contains("ChatJoinRequest", sharedMessages, StringComparison.Ordinal);
        Assert.Contains("ChatMessage", sharedMessages, StringComparison.Ordinal);
        Assert.Contains(@"ProjectReference Include=""..\..\Shared\Shared.csproj""", hotfixProject, StringComparison.Ordinal);
        Assert.Contains(@"PackageReference Include=""ULinkGame.Server.Hotfix.Abstractions""", hotfixProject, StringComparison.Ordinal);
        Assert.Contains("class ChatSystem", hotfixChatSystem, StringComparison.Ordinal);
        Assert.Contains("SanitizeMessage", hotfixChatSystem, StringComparison.Ordinal);
        Assert.Contains("ConcurrentDictionary", chatRoom, StringComparison.Ordinal);
        Assert.Contains("class ChatRoom", chatRoom, StringComparison.Ordinal);
        Assert.Contains("class ChatServiceImpl", chatServiceImpl, StringComparison.Ordinal);
        Assert.Contains("IChatService", chatServiceImpl, StringComparison.Ordinal);
        Assert.DoesNotContain("AddULinkGameHotfix", program, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentDirectoryHotfixAssemblySource", program, StringComparison.Ordinal);
        Assert.DoesNotContain("IHotfixManager", program, StringComparison.Ordinal);
        Assert.Contains("AddULinkGameHotfix", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("CurrentDirectoryHotfixAssemblySource", generatedApplication, StringComparison.Ordinal);
        Assert.Contains("IHotfixManager", generatedApplication, StringComparison.Ordinal);
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
            Directory.CreateDirectory(Path.Combine(projectRoot, "Client", "Assets", "Scenes"));
            var scenePath = Path.Combine(projectRoot, "Client", "Assets", "Scenes", "ConnectionTest.unity");
            await File.WriteAllTextAsync(
                scenePath,
                """
                %YAML 1.1
                %TAG !u! tag:unity3d.com,2011:
                --- !u!1 &1
                GameObject:
                  m_Component:
                  - component: {fileID: 2}
                  m_Name: Main Camera
                --- !u!4 &2
                Transform:
                  m_GameObject: {fileID: 1}
                  m_Father: {fileID: 0}
                --- !u!1660057539 &9223372036854775807
                SceneRoots:
                  m_ObjectHideFlags: 0
                  m_Roots:
                  - {fileID: 2}
                """,
                TestContext.Current.CancellationToken);

            await new ProjectScaffolder().AugmentProjectWithULinkGameAsync(projectRoot, CliParser.ParseNewOptions([]));

            var packagesConfig = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Assets", "packages.config"),
                TestContext.Current.CancellationToken);
            var importGuard = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Assets", "Editor", "ULinkGameNuGetPackageImportGuard.cs"),
                TestContext.Current.CancellationToken);
            var scene = await File.ReadAllTextAsync(scenePath, TestContext.Current.CancellationToken);

            Assert.Contains("id=\"ULinkGame.Client\"", packagesConfig, StringComparison.Ordinal);
            Assert.Contains("id=\"ULinkGame.Abstractions\"", packagesConfig, StringComparison.Ordinal);
            Assert.Contains("AssetPostprocessor", importGuard, StringComparison.Ordinal);
            Assert.Contains("Assets/Packages/", importGuard, StringComparison.Ordinal);
            Assert.Contains("/analyzers/", importGuard, StringComparison.Ordinal);
            Assert.Contains("SetCompatibleWithAnyPlatform(false)", importGuard, StringComparison.Ordinal);
            Assert.Contains("SetCompatibleWithEditor(false)", importGuard, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "Editor", "ULinkGameChatSceneInstaller.cs")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "Scripts", "Chat", "ChatUI.cs.meta")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "UI", "ChatScene.uxml.meta")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "UI", "ULinkGameChatPanelSettings.asset")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "UI", "ULinkGameChatPanelSettings.asset.meta")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "UI Toolkit", "UnityThemes", "UnityDefaultRuntimeTheme.tss")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "Client", "Assets", "UI Toolkit", "UnityThemes", "UnityDefaultRuntimeTheme.tss.meta")));
            Assert.Contains("m_Name: ULinkGame Chat UI", scene, StringComparison.Ordinal);
            Assert.Contains("guid: 462a8730535800d4a801000623f4450e, type: 3", scene, StringComparison.Ordinal);
            Assert.Contains("guid: d8e055cb54604094cb41badb6b3866f6, type: 3", scene, StringComparison.Ordinal);
            Assert.Contains("m_PanelSettings: {fileID: 11400000, guid: 0c8089bab5856fe4d8f88e6f526fd306, type: 2}", scene, StringComparison.Ordinal);
            Assert.Contains("_serverPath:", scene, StringComparison.Ordinal);
            Assert.DoesNotContain("_serverPath: /ws", scene, StringComparison.Ordinal);
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
    public async Task JsonSerializerScaffoldDoesNotEmitMemoryPackChatContracts()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "ulinkgame-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "Server", "Server"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "Shared"));

            var options = new NewCommandOptions(
                Name: "MyGame",
                OutputPath: null,
                ClientEngine: "unity",
                Transport: "websocket",
                NetworkProfile: "single",
                Serializer: "json",
                Persistence: "none",
                NuGetForUnitySource: "embedded",
                DeployProfile: "none");

            await new ProjectScaffolder().AugmentProjectWithULinkGameAsync(projectRoot, options);

            var chatMessages = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Shared", "Chat", "ChatMessages.cs"),
                TestContext.Current.CancellationToken);

            Assert.DoesNotContain("MemoryPack", chatMessages, StringComparison.Ordinal);
            Assert.DoesNotContain("MemoryPackable", chatMessages, StringComparison.Ordinal);
            Assert.DoesNotContain("MemoryPackOrder", chatMessages, StringComparison.Ordinal);
            Assert.Contains("public partial class ChatJoinRequest", chatMessages, StringComparison.Ordinal);
            Assert.Contains("public string PlayerName { get; set; }", chatMessages, StringComparison.Ordinal);
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
    public async Task GodotScaffoldInstallsDistributedChatScene()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "ulinkgame-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "Client", "Scripts", "Rpc", "Testing"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "Server", "Server"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "Shared"));
            await File.WriteAllTextAsync(
                Path.Combine(projectRoot, "Client", "Client.csproj"),
                """
                <Project Sdk="Godot.NET.Sdk/4.6.1">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(projectRoot, "Client", "project.godot"),
                """
                ; Engine configuration file.
                config_version=5

                [application]
                config/name="MyGame"
                run/main_scene="res://Main.tscn"
                config/features=PackedStringArray("4.6", "C#")
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(projectRoot, "Client", "Main.tscn"),
                """
                [gd_scene load_steps=2 format=3]

                [ext_resource type="Script" path="res://Scripts/Rpc/Testing/RpcConnectionTester.cs" id="1"]

                [node name="Main" type="Node"]
                script = ExtResource("1")
                """,
                TestContext.Current.CancellationToken);

            var options = new NewCommandOptions(
                Name: "MyGame",
                OutputPath: null,
                ClientEngine: "godot",
                Transport: "websocket",
                NetworkProfile: "single",
                Serializer: "json",
                Persistence: "none",
                NuGetForUnitySource: "embedded",
                DeployProfile: "none");

            await new ProjectScaffolder().AugmentProjectWithULinkGameAsync(projectRoot, options);

            var chatSceneScript = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Scripts", "Chat", "ChatScene.cs"),
                TestContext.Current.CancellationToken);
            var mainScene = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "Main.tscn"),
                TestContext.Current.CancellationToken);
            var projectGodot = await File.ReadAllTextAsync(
                Path.Combine(projectRoot, "Client", "project.godot"),
                TestContext.Current.CancellationToken);

            Assert.Contains("public partial class ChatScene : Control", chatSceneScript, StringComparison.Ordinal);
            Assert.Contains("new WsTransport($\"ws://{_serverHost}:{_serverPort}{NormalizePath(_serverPath)}\")", chatSceneScript, StringComparison.Ordinal);
            Assert.Contains("new JsonRpcSerializer()", chatSceneScript, StringComparison.Ordinal);
            Assert.Contains("CallDeferred(nameof(AppendMessageDeferred), msg.SenderName, msg.Text);", chatSceneScript, StringComparison.Ordinal);
            Assert.Contains("[ext_resource type=\"Script\" path=\"res://Scripts/Chat/ChatScene.cs\" id=\"1\"]", mainScene, StringComparison.Ordinal);
            Assert.Contains("[node name=\"ChatScene\" type=\"Control\"]", mainScene, StringComparison.Ordinal);
            Assert.Contains("script = ExtResource(\"1\")", mainScene, StringComparison.Ordinal);
            Assert.Contains("run/main_scene=\"res://Main.tscn\"", projectGodot, StringComparison.Ordinal);
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
