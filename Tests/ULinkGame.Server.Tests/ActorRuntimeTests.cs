using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class ActorRuntimeTests
{
    [Fact]
    public async Task AskAsync_runs_messages_serially_for_same_actor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("counter/1");

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => runtime.AskAsync<CounterActor, int>(
                id,
                static async (actor, ct) =>
                {
                    await actor.IncrementAsync(ct);
                    return actor.Value;
                },
                cancellationToken).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        var value = await runtime.AskAsync<CounterActor, int>(
            id,
            static (actor, _) => ValueTask.FromResult(actor.Value),
            cancellationToken);

        Assert.Equal(100, value);
    }

    [Fact]
    public async Task Same_actor_reentrant_call_executes_without_deadlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("reentrant/1");

        var value = await runtime.AskAsync<ReentrantActor, int>(
            id,
            static (actor, ct) => actor.CallSelfAsync(ct),
            cancellationToken);

        Assert.Equal(2, value);
    }

    [Fact]
    public async Task Same_actor_id_cannot_be_reused_for_different_actor_type()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("shared/1");

        await runtime.GetOrCreateAsync<CounterActor>(id, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runtime.GetOrCreateAsync<ReentrantActor>(id, cancellationToken));
    }

    private sealed class CounterActor : Actor
    {
        public int Value { get; private set; }

        public async ValueTask IncrementAsync(CancellationToken cancellationToken)
        {
            var before = Value;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Value = before + 1;
        }
    }

    private sealed class ReentrantActor : Actor
    {
        private int _value;

        public async ValueTask<int> CallSelfAsync(CancellationToken cancellationToken)
        {
            _value++;
            await Context.Runtime.TellAsync<ReentrantActor>(
                Context.Id,
                static (actor, _) =>
                {
                    actor._value++;
                    return ValueTask.CompletedTask;
                },
                cancellationToken);

            return _value;
        }
    }
}
