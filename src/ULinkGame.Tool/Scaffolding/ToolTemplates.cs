internal static class ToolTemplates
{
    public static string RenderServerSolution()
    {
        return """
        <Solution>
          <Project Path="../Shared/Shared.csproj" />
          <Project Path="Hotfix/Server.Hotfix.csproj" />
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
            using ULinkGame.Server.Hotfix;
            using ULinkGame.Server.Hotfix.Loading;
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
            {{RenderHotfixServiceRegistration()}}
            builder.Services.AddULinkGameServerGateway();

            var host = builder.Build();
            await LoadInitialHotfixAsync(host);
            await host.RunAsync();
            return 0;
            {{RenderHotfixHelpers()}}
            """;
        }

        return $$"""
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Server.Hosting;
        using ULinkGame.Server;
        using ULinkGame.Server.Hotfix;
        using ULinkGame.Server.Hotfix.Loading;
        using ULinkGame.Server.Hosting;

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(builder.Configuration);
        {{RenderULinkGameCheckExit(options)}}
        {{RenderClusterHealthCheckExit(options)}}

        builder.Services.AddULinkGameServer();
        builder.Services.AddSingleton(runtimeOptions);
        {{RenderClusterServiceRegistration(options)}}
        builder.Services.AddSingleton(runtimeOptions.ToServerRpcServerOptions());
        builder.Services.AddULinkRpcServer<DefaultRpcServerConfigurator>();
        {{RenderHotfixServiceRegistration()}}
        builder.Services.AddULinkGameServerGateway();

        var host = builder.Build();
        await LoadInitialHotfixAsync(host);
        await host.RunAsync();
        return 0;
        {{RenderHotfixHelpers()}}
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
            <ProjectReference Include="..\Hotfix\Server.Hotfix.csproj" ReferenceOutputAssembly="false" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.Extensions.Hosting" Version="{{ToolPackageVersions.MicrosoftExtensionsHosting}}" />
            <PackageReference Include="ULinkGame.Server" Version="{{ToolPackageVersions.ULinkGameServer}}" />
            <PackageReference Include="ULinkGame.Server.Generators" Version="{{ToolPackageVersions.ULinkGameServerGenerators}}" PrivateAssets="all" OutputItemType="Analyzer" />
            <PackageReference Include="ULinkGame.Server.Hotfix" Version="{{ToolPackageVersions.ULinkGameServerHotfix}}" />
        {{clusterReferences}}
        {{persistenceReferences}}
          </ItemGroup>

          <ItemGroup>
            <None Update="appsettings.json">
              <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            </None>
          </ItemGroup>

          <Target Name="CopyHotfixOutput" AfterTargets="Build">
            <Copy
              SourceFiles="$(ProjectDir)..\Hotfix\bin\$(Configuration)\$(TargetFramework)\Server.Hotfix.dll"
              DestinationFolder="$(OutDir)hotfix\"
              Condition="Exists('$(ProjectDir)..\Hotfix\bin\$(Configuration)\$(TargetFramework)\Server.Hotfix.dll')" />
          </Target>
        </Project>
        """;
    }

    public static string RenderServerAppSettings(NewCommandOptions options)
    {
        var pathLine = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase)
            ? "," + Environment.NewLine + "          \"Path\": \"/ws\""
            : string.Empty;

        return $$"""
        {
          "ULinkGame": {
            "Node": {
              "Id": "dev-1"
            },
            "Endpoint": {
              "Transport": "{{TemplateText.SanitizeStringLiteral(options.Transport)}}",
              "Host": "127.0.0.1",
              "Port": 20000{{pathLine}}
            }
          }
        }
        """;
    }

    public static string RenderHotfixProject()
    {
        return $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <AssemblyName>Server.Hotfix</AssemblyName>
            <RootNamespace>Server.Hotfix</RootNamespace>
            <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="..\..\Shared\Shared.csproj" TargetFramework="net10.0">
              <SetTargetFramework>TargetFramework=net10.0</SetTargetFramework>
            </ProjectReference>
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Server.Hotfix.Abstractions" Version="{{ToolPackageVersions.ULinkGameServerHotfixAbstractions}}" />
          </ItemGroup>
        </Project>
        """;
    }

    public static string RenderSharedProjectHotfixItemGroup()
    {
        return $$"""
        <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
          <PackageReference Include="ULinkGame.Server.Hotfix.Abstractions" Version="{{ToolPackageVersions.ULinkGameServerHotfixAbstractions}}" />
          <PackageReference Include="ULinkGame.Server.Hotfix" Version="{{ToolPackageVersions.ULinkGameServerHotfix}}" />
          <PackageReference Include="ULinkGame.Server.Hotfix.Generators" Version="{{ToolPackageVersions.ULinkGameServerHotfixGenerators}}" PrivateAssets="all" />
        </ItemGroup>
        """;
    }

    public static string RenderSharedHotfixAssemblyInfo()
    {
        return """
        using System.Runtime.CompilerServices;

        [assembly: InternalsVisibleTo("Server.Hotfix")]
        """;
    }

    public static string RenderSharedChatProtocols()
    {
        return """
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace Shared.Chat
        {
            [RpcService(2, Callback = typeof(IChatCallback))]
            public interface IChatService
            {
                [RpcMethod(1)] ValueTask<ChatJoinReply> JoinAsync(ChatJoinRequest req);
                [RpcMethod(2)] ValueTask SendAsync(ChatSendRequest req);
                [RpcMethod(3)] ValueTask LeaveAsync(ChatLeaveRequest req);
            }

            [RpcCallback(typeof(IChatService))]
            public interface IChatCallback
            {
                [RpcPush(1)] void OnMessageReceived(ChatMessage msg);
                [RpcPush(2)] void OnUserJoined(ChatMember member);
                [RpcPush(3)] void OnUserLeft(ChatUserLeft evt);
            }
        }
        """;
    }

    public static string RenderSharedChatMessages()
    {
        return RenderSharedChatMessages(CliParser.ParseNewOptions([]));
    }

    public static string RenderSharedChatMessages(NewCommandOptions options)
    {
        var memoryPackUsing = string.Equals(options.Serializer, "memorypack", StringComparison.Ordinal)
            ? "using MemoryPack;\n"
            : "";
        var memoryPackable = string.Equals(options.Serializer, "memorypack", StringComparison.Ordinal)
            ? "[MemoryPackable(GenerateType.VersionTolerant)]\n    "
            : "";
        var order0 = string.Equals(options.Serializer, "memorypack", StringComparison.Ordinal) ? "[MemoryPackOrder(0)] " : "";
        var order1 = string.Equals(options.Serializer, "memorypack", StringComparison.Ordinal) ? "[MemoryPackOrder(1)] " : "";
        var order2 = string.Equals(options.Serializer, "memorypack", StringComparison.Ordinal) ? "[MemoryPackOrder(2)] " : "";

        return $$"""
        using System.Collections.Generic;
        {{memoryPackUsing}}

        namespace Shared.Chat
        {
            {{memoryPackable}}public partial class ChatJoinRequest
            {
                {{order0}}public string PlayerName { get; set; } = "";
            }

            {{memoryPackable}}public partial class ChatJoinReply
            {
                {{order0}}public List<ChatMember> Members { get; set; } = new();
                {{order1}}public List<ChatMessage> RecentMessages { get; set; } = new();
            }

            {{memoryPackable}}public partial class ChatSendRequest
            {
                {{order0}}public string Text { get; set; } = "";
            }

            {{memoryPackable}}public partial class ChatLeaveRequest
            {
            }

            {{memoryPackable}}public partial class ChatUserLeft
            {
                {{order0}}public string Name { get; set; } = "";
            }

            {{memoryPackable}}public partial class ChatMember
            {
                {{order0}}public string Name { get; set; } = "";
            }

            {{memoryPackable}}public partial class ChatMessage
            {
                {{order0}}public string SenderName { get; set; } = "";
                {{order1}}public string Text { get; set; } = "";
                {{order2}}public long Timestamp { get; set; }
            }
        }
        """;
    }

    public static string RenderServerChatRoom()
    {
        return """
        using System;
        using System.Collections.Concurrent;
        using System.Collections.Generic;
        using System.Linq;
        using Shared.Chat;

        namespace Server.Chat
        {
            internal sealed class ChatRoom
            {
                private const int MaxRecentMessages = 100;
                private readonly ConcurrentDictionary<string, (string Name, IChatCallback Callback)> _members = new();
                private readonly ConcurrentQueue<ChatMessage> _recentMessages = new();
                private readonly object _lock = new();

                public ChatJoinReply Join(string connectionId, string playerName, IChatCallback callback)
                {
                    var member = new ChatMember { Name = playerName };
                    _members[connectionId] = (playerName, callback);

                    Broadcast(cb => cb.OnUserJoined(member), excludeConnectionId: null);

                    return new ChatJoinReply
                    {
                        Members = _members.Values.Select(v => new ChatMember { Name = v.Name }).ToList(),
                        RecentMessages = _recentMessages.ToList()
                    };
                }

                public void Send(string connectionId, string text)
                {
                    if (!_members.TryGetValue(connectionId, out var entry))
                    {
                        return;
                    }

                    var msg = new ChatMessage
                    {
                        SenderName = entry.Name,
                        Text = text,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    _recentMessages.Enqueue(msg);
                    lock (_lock)
                    {
                        while (_recentMessages.Count > MaxRecentMessages)
                        {
                            _recentMessages.TryDequeue(out _);
                        }
                    }

                    Broadcast(cb => cb.OnMessageReceived(msg), excludeConnectionId: null);
                }

                public void Leave(string connectionId)
                {
                    if (!_members.TryRemove(connectionId, out var entry))
                    {
                        return;
                    }

                    Broadcast(cb => cb.OnUserLeft(new ChatUserLeft { Name = entry.Name }), excludeConnectionId: null);
                }

                public void Disconnect(string connectionId)
                {
                    Leave(connectionId);
                }

                private void Broadcast(Action<IChatCallback> action, string? excludeConnectionId)
                {
                    foreach (var (connId, (_, callback)) in _members)
                    {
                        if (connId == excludeConnectionId)
                        {
                            continue;
                        }

                        try
                        {
                            action(callback);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
        """;
    }

    public static string RenderServerChatServiceImpl()
    {
        return """
        using System;
        using Shared.Chat;

        namespace Server.Chat
        {
            internal sealed class ChatServiceImpl : IChatService
            {
                private static readonly ChatRoom SharedRoom = new();

                private readonly IChatCallback _callback;
                private readonly ChatRoom _room;
                private readonly string _connectionId;

                public ChatServiceImpl(IChatCallback callback)
                {
                    _callback = callback;
                    _room = SharedRoom;
                    _connectionId = Guid.NewGuid().ToString("N");
                }

                public ValueTask<ChatJoinReply> JoinAsync(ChatJoinRequest req)
                {
                    return new ValueTask<ChatJoinReply>(_room.Join(_connectionId, req.PlayerName, _callback));
                }

                public ValueTask SendAsync(ChatSendRequest req)
                {
                    _room.Send(_connectionId, req.Text);
                    return ValueTask.CompletedTask;
                }

                public ValueTask LeaveAsync(ChatLeaveRequest req)
                {
                    _room.Leave(_connectionId);
                    return ValueTask.CompletedTask;
                }
            }
        }
        """;
    }

    public static string RenderHotfixChatSystem()
    {
        return """
        using Shared.Chat;

        namespace Server.Hotfix.Chat
        {
            public static class ChatSystem
            {
                public static ChatMessage SanitizeMessage(this ChatMessage message)
                {
                    if (string.IsNullOrWhiteSpace(message.Text))
                    {
                        message.Text = "<empty>";
                    }
                    else if (message.Text.Length > 500)
                    {
                        message.Text = message.Text[..500];
                    }

                    return message;
                }
            }
        }
        """;
    }

    public static string RenderClientChatClient()
    {
        return """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Rpc.Generated;
        using Shared.Chat;
        using ULinkRPC.Client;

        namespace Client.Chat
        {
            public sealed class ChatClient : IChatCallback, IAsyncDisposable
            {
                private readonly RpcClient _rpcClient;
                private IChatService? _chatService;
                private bool _isConnected;

                public event Action<ChatMessage>? OnMessageReceived;
                public event Action<ChatMember>? OnUserJoined;
                public event Action<string>? OnUserLeft;
                public event Action? OnDisconnected;

                public bool IsConnected => _isConnected;

                public ChatClient(RpcClientOptions options)
                {
                    var callbacks = new RpcClient.RpcCallbackBindings();
                    callbacks.Add(this);

                    _rpcClient = new RpcClient(options, callbacks);
                    _rpcClient.Disconnected += _ =>
                    {
                        _isConnected = false;
                        OnDisconnected?.Invoke();
                    };
                }

                public async Task ConnectAsync(CancellationToken cancellationToken = default)
                {
                    await _rpcClient.ConnectAsync(cancellationToken);
                    _chatService = _rpcClient.Api.Shared.Chat;
                    _isConnected = true;
                }

                public async Task<ChatJoinReply> JoinAsync(string playerName)
                {
                    if (_chatService == null) throw new InvalidOperationException("Not connected.");
                    return await _chatService.JoinAsync(new ChatJoinRequest { PlayerName = playerName });
                }

                public async Task SendAsync(string text)
                {
                    if (_chatService == null) throw new InvalidOperationException("Not connected.");
                    await _chatService.SendAsync(new ChatSendRequest { Text = text });
                }

                public async Task LeaveAsync()
                {
                    if (_chatService == null) return;
                    await _chatService.LeaveAsync(new ChatLeaveRequest());
                }

                public async ValueTask DisposeAsync()
                {
                    _isConnected = false;
                    await _rpcClient.DisposeAsync();
                }

                void IChatCallback.OnMessageReceived(ChatMessage msg)
                {
                    OnMessageReceived?.Invoke(msg);
                }

                void IChatCallback.OnUserJoined(ChatMember member)
                {
                    OnUserJoined?.Invoke(member);
                }

                void IChatCallback.OnUserLeft(ChatUserLeft evt)
                {
                    OnUserLeft?.Invoke(evt.Name);
                }
            }
        }
        """;
    }

    public static string RenderGodotChatScene(NewCommandOptions options)
    {
        var defaultPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var serializerUsing = options.Serializer switch
        {
            "json" => "using ULinkRPC.Serializer.Json;",
            _ => "using ULinkRPC.Serializer.MemoryPack;"
        };
        var transportUsing = options.Transport switch
        {
            "tcp" => "using ULinkRPC.Transport.Tcp;",
            "websocket" => "using ULinkRPC.Transport.WebSocket;",
            _ => "using ULinkRPC.Transport.Kcp;"
        };
        var serializerConstructor = options.Serializer switch
        {
            "json" => "new JsonRpcSerializer()",
            _ => "new MemoryPackRpcSerializer()"
        };
        var transportConstructor = options.Transport switch
        {
            "tcp" => "new TcpTransport(_serverHost, _serverPort)",
            "websocket" => "new WsTransport($\"ws://{_serverHost}:{_serverPort}{NormalizePath(_serverPath)}\")",
            _ => "new KcpTransport(_serverHost, _serverPort)"
        };

        return $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Godot;
        using Shared.Chat;
        using ULinkRPC.Client;
        using ULinkRPC.Core;
        {{serializerUsing}}
        {{transportUsing}}

        namespace Client.Chat
        {
            public partial class ChatScene : Control
            {
                [Export] private string _serverHost = "127.0.0.1";
                [Export] private int _serverPort = 20000;
                [Export] private string _serverPath = "{{TemplateText.SanitizeStringLiteral(defaultPath)}}";

                private readonly CancellationTokenSource _cts = new();
                private ChatClient? _client;
                private LineEdit? _nameField;
                private LineEdit? _messageField;
                private Button? _joinButton;
                private Button? _sendButton;
                private RichTextLabel? _messageLog;
                private Label? _onlineCount;
                private bool _isJoining;
                private bool _isSending;

                public override void _Ready()
                {
                    BuildUi();
                    SetJoinBusy(false);
                    SetSendBusy(false);
                    AppendSystemMessage("Enter a name, click Join, then send a message.");
                }

                private void BuildUi()
                {
                    SetAnchorsPreset(LayoutPreset.FullRect);

                    var background = new ColorRect
                    {
                        Name = "Background",
                        Color = new Color(0.10f, 0.10f, 0.12f, 1.0f)
                    };
                    background.SetAnchorsPreset(LayoutPreset.FullRect);
                    AddChild(background);

                    var margin = new MarginContainer { Name = "Layout" };
                    margin.SetAnchorsPreset(LayoutPreset.FullRect);
                    margin.AddThemeConstantOverride("margin_left", 16);
                    margin.AddThemeConstantOverride("margin_top", 16);
                    margin.AddThemeConstantOverride("margin_right", 16);
                    margin.AddThemeConstantOverride("margin_bottom", 16);
                    AddChild(margin);

                    var layout = new VBoxContainer { Name = "ChatLayout" };
                    layout.AddThemeConstantOverride("separation", 10);
                    margin.AddChild(layout);

                    var header = new HBoxContainer { Name = "Header" };
                    header.AddThemeConstantOverride("separation", 12);
                    layout.AddChild(header);

                    var title = new Label { Name = "Title", Text = "Chat Room" };
                    title.AddThemeFontSizeOverride("font_size", 24);
                    title.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.98f, 1.0f));
                    title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    header.AddChild(title);

                    _onlineCount = new Label { Name = "OnlineCount", Text = "Online: --" };
                    _onlineCount.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.62f, 1.0f));
                    header.AddChild(_onlineCount);

                    _messageLog = new RichTextLabel
                    {
                        Name = "MessageLog",
                        BbcodeEnabled = false,
                        ScrollFollowing = true
                    };
                    _messageLog.AddThemeColorOverride("default_color", new Color(0.88f, 0.90f, 0.94f, 1.0f));
                    _messageLog.SizeFlagsVertical = SizeFlags.ExpandFill;
                    layout.AddChild(_messageLog);

                    var footer = new VBoxContainer { Name = "Footer" };
                    footer.AddThemeConstantOverride("separation", 8);
                    layout.AddChild(footer);

                    var joinRow = new HBoxContainer { Name = "JoinRow" };
                    joinRow.AddThemeConstantOverride("separation", 8);
                    footer.AddChild(joinRow);

                    _nameField = new LineEdit { Name = "NameField", PlaceholderText = "Name", MaxLength = 20 };
                    StyleLineEdit(_nameField);
                    _nameField.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    joinRow.AddChild(_nameField);

                    _joinButton = new Button { Name = "JoinButton", Text = "Join" };
                    StyleButton(_joinButton);
                    _joinButton.Pressed += OnJoinPressed;
                    joinRow.AddChild(_joinButton);

                    var sendRow = new HBoxContainer { Name = "SendRow" };
                    sendRow.AddThemeConstantOverride("separation", 8);
                    footer.AddChild(sendRow);

                    _messageField = new LineEdit { Name = "MessageField", PlaceholderText = "Message", MaxLength = 500 };
                    StyleLineEdit(_messageField);
                    _messageField.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    _messageField.TextSubmitted += _ => OnSendPressed();
                    sendRow.AddChild(_messageField);

                    _sendButton = new Button { Name = "SendButton", Text = "Send" };
                    StyleButton(_sendButton);
                    _sendButton.Pressed += OnSendPressed;
                    sendRow.AddChild(_sendButton);
                }

                private async void OnJoinPressed()
                {
                    if (_isJoining)
                    {
                        return;
                    }

                    if (_client != null && _client.IsConnected)
                    {
                        AppendSystemMessage("Already connected.");
                        return;
                    }

                    var name = _nameField?.Text.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        AppendSystemMessage("Enter a name before joining.");
                        _nameField?.GrabFocus();
                        return;
                    }

                    SetJoinBusy(true);
                    AppendSystemMessage("Connecting...");

                    var client = new ChatClient(CreateRpcClientOptions());
                    client.OnMessageReceived += msg => CallDeferred(nameof(AppendMessageDeferred), msg.SenderName, msg.Text);
                    client.OnUserJoined += member => CallDeferred(nameof(AppendSystemMessageDeferred), $"{member.Name} joined.");
                    client.OnUserLeft += memberName => CallDeferred(nameof(AppendSystemMessageDeferred), $"{memberName} left.");
                    client.OnDisconnected += () => CallDeferred(nameof(AppendSystemMessageDeferred), "Disconnected from server.");

                    try
                    {
                        await client.ConnectAsync(_cts.Token);
                        var reply = await client.JoinAsync(name);
                        _client = client;
                        AppendSystemMessage($"Connected. {reply.Members.Count} online.");
                        SetOnlineCount(reply.Members.Count);

                        foreach (var msg in reply.RecentMessages)
                        {
                            AppendMessageText(msg.SenderName, msg.Text);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendSystemMessage($"Connection failed: {ex.Message}");
                        await client.DisposeAsync();
                    }
                    finally
                    {
                        SetJoinBusy(false);
                    }
                }

                private async void OnSendPressed()
                {
                    if (_isSending)
                    {
                        return;
                    }

                    if (_client == null || !_client.IsConnected)
                    {
                        AppendSystemMessage("Join the chat before sending.");
                        return;
                    }

                    var text = _messageField?.Text.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    SetSendBusy(true);
                    try
                    {
                        await _client.SendAsync(text);
                        if (_messageField != null)
                        {
                            _messageField.Text = string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendSystemMessage($"Send failed: {ex.Message}");
                    }
                    finally
                    {
                        SetSendBusy(false);
                    }
                }

                public void AppendMessageDeferred(string senderName, string text)
                {
                    AppendMessageText(senderName, text);
                }

                public void AppendSystemMessageDeferred(string text)
                {
                    AppendSystemMessage(text);
                }

                private void AppendMessageText(string senderName, string text)
                {
                    AppendLine($"[{senderName}]: {text}");
                }

                private void AppendSystemMessage(string text)
                {
                    AppendLine($"* {text}");
                }

                private void AppendLine(string text)
                {
                    _messageLog?.AppendText(text + System.Environment.NewLine);
                }

                private void SetOnlineCount(int count)
                {
                    if (_onlineCount != null)
                    {
                        _onlineCount.Text = $"Online: {count}";
                    }
                }

                private void SetJoinBusy(bool isBusy)
                {
                    _isJoining = isBusy;
                    if (_joinButton != null)
                    {
                        _joinButton.Disabled = isBusy;
                        _joinButton.Text = isBusy ? "Joining..." : "Join";
                    }
                }

                private void SetSendBusy(bool isBusy)
                {
                    _isSending = isBusy;
                    if (_sendButton != null)
                    {
                        _sendButton.Disabled = isBusy;
                        _sendButton.Text = isBusy ? "Sending..." : "Send";
                    }
                }

                private RpcClientOptions CreateRpcClientOptions()
                {
                    return new RpcClientOptions(
                        {{transportConstructor}},
                        {{serializerConstructor}})
                        .UseSecurity(ConfigureTransportSecurity);
                }

                private static string NormalizePath(string path)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return string.Empty;
                    }

                    return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
                }

                private static void ConfigureTransportSecurity(TransportSecurityConfig security)
                {
                    security.EnableCompression = false;
                    security.CompressionThresholdBytes = 1024;
                    security.EnableEncryption = false;
                    security.EncryptionKeyBase64 = null;
                }

                private static void StyleLineEdit(LineEdit lineEdit)
                {
                    lineEdit.CustomMinimumSize = new Vector2(0, 36);
                    lineEdit.AddThemeColorOverride("font_color", new Color(0.96f, 0.96f, 0.96f, 1.0f));
                    lineEdit.AddThemeColorOverride("font_placeholder_color", new Color(0.58f, 0.62f, 0.70f, 1.0f));
                }

                private static void StyleButton(Button button)
                {
                    button.CustomMinimumSize = new Vector2(96, 36);
                    button.AddThemeColorOverride("font_color", new Color(0.96f, 0.96f, 0.96f, 1.0f));
                    button.AddThemeColorOverride("font_disabled_color", new Color(0.70f, 0.72f, 0.76f, 1.0f));
                }

                public override void _ExitTree()
                {
                    _cts.Cancel();
                    if (_client is not null)
                    {
                        _ = _client.DisposeAsync();
                    }
                    _cts.Dispose();
                }
            }
        }
        """;
    }

    public static string RenderGodotMainScene()
    {
        return """
        [gd_scene load_steps=2 format=3]

        [ext_resource type="Script" path="res://Scripts/Chat/ChatScene.cs" id="1"]

        [node name="ChatScene" type="Control"]
        layout_mode = 3
        anchors_preset = 15
        anchor_right = 1.0
        anchor_bottom = 1.0
        grow_horizontal = 2
        grow_vertical = 2
        script = ExtResource("1")
        """;
    }

    public static string RenderClientChatUI(NewCommandOptions options)
    {
        var defaultPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var serializerUsing = options.Serializer switch
        {
            "json" => "using ULinkRPC.Serializer.Json;",
            _ => "using ULinkRPC.Serializer.MemoryPack;"
        };
        var transportUsing = options.Transport switch
        {
            "tcp" => "using ULinkRPC.Transport.Tcp;",
            "websocket" => "using ULinkRPC.Transport.WebSocket;",
            _ => "using ULinkRPC.Transport.Kcp;"
        };
        var serializerConstructor = options.Serializer switch
        {
            "json" => "new JsonRpcSerializer()",
            _ => "new MemoryPackRpcSerializer()"
        };
        var transportConstructor = options.Transport switch
        {
            "tcp" => "new TcpTransport(_serverHost, _serverPort)",
            "websocket" => "new WsTransport($\"ws://{_serverHost}:{_serverPort}{NormalizePath(_serverPath)}\")",
            _ => "new KcpTransport(_serverHost, _serverPort)"
        };

        return $$"""
        using System;
        using System.Collections.Concurrent;
        using System.Threading;
        using System.Threading.Tasks;
        using Shared.Chat;
        using ULinkRPC.Client;
        using ULinkRPC.Core;
        {{serializerUsing}}
        {{transportUsing}}
        using UnityEngine;
        using UnityEngine.UIElements;

        namespace Client.Chat
        {
            [RequireComponent(typeof(UIDocument))]
            public sealed class ChatUI : MonoBehaviour
            {
                [SerializeField] private string _serverHost = "127.0.0.1";
                [SerializeField] private int _serverPort = 20000;
                [SerializeField] private string _serverPath = "{{TemplateText.SanitizeStringLiteral(defaultPath)}}";

                private readonly CancellationTokenSource _cts = new();
                private readonly ConcurrentQueue<Action> _mainThreadActions = new();
                private ChatClient? _client;
                private TextField? _inputField;
                private TextField? _nameField;
                private ScrollView? _messageList;
                private Label? _onlineCount;
                private Button? _sendButton;
                private Button? _joinButton;
                private bool _isJoining;
                private bool _isSending;

                private async void Start()
                {
                    var root = GetComponent<UIDocument>().rootVisualElement;

                    _inputField = root.Q<TextField>("chat-input");
                    _nameField = root.Q<TextField>("name-field");
                    _messageList = root.Q<ScrollView>("message-list");
                    _onlineCount = root.Q<Label>("online-count");
                    _sendButton = root.Q<Button>("send-button");
                    _joinButton = root.Q<Button>("join-button");

                    if (_sendButton != null)
                    {
                        _sendButton.clicked += OnSendClicked;
                    }

                    _inputField?.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        {
                            OnSendClicked();
                        }
                    });

                    if (_joinButton != null)
                    {
                        _joinButton.clicked += OnJoinClicked;
                    }

                    SetSendBusy(false);
                    SetJoinBusy(false);
                    AppendSystemMessage("Enter a name, click Join, then send a message.");
                }

                private async void OnJoinClicked()
                {
                    if (_isJoining)
                    {
                        return;
                    }

                    if (_client != null && _client.IsConnected)
                    {
                        AppendSystemMessage("Already connected.");
                        return;
                    }

                    var name = _nameField?.value?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        AppendSystemMessage("Enter a name before joining.");
                        _nameField?.Focus();
                        return;
                    }

                    SetJoinBusy(true);
                    AppendSystemMessage("Connecting...");

                    var client = new ChatClient(CreateRpcClientOptions());
                    client.OnMessageReceived += msg => EnqueueMainThread(() => AppendMessage(msg));
                    client.OnUserJoined += member => EnqueueMainThread(() => OnUserJoinedHandler(member));
                    client.OnUserLeft += memberName => EnqueueMainThread(() => OnUserLeftHandler(memberName));
                    client.OnDisconnected += () => EnqueueMainThread(() => AppendSystemMessage("Disconnected from server."));

                    try
                    {
                        await client.ConnectAsync(_cts.Token);
                        var reply = await client.JoinAsync(name);
                        _client = client;
                        AppendSystemMessage($"Connected. {reply.Members.Count} online.");
                        SetOnlineCount(reply.Members.Count);

                        foreach (var msg in reply.RecentMessages)
                        {
                            AppendMessage(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendSystemMessage($"Connection failed: {ex.Message}");
                        await client.DisposeAsync();
                    }
                    finally
                    {
                        SetJoinBusy(false);
                    }
                }

                private async void OnSendClicked()
                {
                    if (_isSending)
                    {
                        return;
                    }

                    if (_client == null || !_client.IsConnected)
                    {
                        AppendSystemMessage("Join the chat before sending.");
                        return;
                    }

                    var text = _inputField?.value?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    SetSendBusy(true);
                    try
                    {
                        await _client.SendAsync(text);
                        _inputField!.value = "";
                    }
                    catch (Exception ex)
                    {
                        AppendSystemMessage($"Send failed: {ex.Message}");
                    }
                    finally
                    {
                        SetSendBusy(false);
                    }
                }

                private void Update()
                {
                    while (_mainThreadActions.TryDequeue(out var action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }

                private void EnqueueMainThread(Action action)
                {
                    _mainThreadActions.Enqueue(action);
                }

                private void AppendMessage(ChatMessage msg)
                {
                    var label = new Label($"[{msg.SenderName}]: {msg.Text}");
                    label.AddToClassList("chat-message");
                    _messageList?.Add(label);
                    _messageList?.ScrollTo(label);
                }

                private void AppendSystemMessage(string text)
                {
                    var label = new Label(text);
                    label.AddToClassList("chat-system");
                    _messageList?.Add(label);
                    _messageList?.ScrollTo(label);
                }

                private void SetOnlineCount(int count)
                {
                    if (_onlineCount != null)
                    {
                        _onlineCount.text = $"Online: {count}";
                    }
                }

                private void SetJoinBusy(bool isBusy)
                {
                    _isJoining = isBusy;
                    if (_joinButton != null)
                    {
                        _joinButton.SetEnabled(!isBusy);
                        _joinButton.text = isBusy ? "Joining..." : "Join";
                    }
                }

                private void SetSendBusy(bool isBusy)
                {
                    _isSending = isBusy;
                    if (_sendButton != null)
                    {
                        _sendButton.SetEnabled(!isBusy);
                        _sendButton.text = isBusy ? "Sending..." : "Send";
                    }
                }

                private RpcClientOptions CreateRpcClientOptions()
                {
                    return new RpcClientOptions(
                        {{transportConstructor}},
                        {{serializerConstructor}})
                        .UseSecurity(ConfigureTransportSecurity);
                }

                private static string NormalizePath(string path)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return string.Empty;
                    }

                    return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
                }

                private static void ConfigureTransportSecurity(TransportSecurityConfig security)
                {
                    security.EnableCompression = false;
                    security.CompressionThresholdBytes = 1024;
                    security.EnableEncryption = false;
                    security.EncryptionKeyBase64 = null;
                }

                private void OnUserJoinedHandler(ChatMember member)
                {
                    AppendSystemMessage($"{member.Name} joined.");
                }

                private void OnUserLeftHandler(string memberName)
                {
                    AppendSystemMessage($"{memberName} left.");
                }

                private void OnDestroy()
                {
                    _cts.Cancel();
                    if (_client is not null)
                    {
                        _ = _client.DisposeAsync();
                    }
                    _cts.Dispose();
                }
            }
        }
        """;
    }

    public static string RenderClientChatUxml()
    {
        return """
        <ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
            <Style src="ChatScene.uss" />
            <ui:VisualElement class="chat-container" style="width: 100%; height: 100%; flex-grow: 1;">
                <ui:VisualElement class="chat-header">
                    <ui:Label text="Chat Room" class="header-title" />
                    <ui:Label text="Online: --" name="online-count" class="header-count" />
                </ui:VisualElement>
                <ui:ScrollView name="message-list" class="message-list" />
                <ui:VisualElement class="chat-footer">
                    <ui:VisualElement name="join-panel" class="join-panel">
                        <ui:TextField name="name-field" label="Name" max-length="20" class="name-field" />
                        <ui:Button text="Join" name="join-button" class="join-button" />
                    </ui:VisualElement>
                    <ui:TextField name="chat-input" label="Message" max-length="500" class="chat-input" />
                    <ui:Button text="Send" name="send-button" class="send-button" />
                </ui:VisualElement>
            </ui:VisualElement>
        </ui:UXML>
        """;
    }

    public static string RenderClientChatUss()
    {
        return """
        .chat-container {
            width: 100%;
            height: 100%;
            flex-grow: 1;
            background-color: rgb(30, 30, 30);
            color: rgb(230, 230, 230);
        }
        .chat-header {
            flex-direction: row;
            padding: 8px 16px;
            background-color: rgb(40, 40, 40);
            border-bottom-width: 1px;
            border-bottom-color: rgb(60, 60, 60);
        }
        .header-title {
            font-size: 18px;
            color: rgb(200, 200, 200);
        }
        .header-count {
            font-size: 14px;
            color: rgb(120, 180, 120);
            margin-left: auto;
        }
        .message-list {
            flex-grow: 1;
            padding: 8px;
        }
        .unity-label {
            color: rgb(230, 230, 230);
        }
        .chat-message {
            font-size: 14px;
            color: rgb(220, 220, 220);
            margin-bottom: 4px;
        }
        .chat-system {
            font-size: 13px;
            color: rgb(140, 140, 140);
            -unity-font-style: italic;
            margin-bottom: 4px;
        }
        .chat-footer {
            padding: 8px;
            background-color: rgb(40, 40, 40);
            border-top-width: 1px;
            border-top-color: rgb(60, 60, 60);
        }
        .join-panel {
            flex-direction: row;
            margin-bottom: 8px;
        }
        .name-field {
            flex-grow: 1;
            margin-right: 8px;
        }
        .name-field .unity-text-field__label,
        .chat-input .unity-text-field__label {
            color: rgb(210, 210, 210);
        }
        .name-field .unity-text-field__input,
        .chat-input .unity-text-field__input {
            color: rgb(245, 245, 245);
            background-color: rgb(24, 24, 24);
            border-top-color: rgb(80, 80, 80);
            border-right-color: rgb(80, 80, 80);
            border-bottom-color: rgb(80, 80, 80);
            border-left-color: rgb(80, 80, 80);
        }
        .join-button {
            width: 80px;
        }
        .chat-input {
            flex-grow: 1;
        }
        .send-button {
            width: 80px;
        }
        .join-button,
        .send-button {
            color: rgb(245, 245, 245);
            background-color: rgb(54, 94, 160);
            border-top-color: rgb(86, 132, 210);
            border-right-color: rgb(86, 132, 210);
            border-bottom-color: rgb(86, 132, 210);
            border-left-color: rgb(86, 132, 210);
        }
        .join-button:disabled,
        .send-button:disabled {
            color: rgb(190, 190, 190);
            background-color: rgb(66, 66, 66);
            border-top-color: rgb(90, 90, 90);
            border-right-color: rgb(90, 90, 90);
            border-bottom-color: rgb(90, 90, 90);
            border-left-color: rgb(90, 90, 90);
        }
        """;
    }

    public static string RenderUnityMonoScriptMeta(string guid)
    {
        return $$"""
        fileFormatVersion: 2
        guid: {{guid}}
        MonoImporter:
          externalObjects: {}
          serializedVersion: 2
          defaultReferences: []
          executionOrder: 0
          icon: {instanceID: 0}
          userData:
          assetBundleName:
          assetBundleVariant:
        """;
    }

    public static string RenderUnityUxmlMeta(string guid)
    {
        return $$"""
        fileFormatVersion: 2
        guid: {{guid}}
        ScriptedImporter:
          internalIDToNameTable: []
          externalObjects: {}
          serializedVersion: 2
          userData:
          assetBundleName:
          assetBundleVariant:
          script: {fileID: 13804, guid: 0000000000000000e000000000000000, type: 0}
        """;
    }

    public static string RenderUnityUssMeta(string guid)
    {
        return $$"""
        fileFormatVersion: 2
        guid: {{guid}}
        ScriptedImporter:
          internalIDToNameTable: []
          externalObjects: {}
          serializedVersion: 2
          userData:
          assetBundleName:
          assetBundleVariant:
          script: {fileID: 12385, guid: 0000000000000000e000000000000000, type: 0}
          disableValidation: 0
        """;
    }

    public static string RenderUnityTssMeta(string guid)
    {
        return $$"""
        fileFormatVersion: 2
        guid: {{guid}}
        ScriptedImporter:
          internalIDToNameTable: []
          externalObjects: {}
          serializedVersion: 2
          userData:
          assetBundleName:
          assetBundleVariant:
          script: {fileID: 12388, guid: 0000000000000000e000000000000000, type: 0}
          disableValidation: 0
        """;
    }

    public static string RenderUnityNativeAssetMeta(string guid)
    {
        return $$"""
        fileFormatVersion: 2
        guid: {{guid}}
        NativeFormatImporter:
          externalObjects: {}
          mainObjectFileID: 11400000
          userData:
          assetBundleName:
          assetBundleVariant:
        """;
    }

    public static string RenderUnityDefaultRuntimeTheme()
    {
        return """
        @import url("unity-theme://default");
        """;
    }

    public static string RenderUnityPanelSettingsAsset(string defaultRuntimeThemeGuid)
    {
        return $$"""
        %YAML 1.1
        %TAG !u! tag:unity3d.com,2011:
        --- !u!114 &11400000
        MonoBehaviour:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {fileID: 0}
          m_PrefabInstance: {fileID: 0}
          m_PrefabAsset: {fileID: 0}
          m_GameObject: {fileID: 0}
          m_Enabled: 1
          m_EditorHideFlags: 0
          m_Script: {fileID: 19101, guid: 0000000000000000e000000000000000, type: 0}
          m_Name: ULinkGameChatPanelSettings
          m_EditorClassIdentifier:
          themeUss: {fileID: -4733365628477956816, guid: {{defaultRuntimeThemeGuid}}, type: 3}
          m_TargetTexture: {fileID: 0}
          m_ScaleMode: 1
          m_ReferenceSpritePixelsPerUnit: 100
          m_Scale: 1
          m_ReferenceDpi: 96
          m_FallbackDpi: 96
          m_ReferenceResolution: {x: 1200, y: 800}
          m_ScreenMatchMode: 0
          m_Match: 0
          m_SortingOrder: 0
          m_TargetDisplay: 0
          m_ClearDepthStencil: 1
          m_ClearColor: 0
          m_ColorClearValue: {r: 0, g: 0, b: 0, a: 0}
          m_DynamicAtlasSettings:
            m_MinAtlasSize: 64
            m_MaxAtlasSize: 4096
            m_MaxSubTextureSize: 64
            m_ActiveFilters: 31
          m_AtlasBlitShader: {fileID: 9101, guid: 0000000000000000f000000000000000, type: 0}
          m_RuntimeShader: {fileID: 9100, guid: 0000000000000000f000000000000000, type: 0}
          m_RuntimeWorldShader: {fileID: 9102, guid: 0000000000000000f000000000000000, type: 0}
          textSettings: {fileID: 0}
        """;
    }

    public static string RenderUnityChatSceneObjects(
        long gameObjectId,
        long chatUiComponentId,
        long uiDocumentComponentId,
        long transformId,
        string chatUiScriptGuid,
        string uxmlGuid,
        string panelSettingsGuid,
        string serverPath = "")
    {
        return $$"""
        --- !u!1 &{{gameObjectId}}
        GameObject:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {fileID: 0}
          m_PrefabInstance: {fileID: 0}
          m_PrefabAsset: {fileID: 0}
          serializedVersion: 6
          m_Component:
          - component: {fileID: {{transformId}}}
          - component: {fileID: {{uiDocumentComponentId}}}
          - component: {fileID: {{chatUiComponentId}}}
          m_Layer: 0
          m_Name: ULinkGame Chat UI
          m_TagString: Untagged
          m_Icon: {fileID: 0}
          m_NavMeshLayer: 0
          m_StaticEditorFlags: 0
          m_IsActive: 1
        --- !u!114 &{{chatUiComponentId}}
        MonoBehaviour:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {fileID: 0}
          m_PrefabInstance: {fileID: 0}
          m_PrefabAsset: {fileID: 0}
          m_GameObject: {fileID: {{gameObjectId}}}
          m_Enabled: 1
          m_EditorHideFlags: 0
          m_Script: {fileID: 11500000, guid: {{chatUiScriptGuid}}, type: 3}
          m_Name:
          m_EditorClassIdentifier:
          _serverHost: 127.0.0.1
          _serverPort: 20000
          _serverPath: {{serverPath}}
        --- !u!114 &{{uiDocumentComponentId}}
        MonoBehaviour:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {fileID: 0}
          m_PrefabInstance: {fileID: 0}
          m_PrefabAsset: {fileID: 0}
          m_GameObject: {fileID: {{gameObjectId}}}
          m_Enabled: 1
          m_EditorHideFlags: 0
          m_Script: {fileID: 19102, guid: 0000000000000000e000000000000000, type: 0}
          m_Name:
          m_EditorClassIdentifier:
          m_PanelSettings: {fileID: 11400000, guid: {{panelSettingsGuid}}, type: 2}
          m_ParentUI: {fileID: 0}
          sourceAsset: {fileID: 9197481963319205126, guid: {{uxmlGuid}}, type: 3}
          m_SortingOrder: 0
        --- !u!4 &{{transformId}}
        Transform:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {fileID: 0}
          m_PrefabInstance: {fileID: 0}
          m_PrefabAsset: {fileID: 0}
          m_GameObject: {fileID: {{gameObjectId}}}
          serializedVersion: 2
          m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
          m_LocalPosition: {x: 0, y: 0, z: 0}
          m_LocalScale: {x: 1, y: 1, z: 1}
          m_ConstrainProportionsScale: 0
          m_Children: []
          m_Father: {fileID: 0}
          m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
        """;
    }

    public static string RenderUnityNuGetPackageImportGuard()
    {
        return """
        #if UNITY_EDITOR
        using System;
        using UnityEditor;

        [InitializeOnLoad]
        internal sealed class ULinkGameNuGetPackageImportGuard : AssetPostprocessor
        {
            static ULinkGameNuGetPackageImportGuard()
            {
                EditorApplication.delayCall += DisableExistingAnalyzerPlugins;
            }

            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                foreach (var assetPath in importedAssets)
                {
                    DisableAnalyzerPlugin(assetPath);
                }

                foreach (var assetPath in movedAssets)
                {
                    DisableAnalyzerPlugin(assetPath);
                }
            }

            private static void DisableExistingAnalyzerPlugins()
            {
                var pluginGuids = AssetDatabase.FindAssets("t:PluginImporter", new[] { "Assets/Packages" });
                foreach (var guid in pluginGuids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    DisableAnalyzerPlugin(assetPath);
                }
            }

            private static void DisableAnalyzerPlugin(string assetPath)
            {
                var normalizedPath = assetPath.Replace('\\', '/');
                if (!normalizedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.IndexOf("Assets/Packages/", StringComparison.OrdinalIgnoreCase) < 0 ||
                    normalizedPath.IndexOf("/analyzers/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
                if (importer == null)
                {
                    return;
                }

                if (!importer.GetCompatibleWithAnyPlatform() && !importer.GetCompatibleWithEditor())
                {
                    return;
                }

                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SaveAndReimport();
            }
        }
        #endif
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
        return @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Guardrails.Rules;

namespace Server.Hosting;

internal sealed class ULinkGameRuntimeOptions
{
    private const string NodeIdConfigurationKey = ""ULinkGame:Node:Id"";
    private const string EndpointTransportConfigurationKey = ""ULinkGame:Endpoint:Transport"";
    private const string EndpointHostConfigurationKey = ""ULinkGame:Endpoint:Host"";
    private const string EndpointPortConfigurationKey = ""ULinkGame:Endpoint:Port"";
    private const string EndpointPathConfigurationKey = ""ULinkGame:Endpoint:Path"";

    public ULinkGameNodeOptions Node { get; init; } = new();
    public ULinkGameEndpointOptions Endpoint { get; init; } = new();
    public string ClusterEndpoint { get; init; } = ""tcp://127.0.0.1:21000"";
    public string AdvertisedClientEndpoint => Endpoint.ToAdvertisedEndpoint();

    public static ULinkGameRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(""ULinkGame"");
        return new ULinkGameRuntimeOptions
        {
            Node = ULinkGameNodeOptions.FromConfiguration(section.GetSection(""Node"")),
            Endpoint = ULinkGameEndpointOptions.FromConfiguration(section.GetSection(""Endpoint""))
        };
    }

    public ServerRpcServerOptions ToServerRpcServerOptions()
    {
        return new ServerRpcServerOptions
        {
            Transport = Endpoint.Transport,
            Host = Endpoint.Host,
            Port = Endpoint.Port,
            Path = Endpoint.Path
        };
    }

    public ClusterOptions ToClusterOptions()
    {
        return new ClusterOptions
        {
            NodeId = Node.Id,
            AdvertisedEndpoints = new Dictionary<string, string>
            {
                [""cluster""] = ClusterEndpoint,
                [""client""] = AdvertisedClientEndpoint
            },
            Bootstrap = new ClusterBootstrapOptions
            {
                NodeDirectoryEndpoints = new[] { ClusterEndpoint }
            },
            Services = new[]
            {
                new ClusterServiceOptions { Kind = ""node-directory"", Name = ""node-directory"" },
                new ClusterServiceOptions { Kind = ""route-directory"", Name = ""route-directory"" },
                new ClusterServiceOptions { Kind = ""gateway"", Name = ""gateway"" }
            }
        };
    }

    public ClusterOptions ToClusterOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(""Cluster"");
        var defaults = ToClusterOptions();
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

internal sealed class ULinkGameNodeOptions
{
    public string Id { get; init; } = ""dev-1"";

    public static ULinkGameNodeOptions FromConfiguration(IConfiguration section)
    {
        return new ULinkGameNodeOptions
        {
            Id = ReadString(section, ""Id"", ""dev-1"")
        };
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

internal sealed class ULinkGameEndpointOptions
{
    public string Transport { get; init; } = ""kcp"";
    public string Host { get; init; } = ""127.0.0.1"";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = """";

    public static ULinkGameEndpointOptions FromConfiguration(IConfiguration section)
    {
        var transport = NormalizeTransport(section[""Transport""], ""kcp"");
        return new ULinkGameEndpointOptions
        {
            Transport = transport,
            Host = ReadString(section, ""Host"", ""127.0.0.1""),
            Port = ReadInt(section, ""Port"", 20000),
            Path = ReadString(section, ""Path"", GetDefaultPath(transport))
        };
    }

    public string ToAdvertisedEndpoint()
    {
        var scheme = Transport switch
        {
            ""websocket"" => ""ws"",
            ""tcp"" => ""tcp"",
            _ => ""kcp""
        };

        return string.IsNullOrWhiteSpace(Path)
            ? $""{scheme}://{Host}:{Port}""
            : $""{scheme}://{Host}:{Port}{Path}"";
    }

    private static string NormalizeTransport(string? rawValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? fallback
            : rawValue.Trim().ToLowerInvariant();
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

    private static string GetDefaultPath(string transport)
    {
        return string.Equals(transport, ""websocket"", StringComparison.OrdinalIgnoreCase)
            ? ""/ws""
            : """";
    }
}

internal static class ULinkGameCheck
{
    public static int Run(ULinkGameRuntimeOptions runtime, ClusterOptions clusterOptions, string[] args)
    {
        var resolved = ToResolvedRuntime(runtime, clusterOptions);
        var validator = new ULinkGameRuntimeValidator(
            new IULinkGameValidationRule[]
            {
                new NodeIdentityRule(),
                new EndpointRule(),
                new HotfixSourceRule(),
                new ClusterServiceGraphRule()
            });
        var result = validator.Validate(resolved);

        if (args.Contains(""--json"", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    [""succeeded""] = result.Succeeded,
                    [""diagnostics""] = result.Diagnostics.Select(diagnostic => new
                    {
                        code = diagnostic.Code,
                        severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                        message = diagnostic.Message,
                        repair = diagnostic.Repair
                    })
                },
                new JsonSerializerOptions { WriteIndented = true }));
            return result.Succeeded ? 0 : 1;
        }

        return WriteText(runtime, clusterOptions, result);
    }

    private static int WriteText(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions clusterOptions,
        ULinkGameValidationResult result)
    {
        var serviceNames = clusterOptions.Services.Select(service => service.Name);
        var rpcEndpoint = clusterOptions.AdvertisedEndpoints.TryGetValue(""client"", out var clientEndpoint)
            ? clientEndpoint
            : runtime.Endpoint.ToAdvertisedEndpoint();

        Console.WriteLine(""cluster: ok single-node"");
        Console.WriteLine($""node: ok {clusterOptions.NodeId}"");
        Console.WriteLine($""services: ok {string.Join("", "", serviceNames)}"");
        var hotfixFailure = result.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == ""ULINK071"");
        if (hotfixFailure is not null)
        {
            Console.Error.WriteLine(""hotfix: failed local build output not found"");
            Console.Error.WriteLine($""fix: {hotfixFailure.Repair}"");
            return 1;
        }

        Console.WriteLine(""hotfix: ok local-build Server.Hotfix.dll"");
        Console.WriteLine(""reliable-push: ok pending limit 256, replay window 120s"");
        Console.WriteLine($""rpc: ok {rpcEndpoint}"");

        foreach (var diagnostic in result.Diagnostics.Where(diagnostic => diagnostic.Severity == ULinkGameDiagnosticSeverity.Error))
        {
            Console.Error.WriteLine($""{diagnostic.Code}: {diagnostic.Message}"");
            if (!string.IsNullOrWhiteSpace(diagnostic.Repair))
            {
                Console.Error.WriteLine($""fix: {diagnostic.Repair}"");
            }
        }

        return result.Succeeded ? 0 : 1;
    }

    private static ULinkGameResolvedRuntime ToResolvedRuntime(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions clusterOptions)
    {
        var hotfixPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            ""hotfix"",
            ""Server.Hotfix.dll"");

        return new ULinkGameResolvedRuntime(
            NodeId: new ULinkGameResolvedValue<string>(clusterOptions.NodeId, ULinkGameValueSource.Configuration, ""ULinkGame:Node:Id""),
            Endpoint: new ULinkGameResolvedEndpoint(
                Transport: new ULinkGameResolvedValue<string>(runtime.Endpoint.Transport, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Transport""),
                Host: new ULinkGameResolvedValue<string>(runtime.Endpoint.Host, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Host""),
                Port: new ULinkGameResolvedValue<int>(runtime.Endpoint.Port, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Port""),
                Path: new ULinkGameResolvedValue<string>(runtime.Endpoint.Path, ULinkGameValueSource.Configuration, ""ULinkGame:Endpoint:Path""),
                AdvertisedEndpoint: new ULinkGameResolvedValue<string>(runtime.Endpoint.ToAdvertisedEndpoint(), ULinkGameValueSource.GeneratedConvention)),
            Cluster: new ULinkGameResolvedCluster(
                Services: clusterOptions.Services
                    .Select(service => new ULinkGameResolvedClusterService(service.Kind, service.Name))
                    .ToArray(),
                AdvertisedEndpoints: clusterOptions.AdvertisedEndpoints),
            Hotfix: new ULinkGameResolvedHotfix(
                AssemblyPath: new ULinkGameResolvedValue<string>(hotfixPath, ULinkGameValueSource.GeneratedConvention),
                AssemblyFileName: new ULinkGameResolvedValue<string>(""Server.Hotfix.dll"", ULinkGameValueSource.GeneratedConvention)),
            ReliablePush: new ULinkGameResolvedReliablePush(
                StorageMode: new ULinkGameResolvedValue<string>(""InMemory"", ULinkGameValueSource.Default),
                PendingLimit: new ULinkGameResolvedValue<int>(256, ULinkGameValueSource.Default),
                ReplayWindowSeconds: new ULinkGameResolvedValue<int>(120, ULinkGameValueSource.Default),
                HasSessionIdentityResolver: true),
            Profile: ULinkGameRuntimeProfile.Development);
    }
}

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
        return ULinkGameRuntimeOptions
            .FromConfiguration(configuration)
            .ToClusterOptions(configuration);
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

    private static string RenderHotfixServiceRegistration()
    {
        return """
        var hotfixDirectory = Path.Combine(AppContext.BaseDirectory, "hotfix");
        builder.Services.AddULinkGameHotfix(
            new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, "Server.Hotfix.dll"),
            sharedAssemblyNames: ["Shared"]);
        """;
    }

    private static string RenderHotfixHelpers()
    {
        return """

        static async Task LoadInitialHotfixAsync(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var hotfix = scope.ServiceProvider.GetRequiredService<IHotfixManager>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Server.Hotfix");
            var result = await hotfix.ReloadAsync();
            if (result.Succeeded)
            {
                logger.LogInformation(
                    "Initial hotfix load succeeded from {HotfixPath} with {MethodCount} method(s).",
                    result.Current.SourcePath,
                    result.Current.Methods.Count);
                return;
            }

            logger.LogWarning(
                "Initial hotfix load failed for {HotfixPath}: {ErrorMessage}",
                result.RequestedPath,
                result.ErrorMessage);
            foreach (var diagnostic in result.Diagnostics)
            {
                logger.LogWarning("Hotfix diagnostic: {Diagnostic}", diagnostic);
            }
        }

        """;
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
            ? "builder.Services.AddSingleton(runtimeOptions.ToClusterOptions(builder.Configuration));"
            : string.Empty;
    }

    private static string RenderULinkGameCheckExit(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? """
              if (args.Contains("--ulinkgame-check", StringComparer.Ordinal))
              {
                  return ULinkGameCheck.Run(runtimeOptions, runtimeOptions.ToClusterOptions(builder.Configuration), args);
              }
              """
            : string.Empty;
    }

    private static string RenderClusterHealthCheckExit(NewCommandOptions options)
    {
        return ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile)
            ? """
              if (args.Contains("--health-check", StringComparer.Ordinal))
              {
                  return ClusterHealthCheck.Run(runtimeOptions.ToClusterOptions(builder.Configuration));
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
              ULinkGame__Endpoint__Transport: "{{TemplateText.SanitizeStringLiteral(options.Transport)}}"
              ULinkGame__Endpoint__Host: "0.0.0.0"
              ULinkGame__Endpoint__Port: "20000"
              ULinkGame__Endpoint__Path: "{{TemplateText.SanitizeStringLiteral(endpointPath)}}"
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

    public static string RenderClusterEnvExample(NewCommandOptions options)
    {
        var endpointPath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";
        var advertisedClientEndpoint = RenderAdvertisedClientEndpoint(options.Transport, "gateway", 20000, endpointPath);

        return $$"""
        # This file intentionally contains no production secrets.
        # Put node authentication and TLS material in your deployment platform secret store.
        ULINKGAME_CLUSTER_NODE_ID=gateway-1
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLUSTER=tcp://gateway:21000
        ULINKGAME_CLUSTER_ADVERTISED_ENDPOINTS_CLIENT={{advertisedClientEndpoint}}
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
