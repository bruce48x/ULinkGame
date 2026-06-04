# Session Termination Notice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fixed, minimal ULinkGame session termination notice protocol with server-side notify-before-close orchestration and client terminal-state helpers.

**Architecture:** Shared termination contracts live in `ULinkGame.Abstractions`. `ULinkGame.Server` commits terminal session state, sends `IULinkGameSessionCallback.OnSessionTerminatedAsync` on the control endpoint by default, waits only a bounded timeout, then asks an endpoint closer to close the stored connection. `ULinkGame.Client` applies the fixed notice exactly once and exposes the terminal notice in its snapshot.

**Tech Stack:** C#/.NET, xUnit, existing ULinkGame session directory, client session controller, Microsoft.Extensions.DependencyInjection.

---

### Task 1: Shared Termination Contracts

**Files:**
- Create: `src/ULinkGame.Abstractions/Sessions/SessionTerminationReason.cs`
- Create: `src/ULinkGame.Abstractions/Sessions/SessionTerminationNotice.cs`
- Create: `src/ULinkGame.Abstractions/Sessions/IULinkGameSessionCallback.cs`
- Modify: `src/ULinkGame.Abstractions/Sessions/SessionResumeStatus.cs`
- Modify: `src/ULinkGame.Abstractions/Sessions/SessionResumeDecision.cs`

- [ ] **Step 1: Write the failing contract compile test**

Add this test to `Tests/ULinkGame.Server.Tests/ULinkGameServerTests.cs`:

```csharp
[Fact]
public void SessionTerminationNoticeCarriesFixedFrameworkReason()
{
    var session = new GameSessionKey("player-a", "session-a", 1);
    var issuedAt = new DateTimeOffset(2026, 6, 4, 1, 2, 3, TimeSpan.Zero);

    var notice = new SessionTerminationNotice(
        session,
        SessionTerminationReason.ReplacedByNewLogin,
        "This account logged in elsewhere.",
        issuedAt);

    Assert.Equal(session, notice.Session);
    Assert.Equal(SessionTerminationReason.ReplacedByNewLogin, notice.Reason);
    Assert.Equal("This account logged in elsewhere.", notice.Message);
    Assert.Equal(issuedAt, notice.IssuedAt);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter SessionTerminationNoticeCarriesFixedFrameworkReason`

Expected: FAIL to compile because `SessionTerminationNotice` and `SessionTerminationReason` do not exist.

- [ ] **Step 3: Add minimal shared contracts**

Create `SessionTerminationReason.cs`:

```csharp
namespace ULinkGame.Abstractions
{
    public enum SessionTerminationReason
    {
        ReplacedByNewLogin,
        ServerShutdown,
        Maintenance,
        Unauthorized,
        Policy,
        StateLost,
        Application
    }
}
```

Create `SessionTerminationNotice.cs`:

```csharp
using System;

namespace ULinkGame.Abstractions
{
    public sealed class SessionTerminationNotice
    {
        public SessionTerminationNotice(
            GameSessionKey session,
            SessionTerminationReason reason,
            string? message = null,
            DateTimeOffset? issuedAt = null)
        {
            Session = session;
            Reason = reason;
            Message = message;
            IssuedAt = issuedAt ?? DateTimeOffset.UtcNow;
        }

        public GameSessionKey Session { get; }

        public SessionTerminationReason Reason { get; }

        public string? Message { get; }

        public DateTimeOffset IssuedAt { get; }
    }
}
```

Create `IULinkGameSessionCallback.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Abstractions
{
    public interface IULinkGameSessionCallback
    {
        ValueTask OnSessionTerminatedAsync(
            SessionTerminationNotice notice,
            CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter SessionTerminationNoticeCarriesFixedFrameworkReason`

Expected: PASS.

### Task 2: Terminal Session State In Directory

**Files:**
- Modify: `src/ULinkGame.Server/Sessions/IGameSessionDirectory.cs`
- Modify: `src/ULinkGame.Server/Sessions/InMemoryGameSessionDirectory.cs`
- Modify: `src/ULinkGame.Abstractions/Sessions/SessionResumeStatus.cs`
- Modify: `src/ULinkGame.Abstractions/Sessions/SessionResumeDecision.cs`
- Test: `Tests/ULinkGame.Server.Tests/GameSessionDirectoryTests.cs`

- [ ] **Step 1: Write failing tests**

Add these tests:

```csharp
[Fact]
public async Task TerminatedSessionResumesAsTerminated()
{
    var directory = new InMemoryGameSessionDirectory();
    var session = await directory.StartNewSessionAsync("player-a", TestContext.Current.CancellationToken);
    var notice = new SessionTerminationNotice(session, SessionTerminationReason.Policy, "Removed.");

    await directory.MarkSessionTerminatedAsync(session, notice, keepForResume: true, TestContext.Current.CancellationToken);

    var decision = await directory.TryResumeAsync(session, TestContext.Current.CancellationToken);

    Assert.Equal(SessionResumeStatus.Terminated, decision.Status);
    Assert.Same(notice, decision.Termination);
}

[Fact]
public async Task BindingEndpointAfterTerminationIsRejected()
{
    var directory = new InMemoryGameSessionDirectory();
    var session = await directory.StartNewSessionAsync("player-a", TestContext.Current.CancellationToken);
    var notice = new SessionTerminationNotice(session, SessionTerminationReason.Policy);

    await directory.MarkSessionTerminatedAsync(session, notice, keepForResume: true, TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(() => directory
        .BindEndpointAsync(new SessionEndpointKey(session, "control"), "connection-a", new Callback("control"), TestContext.Current.CancellationToken)
        .AsTask());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter "TerminatedSessionResumesAsTerminated|BindingEndpointAfterTerminationIsRejected"`

Expected: FAIL to compile because directory termination APIs and resume status do not exist.

- [ ] **Step 3: Implement terminal state**

Add `Terminated` to `SessionResumeStatus`. Add `SessionTerminationNotice? Termination` and static `Terminated(SessionTerminationNotice notice)` to `SessionResumeDecision`. Add `MarkSessionTerminatedAsync` to `IGameSessionDirectory` and implement it in `InMemoryGameSessionDirectory` by storing the notice on the current owner state. Update `TryResumeAsync` to return `SessionResumeDecision.Terminated(state.Termination)` when present. Update `BindEndpointAsync` to reject a terminated current session.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter "TerminatedSessionResumesAsTerminated|BindingEndpointAfterTerminationIsRejected"`

Expected: PASS.

### Task 3: Server TerminateSessionAsync Orchestration

**Files:**
- Create: `src/ULinkGame.Server/Sessions/SessionTerminationOptions.cs`
- Create: `src/ULinkGame.Server/Sessions/GameSessionEndpointBinding.cs`
- Create: `src/ULinkGame.Server/Sessions/IGameSessionEndpointCloser.cs`
- Create: `src/ULinkGame.Server/Sessions/NoopGameSessionEndpointCloser.cs`
- Modify: `src/ULinkGame.Server/Sessions/IGameSessionDirectory.cs`
- Modify: `src/ULinkGame.Server/Sessions/InMemoryGameSessionDirectory.cs`
- Modify: `src/ULinkGame.Server/Sessions/SessionServiceCollectionExtensions.cs`
- Modify: `src/ULinkGame.Server/IULinkGameServer.cs`
- Modify: `src/ULinkGame.Server/ULinkGameServer.cs`
- Test: `Tests/ULinkGame.Server.Tests/ULinkGameServerTests.cs`

- [ ] **Step 1: Write failing server orchestration tests**

Add tests that bind a callback implementing `IULinkGameSessionCallback`, call `TerminateSessionAsync(session, SessionTerminationReason.ReplacedByNewLogin, message: "Duplicate login.")`, assert the callback received a `SessionTerminationNotice`, assert the fake closer received the control endpoint and connection id, and assert `ResumeSessionAsync` returns `SessionResumeStatus.Terminated`.

Add a second test with a callback that never completes and `NotifyTimeout = TimeSpan.FromMilliseconds(10)`; assert the fake closer is still called.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter "TerminateSession"`

Expected: FAIL to compile because `TerminateSessionAsync`, endpoint closer, options, and endpoint binding APIs do not exist.

- [ ] **Step 3: Implement minimal orchestration**

Add default and explicit endpoint overloads to `IULinkGameServer`. In `ULinkGameServer`, resolve the current `GameSessionEndpointBinding<IULinkGameSessionCallback>` from the directory, mark the session terminated, attempt `OnSessionTerminatedAsync` within `NotifyTimeout`, swallow notification failures after terminal state is committed, and call `IGameSessionEndpointCloser.CloseEndpointAsync` when a binding exists. Register `NoopGameSessionEndpointCloser` with `TryAddSingleton`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj --filter "TerminateSession"`

Expected: PASS.

### Task 4: Client Terminal State Helper

**Files:**
- Modify: `src/ULinkGame.Client/Runtime/Sessions/ClientSessionPhase.cs`
- Modify: `src/ULinkGame.Client/Runtime/Sessions/ClientSessionSnapshot.cs`
- Modify: `src/ULinkGame.Client/Runtime/Sessions/ClientSessionController.cs`
- Modify: `src/ULinkGame.Client/ULinkGameClient.cs`
- Test: `Tests/ULinkGame.Client.Tests/ClientSessionControllerTests.cs`
- Test: `Tests/ULinkGame.Client.Tests/ULinkGameClientTests.cs`

- [ ] **Step 1: Write failing client tests**

Add tests asserting `ApplySessionTerminationNotice(notice)` moves an active matching session to `ClientSessionPhase.Terminated`, clears reliable state, stores the notice, and ignores a stale notice whose `Session` does not match the current session.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Tests/ULinkGame.Client.Tests/ULinkGame.Client.Tests.csproj --filter "Termination|Terminated"`

Expected: FAIL to compile because client termination APIs and phase do not exist.

- [ ] **Step 3: Implement minimal client state**

Add `Terminated` to `ClientSessionPhase`. Add `SessionTerminationNotice? Termination` to `ClientSessionSnapshot`. Add `ApplySessionTerminationNotice(SessionTerminationNotice notice)` to `ClientSessionController` and `ULinkGameClient`. Matching active/reconnecting/refresh-required sessions become `Terminated` with null session and zero sequence; stale notices are ignored.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/ULinkGame.Client.Tests/ULinkGame.Client.Tests.csproj --filter "Termination|Terminated"`

Expected: PASS.

### Task 5: Full Verification And Package Metadata

**Files:**
- Modify package versions if shippable package content changed.
- Modify `CHANGELOG.md` if package versions are bumped.

- [ ] **Step 1: Run focused tests**

Run:

```powershell
dotnet test Tests/ULinkGame.Server.Tests/ULinkGame.Server.Tests.csproj
dotnet test Tests/ULinkGame.Client.Tests/ULinkGame.Client.Tests.csproj
```

Expected: PASS.

- [ ] **Step 2: Run broad framework tests**

Run: `dotnet test Tests/tests.slnx`

Expected: PASS.

- [ ] **Step 3: Update package metadata**

If the implementation changes shippable files under `src/ULinkGame.Abstractions`, `src/ULinkGame.Server`, or `src/ULinkGame.Client`, bump their package versions and update `CHANGELOG.md` according to `CONTRIBUTING.md`.
