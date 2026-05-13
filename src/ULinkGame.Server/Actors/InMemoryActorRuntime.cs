using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Actors;

public sealed class InMemoryActorRuntime : IActorRuntime, IAsyncDisposable
{
    private static readonly AsyncLocal<ActorCell?> CurrentCell = new();

    private readonly ConcurrentDictionary<ActorId, ActorCell> _actors = new();
    private readonly IServiceProvider _services;
    private readonly ActorRuntimeOptions _options;

    public InMemoryActorRuntime(IServiceProvider services, ActorRuntimeOptions options)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<TActor> GetOrCreateAsync<TActor>(
        ActorId id,
        CancellationToken cancellationToken = default)
        where TActor : class, IActor
    {
        var cell = GetOrCreateCell<TActor>(id);
        await cell.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return (TActor)cell.Actor;
    }

    public async ValueTask TellAsync<TActor>(
        ActorId id,
        Func<TActor, CancellationToken, ValueTask> message,
        CancellationToken cancellationToken = default)
        where TActor : class, IActor
    {
        ArgumentNullException.ThrowIfNull(message);

        var cell = GetOrCreateCell<TActor>(id);
        await cell.EnqueueAsync(
            static async (actor, state, ct) =>
            {
                var typedState = ((Func<TActor, CancellationToken, ValueTask>)state);
                await typedState((TActor)actor, ct).ConfigureAwait(false);
                return null;
            },
            message,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TResult> AskAsync<TActor, TResult>(
        ActorId id,
        Func<TActor, CancellationToken, ValueTask<TResult>> message,
        CancellationToken cancellationToken = default)
        where TActor : class, IActor
    {
        ArgumentNullException.ThrowIfNull(message);

        var cell = GetOrCreateCell<TActor>(id);
        var result = await cell.EnqueueAsync(
            static async (actor, state, ct) =>
            {
                var typedState = ((Func<TActor, CancellationToken, ValueTask<TResult>>)state);
                return await typedState((TActor)actor, ct).ConfigureAwait(false);
            },
            message,
            cancellationToken).ConfigureAwait(false);

        return result is TResult typedResult
            ? typedResult
            : throw new InvalidOperationException($"Actor call returned an invalid result for '{typeof(TResult).FullName}'.");
    }

    public IAsyncDisposable RegisterTimer<TActor>(
        ActorId id,
        TimeSpan dueTime,
        TimeSpan? period,
        Func<TActor, CancellationToken, ValueTask> callback)
        where TActor : class, IActor
    {
        ArgumentNullException.ThrowIfNull(callback);

        return new ActorTimer<TActor>(this, id, dueTime, period, callback);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cell in _actors.Values)
        {
            await cell.DisposeAsync().ConfigureAwait(false);
        }

        _actors.Clear();
    }

    private ActorCell GetOrCreateCell<TActor>(ActorId id)
        where TActor : class, IActor
    {
        var cell = _actors.GetOrAdd(id, static (actorId, state) =>
        {
            var runtime = state.Runtime;
            var actor = ActivatorUtilities.CreateInstance<TActor>(runtime._services);
            var capacity = Math.Max(1, runtime._options.MailboxCapacity);
            return new ActorCell(actorId, actor, typeof(TActor), runtime._services, runtime, capacity);
        }, new RuntimeState(this));

        if (!cell.ActorType.IsAssignableTo(typeof(TActor)) && !typeof(TActor).IsAssignableFrom(cell.ActorType))
        {
            throw new InvalidOperationException(
                $"Actor id '{id}' is already bound to '{cell.ActorType.FullName}', not '{typeof(TActor).FullName}'.");
        }

        return cell;
    }

    private readonly record struct RuntimeState(InMemoryActorRuntime Runtime);

    private sealed class ActorCell : IAsyncDisposable
    {
        private readonly Channel<ActorWorkItem> _mailbox;
        private readonly CancellationTokenSource _stopping = new();
        private readonly ActorId _id;
        private readonly IServiceProvider _services;
        private readonly IActorRuntime _runtime;
        private readonly object _startGate = new();
        private Task? _loop;
        private bool _started;

        public ActorCell(
            ActorId id,
            IActor actor,
            Type actorType,
            IServiceProvider services,
            IActorRuntime runtime,
            int mailboxCapacity)
        {
            _id = id;
            _services = services;
            _runtime = runtime;
            Actor = actor;
            ActorType = actorType;
            _mailbox = Channel.CreateBounded<ActorWorkItem>(new BoundedChannelOptions(mailboxCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public IActor Actor { get; }

        public Type ActorType { get; }

        public async ValueTask EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if (!_started)
            {
                lock (_startGate)
                {
                    if (!_started)
                    {
                        _started = true;
                        _loop = Task.Run(RunAsync);
                    }
                }
            }

            if (Actor is Actor actor)
            {
                await EnqueueAsync(
                    static async (target, state, ct) =>
                    {
                        var typedActor = (Actor)target;
                        var typedState = ((ActorCell Cell, IServiceProvider Services, IActorRuntime Runtime))state;
                        if (ReferenceEquals(typedActor.Context, ActorContext.Uninitialized))
                        {
                            await typedActor.ActivateAsync(
                                new ActorContext(typedState.Cell._id, typedState.Services, typedState.Runtime),
                                ct).ConfigureAwait(false);
                        }

                        return null;
                    },
                    (this, _services, _runtime),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask<object?> EnqueueAsync(
            Func<IActor, object, CancellationToken, ValueTask<object?>> callback,
            object state,
            CancellationToken cancellationToken)
        {
            if (ReferenceEquals(CurrentCell.Value, this))
            {
                return await callback(Actor, state, cancellationToken).ConfigureAwait(false);
            }

            await EnsureStartedCoreAsync(cancellationToken).ConfigureAwait(false);

            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new ActorWorkItem(callback, state, completion, cancellationToken);
            await _mailbox.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            _mailbox.Writer.TryComplete();

            if (_loop is not null)
            {
                try
                {
                    await _loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _stopping.Dispose();
        }

        private ValueTask EnsureStartedCoreAsync(CancellationToken cancellationToken)
        {
            if (!_started)
            {
                return EnsureStartedAsync(cancellationToken);
            }

            return ValueTask.CompletedTask;
        }

        private async Task RunAsync()
        {
            await foreach (var item in _mailbox.Reader.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                if (item.CancellationToken.IsCancellationRequested)
                {
                    item.Completion.TrySetCanceled(item.CancellationToken);
                    continue;
                }

                try
                {
                    CurrentCell.Value = this;
                    var result = await item.Callback(Actor, item.State, item.CancellationToken).ConfigureAwait(false);
                    item.Completion.TrySetResult(result);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == item.CancellationToken)
                {
                    item.Completion.TrySetCanceled(item.CancellationToken);
                }
                catch (Exception ex)
                {
                    item.Completion.TrySetException(ex);
                }
                finally
                {
                    CurrentCell.Value = null;
                }
            }
        }
    }

    private sealed record ActorWorkItem(
        Func<IActor, object, CancellationToken, ValueTask<object?>> Callback,
        object State,
        TaskCompletionSource<object?> Completion,
        CancellationToken CancellationToken);

    private sealed class ActorTimer<TActor> : IAsyncDisposable
        where TActor : class, IActor
    {
        private readonly InMemoryActorRuntime _runtime;
        private readonly ActorId _id;
        private readonly Func<TActor, CancellationToken, ValueTask> _callback;
        private readonly Timer _timer;
        private int _disposed;

        public ActorTimer(
            InMemoryActorRuntime runtime,
            ActorId id,
            TimeSpan dueTime,
            TimeSpan? period,
            Func<TActor, CancellationToken, ValueTask> callback)
        {
            _runtime = runtime;
            _id = id;
            _callback = callback;
            _timer = new Timer(Tick, null, dueTime, period ?? Timeout.InfiniteTimeSpan);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await _timer.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void Tick(object? state)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _ = _runtime.TellAsync<TActor>(_id, _callback, CancellationToken.None);
        }
    }
}
