# Chat App Template — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace ULinkGame.Tool's default GameRules skeleton with a working distributed chat room template (join/leave/send/broadcast).

**Architecture:** ULinkRPC service/callback pattern — `IChatService` (client→server RPC) + `IChatCallback` (server→client push). Server has a singleton `ChatRoom` holding shared state and a per-connection `ChatServiceImpl` created by the RPC binder. Client has `ChatClient.cs` (RPC wrapper) + `ChatUI.cs` (UI Toolkit binding).

**Tech Stack:** C#, ULinkRPC (MemoryPack serializer), Unity UI Toolkit (UXML/USS), .NET 10 (server), .NET Standard 2.1 (shared).

---

### Task 1: Remove old GameRules templates, add Chat RPC protocol templates

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs:222-294` (remove `RenderSharedGameRules` + `RenderHotfixGameRulesSystem`)
- The rest of ToolTemplates.cs (add new methods at lines 296+)

**Files to add template methods for:**

- [ ] **Step 1: Replace `RenderSharedGameRules()` with `RenderSharedChatProtocols()`**

Replace the existing `RenderSharedGameRules` method body (lines 222-295) with a new `RenderSharedChatProtocols` that generates `Shared/Chat/ChatProtocols.cs`:

```csharp
public static string RenderSharedChatProtocols()
{
    return """
    using ULinkRPC.Core;

    namespace Shared.Chat;

    [RpcService(2, Callback = typeof(IChatCallback))]
    public interface IChatService
    {
        [RpcMethod(1)] ValueTask<ChatJoinReply> JoinAsync(ChatJoinRequest req);
        [RpcMethod(2)] ValueTask SendAsync(ChatSendRequest req);
        [RpcMethod(3)] ValueTask LeaveAsync();
    }

    [RpcCallback(typeof(IChatService))]
    public interface IChatCallback
    {
        [RpcPush(1)] void OnMessageReceived(ChatMessage msg);
        [RpcPush(2)] void OnUserJoined(ChatMember member);
        [RpcPush(3)] void OnUserLeft(string memberName);
    }
    """;
}
```

- [ ] **Step 2: Add `RenderSharedChatMessages()` — message types**

Add a new method that generates `Shared/Chat/ChatMessages.cs`:

```csharp
public static string RenderSharedChatMessages()
{
    return """
    using MemoryPack;

    namespace Shared.Chat;

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ChatJoinRequest
    {
        [MemoryPackOrder(0)] public string PlayerName { get; set; } = "";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ChatJoinReply
    {
        [MemoryPackOrder(0)] public List<ChatMember> Members { get; set; } = new();
        [MemoryPackOrder(1)] public List<ChatMessage> RecentMessages { get; set; } = new();
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ChatSendRequest
    {
        [MemoryPackOrder(0)] public string Text { get; set; } = "";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ChatMember
    {
        [MemoryPackOrder(0)] public string Name { get; set; } = "";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ChatMessage
    {
        [MemoryPackOrder(0)] public string SenderName { get; set; } = "";
        [MemoryPackOrder(1)] public string Text { get; set; } = "";
        [MemoryPackOrder(2)] public long Timestamp { get; set; }
    }
    """;
}
```

- [ ] **Step 3: Build and confirm ToolTemplateTests.cs compiles against updated templates**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: FAIL — tests reference `RenderSharedGameRules` and `RenderHotfixGameRulesSystem` which no longer exist.

- [ ] **Step 4: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs
git commit -m "feat: replace GameRules templates with Chat RPC protocol templates"
```

---

### Task 2: Add server-side Chat templates (ChatRoom singleton + ChatServiceImpl)

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`

- [ ] **Step 1: Add `RenderServerChatRoom()` — singleton room state manager**

Add a new template method that generates `Server/Server/Chat/ChatRoom.cs`:

```csharp
public static string RenderServerChatRoom()
{
    return """
    using System.Collections.Concurrent;
    using Shared.Chat;

    namespace Server.Chat;

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

            Broadcast(cb => cb.OnUserLeft(entry.Name), excludeConnectionId: null);
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
                    // callback may be stale; member is cleaned up on next RPC call
                }
            }
        }
    }
    """;
}
```

- [ ] **Step 2: Add `RenderServerChatServiceImpl()` — per-connection RPC service**

Add a new template method that generates `Server/Server/Chat/ChatServiceImpl.cs`:

```csharp
public static string RenderServerChatServiceImpl()
{
    return """
    using Shared.Chat;

    namespace Server.Chat;

    internal sealed class ChatServiceImpl : IChatService
    {
        private readonly IChatCallback _callback;
        private readonly ChatRoom _room;
        private readonly string _connectionId;

        public ChatServiceImpl(IChatCallback callback, ChatRoom room)
        {
            _callback = callback;
            _room = room;
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

        public ValueTask LeaveAsync()
        {
            _room.Leave(_connectionId);
            return ValueTask.CompletedTask;
        }
    }
    """;
}
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: FAIL — tests still reference old template methods.

- [ ] **Step 4: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs
git commit -m "feat: add server-side ChatRoom and ChatServiceImpl templates"
```

---

### Task 3: Add Hotfix ChatSystem template

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`

- [ ] **Step 1: Replace `RenderHotfixGameRulesSystem()` with `RenderHotfixChatSystem()`**

```csharp
public static string RenderHotfixChatSystem()
{
    return """
    using Shared.Chat;
    using ULinkGame.Server.Hotfix.Abstractions;

    namespace Server.Hotfix.Chat;

    [HotfixSystem]
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
    """;
}
```

Note: This uses `[HotfixSystem]` (not `[HotfixSystemOf]`) to decouple from a specific state type. The `ChatSystem` shows the hotfix pattern with a simple message sanitization extension method that can be reloaded at runtime.

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: FAIL — tests still reference old method names.

- [ ] **Step 3: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs
git commit -m "feat: replace Hotfix GameRulesSystem with ChatSystem template"
```

---

### Task 4: Add client ChatClient template

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`

- [ ] **Step 1: Add `RenderClientChatClient()`**

Generates `Client/Assets/Scripts/Chat/ChatClient.cs`:

```csharp
public static string RenderClientChatClient()
{
    return """
    using System;
    using Shared.Chat;
    using ULinkRPC.Client;

    namespace Client.Chat;

    public sealed class ChatClient : IChatCallback
    {
        private readonly RpcClient _rpcClient;
        private IChatService? _chatService;

        public event Action<ChatMessage>? OnMessageReceived;
        public event Action<ChatMember>? OnUserJoined;
        public event Action<string>? OnUserLeft;
        public event Action? OnDisconnected;

        public bool IsConnected => _rpcClient.IsConnected;

        public ChatClient(RpcClient rpcClient)
        {
            _rpcClient = rpcClient;
            _rpcClient.OnDisconnected += () => OnDisconnected?.Invoke();
        }

        public async Task ConnectAsync(string serverAddress, int port)
        {
            await _rpcClient.ConnectAsync(serverAddress, port);
            _chatService = _rpcClient.CreateService<IChatService>(this);
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
            await _chatService.LeaveAsync();
        }

        public void Disconnect()
        {
            _rpcClient.Disconnect();
        }

        void IChatCallback.OnMessageReceived(ChatMessage msg)
        {
            OnMessageReceived?.Invoke(msg);
        }

        void IChatCallback.OnUserJoined(ChatMember member)
        {
            OnUserJoined?.Invoke(member);
        }

        void IChatCallback.OnUserLeft(string memberName)
        {
            OnUserLeft?.Invoke(memberName);
        }
    }
    """;
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: PASS (no test references this new method yet).

- [ ] **Step 3: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs
git commit -m "feat: add client ChatClient RPC wrapper template"
```

---

### Task 5: Add client ChatUI template (C# + UXML + USS)

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs`

- [ ] **Step 1: Add `RenderClientChatUI()`**

Generates `Client/Assets/Scripts/Chat/ChatUI.cs`:

```csharp
public static string RenderClientChatUI()
{
    return """
    using System;
    using System.Collections.Generic;
    using Shared.Chat;
    using ULinkRPC.Client;
    using UnityEngine;
    using UnityEngine.UIElements;

    namespace Client.Chat;

    [RequireComponent(typeof(UIDocument))]
    public sealed class ChatUI : MonoBehaviour
    {
        [SerializeField] private string _serverHost = "127.0.0.1";
        [SerializeField] private int _serverPort = 20000;

        private ChatClient? _client;
        private TextField? _inputField;
        private ScrollView? _messageList;
        private Label? _onlineCount;

        private async void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _inputField = root.Q<TextField>("chat-input");
            _messageList = root.Q<ScrollView>("message-list");
            _onlineCount = root.Q<Label>("online-count");

            var sendButton = root.Q<Button>("send-button");
            sendButton?.clicked += OnSendClicked;

            _inputField?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnSendClicked();
                }
            });

            var nameField = root.Q<TextField>("name-field");
            var joinButton = root.Q<Button>("join-button");

            joinButton?.clicked += async () =>
            {
                var name = nameField?.value?.Trim();
                if (string.IsNullOrWhiteSpace(name)) return;

                var rpcClient = new RpcClient();
                _client = new ChatClient(rpcClient);
                _client.OnMessageReceived += AppendMessage;
                _client.OnUserJoined += OnUserJoined;
                _client.OnUserLeft += OnUserLeft;
                _client.OnDisconnected += () => AppendSystemMessage("Disconnected from server.");

                try
                {
                    await _client.ConnectAsync(_serverHost, _serverPort);
                    var reply = await _client.JoinAsync(name);
                    AppendSystemMessage($"Connected. {reply.Members.Count} online.");

                    foreach (var msg in reply.RecentMessages)
                    {
                        AppendMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    AppendSystemMessage($"Connection failed: {ex.Message}");
                }
            };
        }

        private async void OnSendClicked()
        {
            if (_client == null || !_client.IsConnected) return;
            var text = _inputField?.value?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            await _client.SendAsync(text);
            _inputField!.value = "";
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

        private void OnUserJoined(ChatMember member)
        {
            AppendSystemMessage($"{member.Name} joined.");
        }

        private void OnUserLeft(string memberName)
        {
            AppendSystemMessage($"{memberName} left.");
        }

        private void OnDestroy()
        {
            _client?.Disconnect();
        }
    }
    """;
}
```

- [ ] **Step 2: Add `RenderClientChatUxml()`**

Generates `Client/Assets/UI/ChatScene.uxml`:

```csharp
public static string RenderClientChatUxml()
{
    return """
    <ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
        <ui:VisualElement class="chat-container">
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
```

- [ ] **Step 3: Add `RenderClientChatUss()`**

Generates `Client/Assets/UI/ChatScene.uss`:

```csharp
public static string RenderClientChatUss()
{
    return """
    .chat-container {
        flex-grow: 1;
        background-color: rgb(30, 30, 30);
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
    .join-button {
        width: 80px;
    }
    .chat-input {
        flex-grow: 1;
    }
    .send-button {
        width: 80px;
    }
    """;
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ToolTemplates.cs
git commit -m "feat: add client ChatUI, UXML, and USS templates"
```

---

### Task 6: Update ProjectScaffolder to use new templates

**Files:**
- Modify: `src/ULinkGame.Tool/Scaffolding/ProjectScaffolder.cs`

- [ ] **Step 1: Replace old file writes with new chat file writes**

In `WriteSharedHotfixBoundaryFilesAsync` (lines 54-63), replace:

```csharp
private static Task WriteSharedHotfixBoundaryFilesAsync(string projectRoot)
{
    return Task.WhenAll(
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Shared", "Properties", "AssemblyInfo.cs"),
            ToolTemplates.RenderSharedHotfixAssemblyInfo()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Shared", "Gameplay", "GameRules.cs"),
            ToolTemplates.RenderSharedGameRules()));
}
```

With:

```csharp
private static Task WriteSharedHotfixBoundaryFilesAsync(string projectRoot)
{
    return Task.WhenAll(
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Shared", "Properties", "AssemblyInfo.cs"),
            ToolTemplates.RenderSharedHotfixAssemblyInfo()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Shared", "Chat", "ChatProtocols.cs"),
            ToolTemplates.RenderSharedChatProtocols()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Shared", "Chat", "ChatMessages.cs"),
            ToolTemplates.RenderSharedChatMessages()));
}
```

- [ ] **Step 2: Replace hotfix boundary file**

In `WriteHotfixBoundaryFilesAsync` (lines 201-206), replace:

```csharp
private static Task WriteHotfixBoundaryFilesAsync(string projectRoot)
{
    return WriteIfMissingAsync(
        Path.Combine(projectRoot, "Server", "Hotfix", "Gameplay", "GameRulesSystem.cs"),
        ToolTemplates.RenderHotfixGameRulesSystem());
}
```

With:

```csharp
private static Task WriteHotfixBoundaryFilesAsync(string projectRoot)
{
    return WriteIfMissingAsync(
        Path.Combine(projectRoot, "Server", "Hotfix", "Chat", "ChatSystem.cs"),
        ToolTemplates.RenderHotfixChatSystem());
}
```

- [ ] **Step 3: Add server chat file writes**

Add a new method call in `AugmentProjectWithULinkGameAsync` (after WriteServerConfiguratorsAsync at line 15-16):

```csharp
await WriteServerChatFilesAsync(projectRoot).ConfigureAwait(false);
```

And add the new method:

```csharp
private static Task WriteServerChatFilesAsync(string projectRoot)
{
    return Task.WhenAll(
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Server", "Server", "Chat", "ChatRoom.cs"),
            ToolTemplates.RenderServerChatRoom()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Server", "Server", "Chat", "ChatServiceImpl.cs"),
            ToolTemplates.RenderServerChatServiceImpl()));
}
```

- [ ] **Step 4: Add client chat file writes**

Add a new method call in `AugmentProjectWithULinkGameAsync` (after WriteClientPackageReferenceAsync at line 6):

```csharp
await WriteClientChatFilesAsync(projectRoot, options).ConfigureAwait(false);
```

And add the new method:

```csharp
private static Task WriteClientChatFilesAsync(string projectRoot, NewCommandOptions options)
{
    if (ProjectConventions.IsGodot(options.ClientEngine))
    {
        // Godot client scripts are C# only
        return WriteIfMissingAsync(
            Path.Combine(projectRoot, "Client", "Scripts", "Chat", "ChatClient.cs"),
            ToolTemplates.RenderClientChatClient());
    }

    return Task.WhenAll(
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Client", "Assets", "Scripts", "Chat", "ChatClient.cs"),
            ToolTemplates.RenderClientChatClient()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Client", "Assets", "Scripts", "Chat", "ChatUI.cs"),
            ToolTemplates.RenderClientChatUI()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Client", "Assets", "UI", "ChatScene.uxml"),
            ToolTemplates.RenderClientChatUxml()),
        WriteIfMissingAsync(
            Path.Combine(projectRoot, "Client", "Assets", "UI", "ChatScene.uss"),
            ToolTemplates.RenderClientChatUss()));
}
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ULinkGame.Tool/Scaffolding/ProjectScaffolder.cs
git commit -m "feat: update ProjectScaffolder to write chat template files"
```

---

### Task 7: Update tests

**Files:**
- Modify: `Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs`

- [ ] **Step 1: Update `DefaultScaffoldIncludesServerHotfixInfrastructure` test**

Replace assertions referencing `GameRules`, `GameRulesState`, `GameRulesSystem`, `Evaluate`, `EvaluateStable`, `Gameplay` namespaces with chat equivalents. The full updated test:

```csharp
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
    Assert.Contains("[HotfixSystem]", hotfixChatSystem, StringComparison.Ordinal);
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
```

- [ ] **Step 2: Add new chat-specific tests**

Add these new test methods to `ToolTemplateTests.cs`:

```csharp
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
    Assert.Contains("_room.Join", source, StringComparison.Ordinal);
    Assert.Contains("_room.Send", source, StringComparison.Ordinal);
    Assert.Contains("_room.Leave", source, StringComparison.Ordinal);
}

[Fact]
public void RenderClientChatClient_ImplementsIChatCallback()
{
    var source = ToolTemplates.RenderClientChatClient();

    Assert.Contains("class ChatClient : IChatCallback", source, StringComparison.Ordinal);
    Assert.Contains("CreateService<IChatService>", source, StringComparison.Ordinal);
    Assert.Contains("OnMessageReceived?.Invoke", source, StringComparison.Ordinal);
}

[Fact]
public void RenderClientChatUi_RequiresUiDocument()
{
    var source = ToolTemplates.RenderClientChatUI();

    Assert.Contains("RequireComponent(typeof(UIDocument))", source, StringComparison.Ordinal);
    Assert.Contains("chat-input", source, StringComparison.Ordinal);
    Assert.Contains("message-list", source, StringComparison.Ordinal);
    Assert.Contains("send-button", source, StringComparison.Ordinal);
}

[Fact]
public void RenderClientChatUxml_UsesUiNamespacePrefix()
{
    var source = ToolTemplates.RenderClientChatUxml();

    Assert.Contains("<ui:UXML", source, StringComparison.Ordinal);
    Assert.Contains("name=\"chat-input\"", source, StringComparison.Ordinal);
    Assert.Contains("name=\"message-list\"", source, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Run the tests**

```bash
dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj
```

Expected: PASS for all tests.

- [ ] **Step 4: Commit**

```bash
git add Tests/ULinkGame.Tool.Tests/ToolTemplateTests.cs
git commit -m "test: update ToolTemplateTests for chat template files"
```

---

### Task 8: Integration verification — generate a project and build it

**Files:**
- None (manual verification)

- [ ] **Step 1: Pack the tool**

Run: `dotnet pack src/ULinkGame.Tool/ULinkGame.Tool.csproj -c Release -o artifacts`
Expected: PASS.

- [ ] **Step 2: Install the tool locally**

Run: `dotnet tool install --global --add-source ./artifacts ULinkGame.Tool --version 0.3.4`
Expected: PASS or "already installed" — if already installed, run `dotnet tool update --global --add-source ./artifacts ULinkGame.Tool`

- [ ] **Step 3: Generate a test project**

Run:
```powershell
$tempDir = Join-Path $env:TEMP "ulinkgame-chat-test-$(Get-Random)"
ulinkgame-tool new --name ChatTest --output $tempDir
```
Expected: PASS — project generated.

- [ ] **Step 4: Verify generated file structure**

Check that the following files exist:
```
ChatTest/Shared/Chat/ChatProtocols.cs
ChatTest/Shared/Chat/ChatMessages.cs
ChatTest/Server/Server/Chat/ChatRoom.cs
ChatTest/Server/Server/Chat/ChatServiceImpl.cs
ChatTest/Server/Hotfix/Chat/ChatSystem.cs
ChatTest/Client/Assets/Scripts/Chat/ChatClient.cs
ChatTest/Client/Assets/Scripts/Chat/ChatUI.cs
ChatTest/Client/Assets/UI/ChatScene.uxml
ChatTest/Client/Assets/UI/ChatScene.uss
```

And verify that the OLD files do NOT exist:
```
ChatTest/Shared/Gameplay/GameRules.cs  (should NOT exist)
ChatTest/Server/Hotfix/Gameplay/GameRulesSystem.cs  (should NOT exist)
```

- [ ] **Step 5: Build the server**

Run: `dotnet build $tempDir/Server/Server/Server.csproj`
Expected: PASS — server project builds (may need `dotnet restore` first if packages aren't cached).

- [ ] **Step 6: Run --ulinkgame-check**

Run: `dotnet run --project $tempDir/Server/Server/Server.csproj -- --ulinkgame-check`
Expected: Prints cluster/node/services/hotfix/reliable-push/rpc status. Returns 0.

- [ ] **Step 7: Clean up**

```powershell
Remove-Item -Recurse -Force $tempDir
```

- [ ] **Step 8: Commit (no code changes, just verification)**

No commit needed unless step 5/6 revealed issues requiring fixes.

---

### Task 9: Final build and test run

**Files:**
- None (verification)

- [ ] **Step 1: Run full test suite for ULinkGame.Tool**

Run: `dotnet test Tests/ULinkGame.Tool.Tests/ULinkGame.Tool.Tests.csproj`
Expected: ALL tests PASS.

- [ ] **Step 2: Run full solution build**

Run: `dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj`
Expected: PASS, no warnings.

- [ ] **Step 3: Final commit if any last changes**

```bash
git status
git diff
```
