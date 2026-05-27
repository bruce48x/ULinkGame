using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class ActorRuntimeTests
{
    [Fact]
    public void AddULinkGameServerActors_registers_ULinkActor_backed_runtime()
    {
        using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();

        Assert.IsType<ULinkActorRuntime>(provider.GetRequiredService<IActorRuntime>());
    }

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

    [Fact]
    public async Task Slow_message_diagnostic_maps_ULinkActor_event_to_ULinkGame_actor_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var observed = new TaskCompletionSource<ActorSlowMessageDiagnostic>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var id = ActorId.From("slow/1");

        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors(options =>
            {
                options.SlowMessageThreshold = TimeSpan.FromMilliseconds(1);
                options.SlowMessageHandler = diagnostic => observed.TrySetResult(diagnostic);
            })
            .BuildServiceProvider();

        var runtime = provider.GetRequiredService<IActorRuntime>();

        await runtime.TellAsync<SlowActor>(
            id,
            static (actor, ct) => actor.DelayAsync(TimeSpan.FromMilliseconds(50), ct),
            cancellationToken);

        var diagnostic = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Equal(id, diagnostic.ActorId);
        Assert.True(diagnostic.Elapsed >= TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Call_timeout_diagnostic_maps_reason_and_actor_ids()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var observed = new TaskCompletionSource<ActorCallTimeoutDiagnostic>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var id = ActorId.From("timeout/1");

        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors(options =>
            {
                options.CallTimeout = TimeSpan.FromMilliseconds(20);
                options.CallTimeoutHandler = diagnostic => observed.TrySetResult(diagnostic);
            })
            .BuildServiceProvider();

        var runtime = provider.GetRequiredService<IActorRuntime>();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await runtime.AskAsync<SlowActor, int>(
                id,
                static async (actor, ct) =>
                {
                    await actor.DelayAsync(TimeSpan.FromMilliseconds(200), ct);
                    return 1;
                },
                cancellationToken));

        var diagnostic = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Equal(id, diagnostic.Target);
        Assert.Equal(ActorCallTimeoutReason.ResponseTimeout, diagnostic.Reason);
    }

    [Fact]
    public async Task TryTell_returns_mailbox_full_without_waiting_for_capacity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = ActorId.From("backpressure/1");

        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors(options => options.MailboxCapacity = 2)
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();

        var blocking = runtime.TellAsync<BlockingActor>(
            id,
            (actor, ct) => actor.BlockAsync(entered, release.Task, ct),
            cancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        var first = runtime.TryTell<BlockingActor>(
            id,
            static (actor, _) =>
            {
                actor.Count++;
                return ValueTask.CompletedTask;
            },
            cancellationToken);
        var second = runtime.TryTell<BlockingActor>(
            id,
            static (actor, _) =>
            {
                actor.Count++;
                return ValueTask.CompletedTask;
            },
            cancellationToken);

        release.SetResult();
        await blocking;

        var count = await runtime.AskAsync<BlockingActor, int>(
            id,
            static (actor, _) => ValueTask.FromResult(actor.Count),
            cancellationToken);

        Assert.Equal(ActorTellResult.Accepted, first);
        Assert.Equal(ActorTellResult.MailboxFull, second);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StopAsync_drains_and_removes_actor_from_runtime_registry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("stop/1");

        await runtime.TellAsync<CounterActor>(
            id,
            static async (actor, ct) =>
            {
                await actor.IncrementAsync(ct);
            },
            cancellationToken);

        await runtime.StopAsync(id);

        var value = await runtime.AskAsync<CounterActor, int>(
            id,
            static (actor, _) => ValueTask.FromResult(actor.Value),
            cancellationToken);

        Assert.Equal(0, value);
    }

    [Fact]
    public async Task StopAsync_with_timeout_returns_timed_out_when_actor_does_not_drain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("stop-timeout/1");

        var blocking = runtime.TellAsync<BlockingActor>(
            id,
            (actor, ct) => actor.BlockAsync(entered, release.Task, ct),
            cancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        var outcome = await runtime.StopAsync(id, TimeSpan.FromMilliseconds(20));

        release.SetResult();
        await blocking;

        Assert.Equal(ActorStopOutcome.TimedOut, outcome);
    }

    [Fact]
    public async Task TryGetMailboxMetrics_returns_ULinkGame_owned_metrics_snapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = ActorId.From("metrics/1");

        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors(options => options.MailboxCapacity = 3)
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();

        Assert.False(runtime.TryGetMailboxMetrics(id, out _));

        var blocking = runtime.TellAsync<BlockingActor>(
            id,
            (actor, ct) => actor.BlockAsync(entered, release.Task, ct),
            cancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        var tellResult = runtime.TryTell<BlockingActor>(
            id,
            static (actor, _) =>
            {
                actor.Count++;
                return ValueTask.CompletedTask;
            },
            cancellationToken);

        Assert.True(runtime.TryGetMailboxMetrics(id, out var metrics));
        Assert.Equal(ActorTellResult.Accepted, tellResult);
        Assert.Equal(3, metrics.Capacity);
        Assert.True(metrics.QueuedCount >= 1);
        Assert.True(metrics.EnqueuedCount >= 2);
        Assert.False(metrics.IsCompleted);

        release.SetResult();
        await blocking;
    }

    [Fact]
    public async Task RegisterTimer_dispatches_ticks_through_actor_mailbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("timer/1");

        await using var timer = runtime.RegisterTimer<TimerActor>(
            id,
            TimeSpan.FromMilliseconds(10),
            null,
            static (actor, _) =>
            {
                actor.Ticks++;
                return ValueTask.CompletedTask;
            });

        var ticks = await WaitForAsync(
            async () => await runtime.AskAsync<TimerActor, int>(
                id,
                static (actor, _) => ValueTask.FromResult(actor.Ticks),
                cancellationToken),
            value => value >= 1,
            cancellationToken);

        Assert.True(ticks >= 1);
    }

    [Fact]
    public async Task StopAsync_disposes_registered_timer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("timer-stop/1");

        await using var timer = runtime.RegisterTimer<TimerActor>(
            id,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10),
            static (actor, _) =>
            {
                actor.Ticks++;
                return ValueTask.CompletedTask;
            });

        await WaitForAsync(
            async () => await runtime.AskAsync<TimerActor, int>(
                id,
                static (actor, _) => ValueTask.FromResult(actor.Ticks),
                cancellationToken),
            value => value >= 1,
            cancellationToken);

        await runtime.StopAsync(id);
        await Task.Delay(80, cancellationToken);

        var recreatedTicks = await runtime.AskAsync<TimerActor, int>(
            id,
            static (actor, _) => ValueTask.FromResult(actor.Ticks),
            cancellationToken);

        Assert.Equal(0, recreatedTicks);
    }

    [Fact]
    public async Task StopAsync_prevents_queued_timer_registration_from_surviving_stop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("timer-stop-race/1");

        await using var timer = runtime.RegisterTimer<TimerActor>(
            id,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10),
            static (actor, _) =>
            {
                actor.Ticks++;
                return ValueTask.CompletedTask;
            });

        await runtime.StopAsync(id);
        await Task.Delay(80, cancellationToken);

        var recreatedTicks = await runtime.AskAsync<TimerActor, int>(
            id,
            static (actor, _) => ValueTask.FromResult(actor.Ticks),
            cancellationToken);

        Assert.Equal(0, recreatedTicks);
    }

    [Fact]
    public async Task StopAsync_runs_actor_deactivation_hook()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DeactivationActor.Deactivations = 0;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("deactivate/1");

        await runtime.GetOrCreateAsync<DeactivationActor>(id, cancellationToken);

        await runtime.StopAsync(id);

        Assert.Equal(1, DeactivationActor.Deactivations);
    }

    [Fact]
    public async Task RuntimeDispose_does_not_run_actor_deactivation_hook()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DeactivationActor.Deactivations = 0;
        await using (var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider())
        {
            var runtime = provider.GetRequiredService<IActorRuntime>();
            await runtime.GetOrCreateAsync<DeactivationActor>(
                ActorId.From("dispose-no-deactivate/1"),
                cancellationToken);
        }

        Assert.Equal(0, DeactivationActor.Deactivations);
    }

    [Fact]
    public async Task StopAsync_with_timeout_returns_timed_out_when_deactivation_cannot_run()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeactivationActor.Deactivations = 0;
        await using var provider = new ServiceCollection()
            .AddULinkGameServerActors()
            .BuildServiceProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var id = ActorId.From("deactivate-timeout/1");

        var blocking = runtime.TellAsync<DeactivationActor>(
            id,
            (actor, ct) => actor.BlockAsync(entered, release.Task, ct),
            cancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        var outcome = await runtime.StopAsync(id, TimeSpan.FromMilliseconds(20));

        release.SetResult();
        await blocking;

        Assert.Equal(ActorStopOutcome.TimedOut, outcome);
        Assert.Equal(0, DeactivationActor.Deactivations);
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

    private sealed class SlowActor : Actor
    {
        public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private sealed class BlockingActor : Actor
    {
        public int Count { get; set; }

        public async ValueTask BlockAsync(
            TaskCompletionSource entered,
            Task release,
            CancellationToken cancellationToken)
        {
            entered.SetResult();
            await release.WaitAsync(cancellationToken);
        }
    }

    private sealed class TimerActor : Actor
    {
        public int Ticks { get; set; }
    }

    private sealed class DeactivationActor : Actor
    {
        public static int Deactivations { get; set; }

        protected override ValueTask OnDeactivateAsync(CancellationToken cancellationToken)
        {
            Deactivations++;
            return ValueTask.CompletedTask;
        }

        public async ValueTask BlockAsync(
            TaskCompletionSource entered,
            Task release,
            CancellationToken cancellationToken)
        {
            entered.SetResult();
            await release.WaitAsync(cancellationToken);
        }
    }

    private static async Task<T> WaitForAsync<T>(
        Func<Task<T>> read,
        Func<T, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        while (true)
        {
            var value = await read();

            if (predicate(value))
            {
                return value;
            }

            await Task.Delay(10, linked.Token);
        }
    }
}
