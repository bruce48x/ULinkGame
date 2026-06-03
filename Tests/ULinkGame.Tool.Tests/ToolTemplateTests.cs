using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            source.IndexOf("ULinkGameCheck.Run(runtimeOptions, runtimeOptions.ToClusterOptions(builder.Configuration), args)", StringComparison.Ordinal) >
            source.IndexOf("var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration)", StringComparison.Ordinal));
        Assert.True(
            source.IndexOf("ULinkGameCheck.Run(runtimeOptions, runtimeOptions.ToClusterOptions(builder.Configuration), args)", StringComparison.Ordinal) <
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
        Assert.Contains("Path = ReadString(section, \"Path\", GetDefaultPath(transport))", source);
        Assert.Contains("return string.Equals(transport, \"websocket\", StringComparison.OrdinalIgnoreCase)", source);
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
        Assert.Contains("using System.Text.Json;", source);
        Assert.Contains("using ULinkGame.Server.Guardrails;", source);
        Assert.Contains("using ULinkGame.Server.Guardrails.Rules;", source);
        Assert.Contains("ULinkGameValidationResult", source);
        Assert.Contains("--json", source);
        Assert.Contains("JsonSerializer.Serialize", source);
        Assert.Contains("\"succeeded\"", source);
        Assert.Contains("ULINK071", source);
        Assert.Contains("public static int Run(ULinkGameRuntimeOptions runtime, ClusterOptions clusterOptions, string[] args)", source);
        Assert.Contains("node: ok {clusterOptions.NodeId}", source);
        Assert.Contains("clusterOptions.AdvertisedEndpoints.TryGetValue(\"client\"", source);
        Assert.Contains("hotfix: failed local build output not found", source);
        Assert.Contains("fix: {hotfixFailure.Repair}", source);
        Assert.Contains("\"hotfix\"", source);
        Assert.Contains("new HotfixSourceRule()", source);
        Assert.Contains("new ULinkGameResolvedHotfix", source);
        Assert.DoesNotContain("Hotfix:Directory", source);
        Assert.DoesNotContain("Hotfix:Assembly", source);
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
        Assert.Contains("[RpcService(2, Callback = typeof(IChatCallback))]", sharedProtocols, StringComparison.Ordinal);
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
        Assert.Contains("AddULinkGameHotfix", program, StringComparison.Ordinal);
        Assert.Contains("CurrentDirectoryHotfixAssemblySource", program, StringComparison.Ordinal);
        Assert.Contains("IHotfixManager", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Agar.Sample.Hotfix", generatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AugmentExistingStarterServerProjectAddsHotfixCopyTarget()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "ulinkgame-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var serverDirectory = Path.Combine(projectRoot, "Server", "Server");
            Directory.CreateDirectory(serverDirectory);
            Directory.CreateDirectory(Path.Combine(projectRoot, "Shared"));
            await File.WriteAllTextAsync(
                Path.Combine(serverDirectory, "Server.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                TestContext.Current.CancellationToken);

            await new ProjectScaffolder().AugmentProjectWithULinkGameAsync(projectRoot, CliParser.ParseNewOptions([]));

            var project = await File.ReadAllTextAsync(
                Path.Combine(serverDirectory, "Server.csproj"),
                TestContext.Current.CancellationToken);

            Assert.Contains(@"<Target Name=""CopyHotfixOutput"" AfterTargets=""Build"">", project, StringComparison.Ordinal);
            Assert.Contains(@"DestinationFolder=""$(OutDir)hotfix\""", project, StringComparison.Ordinal);
            Assert.Contains("Server.Hotfix.dll", project, StringComparison.Ordinal);
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
    public void RenderSharedChatProtocols_DefinesRpcServiceAndCallback()
    {
        var source = ToolTemplates.RenderSharedChatProtocols();

        Assert.Contains("[RpcService(2", source, StringComparison.Ordinal);
        Assert.Contains("typeof(IChatCallback)", source, StringComparison.Ordinal);
        Assert.Contains("[RpcMethod(1)]", source, StringComparison.Ordinal);
        Assert.Contains("[RpcMethod(2)]", source, StringComparison.Ordinal);
        Assert.Contains("[RpcMethod(3)]", source, StringComparison.Ordinal);
        Assert.Contains("[RpcPush(1)]", source, StringComparison.Ordinal);
        Assert.Contains("OnMessageReceived", source, StringComparison.Ordinal);
        Assert.Contains("OnUserJoined", source, StringComparison.Ordinal);
        Assert.Contains("OnUserLeft", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderUnityFacingChatTemplates_ParseAsCSharpNine()
    {
        var sources = new[]
        {
            ("Shared/Chat/ChatProtocols.cs", ToolTemplates.RenderSharedChatProtocols()),
            ("Shared/Chat/ChatMessages.cs", ToolTemplates.RenderSharedChatMessages()),
            ("Client/Assets/Scripts/Chat/ChatClient.cs", ToolTemplates.RenderClientChatClient()),
            ("Client/Assets/Scripts/Chat/ChatUI.cs", ToolTemplates.RenderClientChatUI(CliParser.ParseNewOptions([])))
        };

        AssertGeneratedSourcesParseAsCSharp9(sources);
    }

    private static void AssertGeneratedSourcesParseAsCSharp9(IEnumerable<(string Path, string Source)> sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);
        var diagnostics = new List<string>();

        foreach (var (path, source) in sources)
        {
            var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path);
            diagnostics.AddRange(
                tree.GetDiagnostics()
                    .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => $"{path}: {diagnostic.Id} {diagnostic.GetMessage()}"));
        }

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void RenderServerChatRoom_UsesConcurrentDictionaryAndBroadcast()
    {
        var source = ToolTemplates.RenderServerChatRoom();

        Assert.Contains("ConcurrentDictionary", source, StringComparison.Ordinal);
        Assert.Contains("MaxRecentMessages = 100", source, StringComparison.Ordinal);
        Assert.Contains("Broadcast(cb => cb.OnUserJoined", source, StringComparison.Ordinal);
        Assert.Contains("Broadcast(cb => cb.OnMessageReceived", source, StringComparison.Ordinal);
        Assert.Contains("Broadcast(cb => cb.OnUserLeft", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderServerChatServiceImpl_WrapsChatRoom()
    {
        var source = ToolTemplates.RenderServerChatServiceImpl();

        Assert.Contains("class ChatServiceImpl : IChatService", source, StringComparison.Ordinal);
        Assert.Contains("private static readonly ChatRoom SharedRoom = new();", source, StringComparison.Ordinal);
        Assert.Contains("public ChatServiceImpl(IChatCallback callback)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public ChatServiceImpl(IChatCallback callback, ChatRoom room)", source, StringComparison.Ordinal);
        Assert.Contains("_room.Join", source, StringComparison.Ordinal);
        Assert.Contains("_room.Send", source, StringComparison.Ordinal);
        Assert.Contains("_room.Leave", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderClientChatClient_ImplementsIChatCallback()
    {
        var source = ToolTemplates.RenderClientChatClient();

        Assert.Contains("class ChatClient : IChatCallback", source, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", source, StringComparison.Ordinal);
        Assert.Contains("using Rpc.Generated;", source, StringComparison.Ordinal);
        Assert.Contains("new RpcClient(options, callbacks)", source, StringComparison.Ordinal);
        Assert.Contains("_rpcClient.Api.Shared.Chat", source, StringComparison.Ordinal);
        Assert.Contains("OnMessageReceived?.Invoke", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderClientChatUi_RequiresUiDocument()
    {
        var source = ToolTemplates.RenderClientChatUI(CliParser.ParseNewOptions([]));

        Assert.Contains("RequireComponent(typeof(UIDocument))", source, StringComparison.Ordinal);
        Assert.Contains("new KcpTransport(_serverHost, _serverPort)", source, StringComparison.Ordinal);
        Assert.Contains("new MemoryPackRpcSerializer()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("?.clicked +=", source, StringComparison.Ordinal);
        Assert.Contains("chat-input", source, StringComparison.Ordinal);
        Assert.Contains("message-list", source, StringComparison.Ordinal);
        Assert.Contains("send-button", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderClientChatUi_UsesSelectedTransportAndSerializer()
    {
        var source = ToolTemplates.RenderClientChatUI(new NewCommandOptions(
            Name: "MyGame",
            OutputPath: null,
            ClientEngine: "unity",
            Transport: "websocket",
            NetworkProfile: "cluster",
            Serializer: "json",
            Persistence: "none",
            NuGetForUnitySource: "embedded",
            DeployProfile: "none"));

        Assert.Contains("using ULinkRPC.Transport.WebSocket;", source, StringComparison.Ordinal);
        Assert.Contains("using ULinkRPC.Serializer.Json;", source, StringComparison.Ordinal);
        Assert.Contains("new WsTransport($\"ws://{_serverHost}:{_serverPort}{NormalizePath(_serverPath)}\")", source, StringComparison.Ordinal);
        Assert.Contains("new JsonRpcSerializer()", source, StringComparison.Ordinal);
        Assert.Contains("[SerializeField] private string _serverPath = \"/ws\";", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderUnityChatSceneInstaller_WiresUiDocumentAndChatUi()
    {
        var source = ToolTemplates.RenderUnityChatSceneInstaller();

        Assert.Contains("Assets/Scenes/ConnectionTest.unity", source, StringComparison.Ordinal);
        Assert.Contains("Assets/UI/ChatScene.uxml", source, StringComparison.Ordinal);
        Assert.Contains("Assets/UI/ULinkGameChatPanelSettings.asset", source, StringComparison.Ordinal);
        Assert.Contains("AddComponent<UIDocument>()", source, StringComparison.Ordinal);
        Assert.Contains("AddComponent<ChatUI>()", source, StringComparison.Ordinal);
        Assert.Contains("document.visualTreeAsset = visualTree", source, StringComparison.Ordinal);
        Assert.Contains("document.panelSettings = panelSettings", source, StringComparison.Ordinal);
        Assert.Contains("EditorSceneManager.SaveScene(scene)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderClientChatUxml_UsesUiNamespacePrefix()
    {
        var source = ToolTemplates.RenderClientChatUxml();

        Assert.Contains("<ui:UXML", source, StringComparison.Ordinal);
        Assert.Contains("name=\"chat-input\"", source, StringComparison.Ordinal);
        Assert.Contains("name=\"message-list\"", source, StringComparison.Ordinal);
    }
}
