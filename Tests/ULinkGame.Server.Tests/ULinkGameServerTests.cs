using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Abstractions;
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class ULinkGameServerTests
{
    [Fact]
    public async Task MainEntryStartsSessionBindsEndpointAndReturnsCallback()
    {
        var services = new ServiceCollection();
        services.AddULinkGameServer();
        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IULinkGameServer>();
        var callback = new TestCallback();

        var session = await server.StartSessionAsync(
            "player-a",
            "control",
            "connection-a",
            callback,
            TestContext.Current.CancellationToken);

        var resolved = await server.GetCallbackAsync<TestCallback>(
            session,
            "control",
            TestContext.Current.CancellationToken);

        Assert.Same(callback, resolved);
    }

    [Fact]
    public async Task MainEntryPublishesReplaysAndAcknowledgesReliablePush()
    {
        var services = new ServiceCollection();
        services.AddULinkGameServer();
        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IULinkGameServer>();
        var session = new GameSessionKey("player-a", "session-a", 1);
        var delivered = new List<ReliablePushRecord>();

        await server.PublishReliablePushAsync(
            session,
            "matched",
            "payload",
            record =>
            {
                delivered.Add(record);
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        var replayedBeforeAck = new List<ReliablePushRecord>();
        await server.ReplayReliablePushAsync(
            session,
            record =>
            {
                replayedBeforeAck.Add(record);
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        var outcome = await server.AckReliablePushAsync(
            session,
            session,
            1,
            TestContext.Current.CancellationToken);
        var replayedAfterAck = new List<ReliablePushRecord>();
        await server.ReplayReliablePushAsync(
            session,
            record =>
            {
                replayedAfterAck.Add(record);
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Single(delivered);
        Assert.Single(replayedBeforeAck);
        Assert.Equal(ReliablePushAckStatus.Accepted, outcome.Status);
        Assert.Empty(replayedAfterAck);
    }

    [Fact]
    public async Task MainEntryPublishesTypedReliablePushThroughEndpointCallback()
    {
        var services = new ServiceCollection();
        services.AddULinkGameServer();
        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IULinkGameServer>();
        var callback = new TestCallback();
        var session = await server.StartSessionAsync(
            "player-a",
            GameEndpointName.Control,
            "connection-a",
            callback,
            TestContext.Current.CancellationToken);

        var sequence = await server.PublishReliablePushAsync<TestCallback, string>(
            session,
            GameEndpointName.Control,
            "matched",
            "payload",
            static (target, reliableSequence, payload, _) =>
            {
                target.Delivered.Add((reliableSequence.Value, payload));
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);
        await server.ReplayReliablePushAsync<TestCallback, string>(
            session,
            GameEndpointName.Control,
            "matched",
            static (target, reliableSequence, payload, _) =>
            {
                target.Delivered.Add((reliableSequence.Value, payload));
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, sequence);
        Assert.Equal(new[] { (1L, "payload"), (1L, "payload") }, callback.Delivered);
    }

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

    [Fact]
    public async Task TerminateSessionUsesControlEndpointByDefaultAndPreservesResumeOutcome()
    {
        var services = new ServiceCollection();
        var closer = new RecordingEndpointCloser();
        services.AddSingleton<IGameSessionEndpointCloser>(closer);
        services.AddULinkGameServer();
        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IULinkGameServer>();
        var callback = new TerminationCallback();
        var session = await server.StartSessionAsync(
            "player-a",
            GameEndpointName.Control,
            "connection-a",
            callback,
            TestContext.Current.CancellationToken);

        await server.TerminateSessionAsync(
            session,
            SessionTerminationReason.ReplacedByNewLogin,
            message: "Duplicate login.",
            cancellationToken: TestContext.Current.CancellationToken);
        var resume = await server.ResumeSessionAsync(
            new GameSessionResumeRequest(session),
            GameEndpointName.Control,
            "connection-b",
            callback,
            TestContext.Current.CancellationToken);

        Assert.NotNull(callback.Notice);
        Assert.Equal(session, callback.Notice.Session);
        Assert.Equal(SessionTerminationReason.ReplacedByNewLogin, callback.Notice.Reason);
        Assert.Equal("Duplicate login.", callback.Notice.Message);
        var closed = Assert.Single(closer.Closed);
        Assert.Equal(new SessionEndpointKey(session, GameEndpointName.Control), closed.Endpoint);
        Assert.Equal("connection-a", closed.ConnectionId);
        Assert.Same(callback.Notice, closed.Notice);
        Assert.Equal(SessionResumeStatus.Terminated, resume.Status);
        Assert.Same(callback.Notice, resume.Termination);
    }

    [Fact]
    public async Task TerminateSessionClosesEndpointWhenNotificationTimesOut()
    {
        var services = new ServiceCollection();
        var closer = new RecordingEndpointCloser();
        services.AddSingleton<IGameSessionEndpointCloser>(closer);
        services.AddULinkGameServer();
        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<IULinkGameServer>();
        var callback = new HangingTerminationCallback();
        var session = await server.StartSessionAsync(
            "player-a",
            GameEndpointName.Control,
            "connection-a",
            callback,
            TestContext.Current.CancellationToken);

        await server.TerminateSessionAsync(
            session,
            SessionTerminationReason.Policy,
            options: new SessionTerminationOptions
            {
                NotifyTimeout = TimeSpan.FromMilliseconds(10)
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var closed = Assert.Single(closer.Closed);
        Assert.Equal(new SessionEndpointKey(session, GameEndpointName.Control), closed.Endpoint);
        Assert.Equal("connection-a", closed.ConnectionId);
        Assert.NotNull(callback.Notice);
        Assert.Same(callback.Notice, closed.Notice);
    }

    private sealed class TestCallback
    {
        public List<(long Sequence, string Payload)> Delivered { get; } = new();
    }

    private sealed class TerminationCallback : IULinkGameSessionCallback
    {
        public SessionTerminationNotice? Notice { get; private set; }

        public ValueTask OnSessionTerminatedAsync(
            SessionTerminationNotice notice,
            CancellationToken cancellationToken = default)
        {
            Notice = notice;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HangingTerminationCallback : IULinkGameSessionCallback
    {
        public SessionTerminationNotice? Notice { get; private set; }

        public ValueTask OnSessionTerminatedAsync(
            SessionTerminationNotice notice,
            CancellationToken cancellationToken = default)
        {
            Notice = notice;
            return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        }
    }

    private sealed class RecordingEndpointCloser : IGameSessionEndpointCloser
    {
        public List<(SessionEndpointKey Endpoint, string ConnectionId, SessionTerminationNotice Notice)> Closed { get; } = new();

        public ValueTask CloseEndpointAsync(
            SessionEndpointKey endpoint,
            string connectionId,
            SessionTerminationNotice notice,
            CancellationToken cancellationToken = default)
        {
            Closed.Add((endpoint, connectionId, notice));
            return ValueTask.CompletedTask;
        }
    }
}
