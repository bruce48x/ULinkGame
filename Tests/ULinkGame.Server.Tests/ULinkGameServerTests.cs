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

    private sealed class TestCallback
    {
        public List<(long Sequence, string Payload)> Delivered { get; } = new();
    }
}
