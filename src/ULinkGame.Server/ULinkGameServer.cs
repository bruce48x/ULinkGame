using ULinkGame.Abstractions;
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;

namespace ULinkGame.Server;

public sealed class ULinkGameServer : IULinkGameServer
{
    private readonly IGameSessionDirectory _sessions;
    private readonly IGameSessionResumeService _resume;
    private readonly IReliablePushOutbox _reliablePush;
    private readonly IReliablePushAckService _reliablePushAcks;

    public ULinkGameServer(
        IGameSessionDirectory sessions,
        IGameSessionResumeService resume,
        IReliablePushOutbox reliablePush,
        IReliablePushAckService reliablePushAcks)
    {
        _sessions = sessions;
        _resume = resume;
        _reliablePush = reliablePush;
        _reliablePushAcks = reliablePushAcks;
    }

    public ValueTask<GameSessionKey> StartSessionAsync(
        string ownerKey,
        CancellationToken cancellationToken = default)
    {
        return _sessions.StartNewSessionAsync(ownerKey, cancellationToken);
    }

    public async ValueTask<GameSessionKey> StartSessionAsync<TCallback>(
        string ownerKey,
        string endpointName,
        string connectionId,
        TCallback callback,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        var session = await StartSessionAsync(ownerKey, cancellationToken).ConfigureAwait(false);
        await BindEndpointAsync(session, endpointName, connectionId, callback, cancellationToken)
            .ConfigureAwait(false);
        return session;
    }

    public async ValueTask<SessionResumeDecision> ResumeSessionAsync<TCallback>(
        GameSessionResumeRequest request,
        string endpointName,
        string connectionId,
        TCallback callback,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        var decision = await _resume.TryResumeAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision.Session is { } session &&
            decision.Status is SessionResumeStatus.Resumed or SessionResumeStatus.StateRefreshRequired)
        {
            await BindEndpointAsync(session, endpointName, connectionId, callback, cancellationToken)
                .ConfigureAwait(false);
        }

        return decision;
    }

    public ValueTask BindEndpointAsync<TCallback>(
        GameSessionKey session,
        string endpointName,
        string connectionId,
        TCallback callback,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        return _sessions.BindEndpointAsync(
            new SessionEndpointKey(session, endpointName),
            connectionId,
            callback,
            cancellationToken);
    }

    public ValueTask MarkEndpointDisconnectedAsync(
        GameSessionKey session,
        string endpointName,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        return _sessions.MarkEndpointDisconnectedAsync(
            new SessionEndpointKey(session, endpointName),
            connectionId,
            cancellationToken);
    }

    public ValueTask<TCallback?> GetCallbackAsync<TCallback>(
        GameSessionKey session,
        string endpointName,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        return _sessions.GetCallbackAsync<TCallback>(
            new SessionEndpointKey(session, endpointName),
            cancellationToken);
    }

    public ValueTask<long> PublishReliablePushAsync(
        GameSessionKey session,
        string kind,
        object payload,
        Func<ReliablePushRecord, ValueTask> deliver,
        CancellationToken cancellationToken = default)
    {
        return _reliablePush.PublishAsync(session, kind, payload, deliver, cancellationToken);
    }

    public ValueTask ReplayReliablePushAsync(
        GameSessionKey session,
        Func<ReliablePushRecord, ValueTask> deliver,
        CancellationToken cancellationToken = default)
    {
        return _reliablePush.ReplayPendingAsync(session, deliver, cancellationToken);
    }

    public ValueTask<ReliablePushAckOutcome> AckReliablePushAsync(
        GameSessionKey currentSession,
        GameSessionKey acknowledgedSession,
        long sequence,
        CancellationToken cancellationToken = default)
    {
        return _reliablePushAcks.AckAsync(
            currentSession,
            acknowledgedSession,
            sequence,
            cancellationToken);
    }
}
