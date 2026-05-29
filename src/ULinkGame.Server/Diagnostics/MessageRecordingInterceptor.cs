using System.Collections.Concurrent;
using ULinkGame.Server.Actors;

namespace ULinkGame.Server.Diagnostics;

public sealed class MessageRecordingInterceptor : global::ULinkActor.IActorMessageInterceptor
{
    private readonly IMessageLogStore _store;
    private readonly ConcurrentDictionary<global::ULinkActor.ActorId, ActorId> _idMap;

    public MessageRecordingInterceptor(
        IMessageLogStore store,
        ConcurrentDictionary<global::ULinkActor.ActorId, ActorId> idMap)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
    }

    public ValueTask OnBeforeMessage(
        global::ULinkActor.ActorId actorId,
        object message,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask OnAfterMessage(
        global::ULinkActor.ActorId actorId,
        object message,
        Exception? error,
        CancellationToken cancellationToken)
    {
        var gameId = _idMap.TryGetValue(actorId, out var mapped)
            ? mapped
            : ActorId.From(actorId.Value.ToString());

        var entry = new MessageLogEntry(
            DateTimeOffset.UtcNow,
            message,
            error?.GetType().FullName);

        await _store.RecordAsync(gameId, entry, cancellationToken).ConfigureAwait(false);
    }
}
