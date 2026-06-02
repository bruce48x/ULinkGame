# ULinkGame.Tool Default Template: Distributed Chat App

## Status

Proposed — awaiting review before implementation planning.

## Problem

`ulinkgame-tool new` currently generates a `GameRules` skeleton (a bare `[HotfixState] GameRulesState` with one `Evaluate` dispatch method and a matching `GameRulesSystem`). This sample is too abstract to demonstrate what ULinkGame is or why someone would use it. The generated project builds but doesn't *do* anything visible.

A new user running `ulinkgame-tool new` should get a project they can run and immediately understand — a distributed chat room that works out of the box on a single node, with architecture that naturally extends to a multi-node cluster.

## Scope

Target: **ULinkGame.Tool (src/ULinkGame.Tool/)** — templating layer only.

What changes:
- `ToolTemplates.cs` — replace GameRules templates with Chat templates (C# source, .csproj items, UXML/USS)
- `ProjectScaffolder.cs` — adjust file paths for new template structure, add client script/UI file writes
- Client output gains script files and UI assets (currently only packages.config + import guard)

What does NOT change:
- Server entry point (`Program.cs`), hosting helpers, solution structure, appsettings philosophy
- `ToolModels.cs`, `CliApplication.cs`, `ToolProcessRunner.cs`, package versions
- Underlying ULinkGame framework packages
- Existing test coverage for tool

## Design

### Architecture

```
Client (Unity)                          Server (.NET 10)
┌──────────────────────┐          ┌──────────────────────────────────────┐
│  ChatClient.cs       │  RPC     │  ChatRoomService (DI registered)     │
│  ChatUI.cs           │◄───────►│    │ routes to ChatRoomActor          │
│  ChatScene.uxml      │  Push   │  ┌─────────────────────────────────┐ │
│  ChatScene.uss       │         │  │ ChatRoomActor                   │ │
└──────────────────────┘         │  │   State: ChatRoomState          │ │
                                 │  │   - Members[]                   │ │
                                 │  │   - RecentMessages[] (ring)     │ │
                                 │  │   Methods:                      │ │
                                 │  │     JoinRoom / LeaveRoom        │ │
                                 │  │     SendMessage / Broadcast     │ │
                                 │  └─────────────────────────────────┘ │
                                 │                  │                   │
                                 │  ┌───────────────▼────────────────┐ │
                                 │  │ ChatSystem (Hotfix)            │ │
                                 │  │   [HotfixSystemOf(             │ │
                                 │  │    typeof(ChatRoomState))]     │ │
                                 │  │   JoinRoomStable → dispatch    │ │
                                 │  │   SendMessageStable → dispatch │ │
                                 │  └────────────────────────────────┘ │
                                 └──────────────────────────────────────┘
```

Default topology: single process runs node-directory, route-directory, and gateway all in-process (same as current). ChatRoomActor is a singleton service actor within this process. Multi-node expansion: extract services to separate processes via configuration — no code change required.

### Data Model (Shared/Chat/)

`ChatRoomState.cs` — HotfixState:
```
ChatRoomState
  RoomId: string
  Members: List<ChatMember>     // { SessionId, Name }
  RecentMessages: Queue<ChatMessage>  // ring buffer, max 100
```

`ChatMessages.cs` — wire types:
```
ChatMember       → { SessionId, Name }
ChatMessage      → { SenderName, Text, Timestamp }
JoinRoomReply    → { Ok, Members: ChatMember[], RecentMessages: ChatMessage[] }
```

`ChatProtocols.cs` — RPC contracts:
```
IChatRoomService (client → server):
  Task<JoinRoomReply> JoinRoom(long sessionId, string playerName)
  Task LeaveRoom(long sessionId)
  Task SendMessage(long sessionId, string text)

IChatClientCallback (server → client push):
  void OnMessageReceived(ChatMessage msg)
  void OnUserJoined(ChatMember member)
  void OnUserLeft(string memberName)
```

### Server Actors (Server/Chat/)

`ChatRoomActor.cs`:
- Holds `ChatRoomState` as in-memory state
- `JoinRoom`: creates member entry, pushes `OnUserJoined` to all, returns `JoinRoomReply` with member list + history
- `LeaveRoom`: removes member, pushes `OnUserLeft` to all
- `SendMessage`: enqueues message (trim to 100), pushes `OnMessageReceived` to all
- Server push uses ULinkGame `ISession` API to send to each connected client

`ChatRoomService.cs`:
- DI-registered service that locates or creates the ChatRoomActor
- Single instance by default (hardcoded room ID); multi-room is a natural extension

### Hotfix Boundary (Hotfix/Chat/)

`ChatSystem.cs` — replaces `GameRulesSystem.cs`:
```csharp
[HotfixSystemOf(typeof(ChatRoomState))]
public static class ChatSystem
{
    // JoinRoomStable → forwards to hotfix logic
    // SendMessageStable → forwards to hotfix logic
    // (same dispatch pattern as current GameRulesSystem)
}
```

### Client (Client/Assets/Scripts/Chat/)

| File | Purpose |
|------|---------|
| `ChatClient.cs` | Wraps ULinkRPC client connection. Connect/Disconnect. Invokes IChatRoomService RPC. Exposes C# events for UI binding. Auto-reconnect on disconnect. |
| `ChatUI.cs` | MonoBehaviour. Binds UIDocument. Reads input on Enter. Subscribes to ChatClient events. Appends messages to scroll view. Colors system messages differently. |

### Client UI (Client/Assets/UI/)

| File | Purpose |
|------|---------|
| `ChatScene.uxml` | Single VisualElement tree: header bar with room name + online count, scrollable message list, bottom row with text field + send button |
| `ChatScene.uss` | Styles: system messages in grey italic, own messages right-aligned, others left-aligned with sender name prefix. Dark theme. |

UI Toolkit chosen over UGUI because `.uxml`/`.uss` are plain text files that embed cleanly in template strings and have no asset binding issues.

### File Mapping: Old → New

| Deleted | Added |
|---------|-------|
| `Shared/Gameplay/GameRules.cs` | `Shared/Chat/ChatRoomState.cs` |
| | `Shared/Chat/ChatMessages.cs` |
| | `Shared/Chat/ChatProtocols.cs` |
| `Shared/Properties/AssemblyInfo.cs` | `Shared/Properties/AssemblyInfo.cs` (unchanged) |
| `Server/Hotfix/Gameplay/GameRulesSystem.cs` | `Server/Hotfix/Chat/ChatSystem.cs` |
| (none) | `Server/Server/Chat/ChatRoomActor.cs` |
| (none) | `Server/Server/Chat/ChatRoomService.cs` |
| (none) | `Client/Assets/Scripts/Chat/ChatClient.cs` |
| (none) | `Client/Assets/Scripts/Chat/ChatUI.cs` |
| (none) | `Client/Assets/UI/ChatScene.uxml` |
| (none) | `Client/Assets/UI/ChatScene.uss` |
| `Client/Assets/Editor/ULinkGameNuGetPackageImportGuard.cs` | (unchanged) |
| `Client/Assets/packages.config` | (unchanged) |

### Template String Strategy

All templates remain as embedded C# string constants in `ToolTemplates.cs`, following the existing pattern. No external template files. Each template method:
- Accepts `NewCommandOptions` where needed (name, transport, etc.)
- Returns a string to be written to disk by `ProjectScaffolder`

New template methods to add to `ToolTemplates.cs`:
- `RenderSharedChatState()` / `RenderSharedChatMessages()` / `RenderSharedChatProtocols()`
- `RenderServerChatRoomActor()` / `RenderServerChatRoomService()`
- `RenderHotfixChatSystem()`
- `RenderClientChatClient()` / `RenderClientChatUI()`
- `RenderClientChatUxml()` / `RenderClientChatUss()`

### ProjectScaffolder Changes

In `AugmentProjectWithULinkGameAsync()`:
- Replace `WriteGameRulesAsync()` with `WriteChatFilesAsync()` — writes all 3 Shared chat files
- Replace `WriteGameRulesSystemAsync()` with `WriteChatSystemAsync()`
- Add `WriteServerChatFilesAsync()` — writes ChatRoomActor.cs, ChatRoomService.cs
- Add `WriteClientChatFilesAsync()` — writes ChatClient.cs, ChatUI.cs, .uxml, .uss
- Client file paths differ by engine: `Assets/Scripts/Chat/` for Unity, `Scripts/Chat/` for Godot

### What stays unchanged

- `Program.cs` — server entry point already supports all hosting patterns needed
- `appsettings.json` — same compact config surface
- `Hosting/*` — RPC options, cluster options, health check unchanged
- `Server.slnx` — still references the same three projects
- `Server.csproj` / `Hotfix.csproj` — package references unchanged
- Docker/compose templates — unchanged
- `ToolModels.cs` — no new CLI flags needed

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Actor APIs in framework may be unstable | ChatRoomActor uses only stable ULinkActor primitives (state, timer, local call). No distributed actor routing in v1 template. |
| UI Toolkit may not be familiar to Unity users | UX is simple (1 scene, 3 elements). UGUI alternative kept as future option. |
| Template string size grows | ToolTemplates.cs already ~1377 lines. New chat templates estimated ~400 lines additional. Acceptable for v1. |
| Godot client path differences | ChatClient.cs is engine-neutral (pure C#). Only ChatUI.cs and UI assets have Unity-specific UI Toolkit binding. Godot UI deferred. |

## Deliverables

1. Updated `ToolTemplates.cs` — 10 new template methods, 3 removed
2. Updated `ProjectScaffolder.cs` — file write paths adjusted
3. Updated `ToolText.cs` — if any new user-facing strings needed (likely minimal)
4. Updated `ULinkGame.Tool.Tests` — test assertions updated for new file names/content
5. Manual verification: run `ulinkgame-tool new --name ChatTest`, `dotnet build` server, open client scene
