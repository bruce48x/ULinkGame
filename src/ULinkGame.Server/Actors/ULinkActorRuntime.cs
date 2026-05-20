using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Actors;

public sealed class ULinkActorRuntime : IActorRuntime, IDisposable, IAsyncDisposable
{
    private static readonly AsyncLocal<ActorCell?> CurrentCell = new();

    private readonly ConcurrentDictionary<ActorId, ActorCell> _actors = new();
    private readonly IServiceProvider _services;
    private readonly ActorRuntimeOptions _options;
    private readonly global::ULinkActor.ActorSystem _actorSystem;

    public ULinkActorRuntime(IServiceProvider services, ActorRuntimeOptions options)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _actorSystem = new global::ULinkActor.ActorSystem(new global::ULinkActor.ActorSystemOptions
        {
            MailboxCapacity = Math.Max(1, options.MailboxCapacity)
        });
    }

    public async ValueTask<TActor> GetOrCreateAsync<TActor>(
        ActorId id,
        CancellationToken cancellationToken = default)
        where TActor : class, IActor
    {
        var cell = GetOrCreateCell<TActor>(id);
        await cell.EnsureActivatedAsync(cancellationToken).ConfigureAwait(false);
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
        await cell.InvokeAsync(
            static async (actor, state, ct) =>
            {
                var callback = (Func<TActor, CancellationToken, ValueTask>)state;
                await callback((TActor)actor, ct).ConfigureAwait(false);
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
        var result = await cell.InvokeAsync(
            static async (actor, state, ct) =>
            {
                var callback = (Func<TActor, CancellationToken, ValueTask<TResult>>)state;
                return await callback((TActor)actor, ct).ConfigureAwait(false);
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
        _actors.Clear();
        await _actorSystem.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _actors.Clear();
        _actorSystem.Dispose();
    }

    private ActorCell GetOrCreateCell<TActor>(ActorId id)
        where TActor : class, IActor
    {
        var cell = _actors.GetOrAdd(id, static (actorId, state) =>
        {
            var runtime = state.Runtime;
            var actor = ActivatorUtilities.CreateInstance<TActor>(runtime._services);
            var cell = new ActorCell(actorId, actor, typeof(TActor), runtime._services, runtime, runtime._options);
            var actorRef = runtime._actorSystem.Spawn(
                actorId.Value,
                new ActorAdapter(cell),
                new global::ULinkActor.ActorSpawnOptions
                {
                    MailboxCapacity = Math.Max(1, runtime._options.MailboxCapacity)
                });
            cell.Bind(actorRef);
            return cell;
        }, new RuntimeState(this));

        if (!cell.ActorType.IsAssignableTo(typeof(TActor)) && !typeof(TActor).IsAssignableFrom(cell.ActorType))
        {
            throw new InvalidOperationException(
                $"Actor id '{id}' is already bound to '{cell.ActorType.FullName}', not '{typeof(TActor).FullName}'.");
        }

        return cell;
    }

    private readonly record struct RuntimeState(ULinkActorRuntime Runtime);

    private sealed class ActorCell
    {
        private readonly ActorId _id;
        private readonly IServiceProvider _services;
        private readonly IActorRuntime _runtime;
        private readonly ActorRuntimeOptions _runtimeOptions;
        private global::ULinkActor.ActorRef<ActorRuntimeEnvelope>? _actorRef;
        private bool _activated;

        public ActorCell(
            ActorId id,
            IActor actor,
            Type actorType,
            IServiceProvider services,
            IActorRuntime runtime,
            ActorRuntimeOptions runtimeOptions)
        {
            _id = id;
            Actor = actor;
            ActorType = actorType;
            _services = services;
            _runtime = runtime;
            _runtimeOptions = runtimeOptions;
        }

        public IActor Actor { get; }

        public Type ActorType { get; }

        public void Bind(global::ULinkActor.ActorRef<ActorRuntimeEnvelope> actorRef)
        {
            _actorRef = actorRef;
        }

        public async ValueTask EnsureActivatedAsync(CancellationToken cancellationToken)
        {
            if (_activated)
            {
                return;
            }

            await InvokeAsync(
                static async (actor, state, ct) =>
                {
                    var cell = (ActorCell)state;
                    await cell.ActivateCoreAsync(actor, ct).ConfigureAwait(false);
                    return null;
                },
                this,
                cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<object?> InvokeAsync(
            Func<IActor, object, CancellationToken, ValueTask<object?>> callback,
            object state,
            CancellationToken cancellationToken)
        {
            if (ReferenceEquals(CurrentCell.Value, this))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await callback(Actor, state, cancellationToken).ConfigureAwait(false);
            }

            var actorRef = _actorRef ?? throw new InvalidOperationException($"Actor '{_id}' is not bound.");
            var envelope = new ActorRuntimeEnvelope(callback, state, cancellationToken);
            return await actorRef.Call<object?>(envelope, _runtimeOptions.CallTimeout, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<object?> DispatchAsync(ActorRuntimeEnvelope envelope)
        {
            if (envelope.CancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(envelope.CancellationToken);
            }

            try
            {
                CurrentCell.Value = this;
                await ActivateCoreAsync(Actor, envelope.CancellationToken).ConfigureAwait(false);
                return await envelope.Callback(Actor, envelope.State, envelope.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CurrentCell.Value = null;
            }
        }

        private async ValueTask ActivateCoreAsync(IActor actor, CancellationToken cancellationToken)
        {
            if (_activated)
            {
                return;
            }

            if (actor is Actor typedActor)
            {
                await typedActor.ActivateAsync(
                    new ActorContext(_id, _services, _runtime),
                    cancellationToken).ConfigureAwait(false);
            }

            _activated = true;
        }
    }

    private sealed record ActorRuntimeEnvelope(
        Func<IActor, object, CancellationToken, ValueTask<object?>> Callback,
        object State,
        CancellationToken CancellationToken);

    private sealed class ActorAdapter : global::ULinkActor.IActor<ActorRuntimeEnvelope>
    {
        private readonly ActorCell _cell;

        public ActorAdapter(ActorCell cell)
        {
            _cell = cell;
        }

        public async ValueTask OnMessage(
            global::ULinkActor.ActorContext<ActorRuntimeEnvelope> ctx,
            ActorRuntimeEnvelope message)
        {
            var result = await _cell.DispatchAsync(message).ConfigureAwait(false);
            ctx.Respond(result);
        }
    }

    private sealed class ActorTimer<TActor> : IAsyncDisposable
        where TActor : class, IActor
    {
        private readonly ULinkActorRuntime _runtime;
        private readonly ActorId _id;
        private readonly Func<TActor, CancellationToken, ValueTask> _callback;
        private readonly Timer _timer;
        private int _disposed;

        public ActorTimer(
            ULinkActorRuntime runtime,
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
