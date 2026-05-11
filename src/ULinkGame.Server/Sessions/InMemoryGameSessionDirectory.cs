namespace ULinkGame.Server.Sessions;

public sealed class InMemoryGameSessionDirectory : IGameSessionDirectory
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, OwnerSessionState> _owners = new(StringComparer.Ordinal);

    public ValueTask<GameSessionKey> StartNewSessionAsync(
        string ownerKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var generation = _owners.TryGetValue(ownerKey, out var existing)
                ? existing.Session.Generation + 1
                : 1;

            var session = new GameSessionKey(ownerKey, Guid.NewGuid().ToString("N"), generation);
            _owners[ownerKey] = new OwnerSessionState(session);
            return ValueTask.FromResult(session);
        }
    }

    public ValueTask<SessionResumeDecision> TryResumeAsync(
        GameSessionKey session,
        CancellationToken cancellationToken = default)
    {
        ValidateSession(session);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_owners.TryGetValue(session.OwnerKey, out var state) ||
                !state.Session.Equals(session))
            {
                return ValueTask.FromResult(SessionResumeDecision.StateLost("Session was not found or generation changed."));
            }

            state.LastSeenAt = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(SessionResumeDecision.Resumed(state.Session));
        }
    }

    public ValueTask BindEndpointAsync<TCallback>(
        SessionEndpointKey endpoint,
        string connectionId,
        TCallback callback,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        ValidateEndpoint(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var state = GetCurrentSessionState(endpoint.Session);
            state.Endpoints[endpoint.EndpointName] = new EndpointBinding(
                connectionId,
                callback,
                typeof(TCallback),
                DateTimeOffset.UtcNow);
            state.LastSeenAt = DateTimeOffset.UtcNow;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MarkEndpointDisconnectedAsync(
        SessionEndpointKey endpoint,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(endpoint);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_owners.TryGetValue(endpoint.Session.OwnerKey, out var state) ||
                !state.Session.Equals(endpoint.Session) ||
                !state.Endpoints.TryGetValue(endpoint.EndpointName, out var binding))
            {
                return ValueTask.CompletedTask;
            }

            if (connectionId is not null &&
                !string.Equals(binding.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return ValueTask.CompletedTask;
            }

            state.Endpoints[endpoint.EndpointName] = binding.Disconnect(DateTimeOffset.UtcNow);
            state.LastSeenAt = DateTimeOffset.UtcNow;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<TCallback?> GetCallbackAsync<TCallback>(
        SessionEndpointKey endpoint,
        CancellationToken cancellationToken = default)
        where TCallback : class
    {
        ValidateEndpoint(endpoint);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_owners.TryGetValue(endpoint.Session.OwnerKey, out var state) ||
                !state.Session.Equals(endpoint.Session) ||
                !state.Endpoints.TryGetValue(endpoint.EndpointName, out var binding) ||
                binding.DisconnectedAt is not null)
            {
                return ValueTask.FromResult<TCallback?>(null);
            }

            return ValueTask.FromResult(binding.Callback as TCallback);
        }
    }

    public ValueTask ExpireDisconnectedEndpointsAsync(
        DateTimeOffset disconnectedBefore,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var expiredOwners = new List<string>();
            foreach (var owner in _owners)
            {
                var endpoints = owner.Value.Endpoints
                    .Where(endpoint => endpoint.Value.DisconnectedAt < disconnectedBefore)
                    .Select(endpoint => endpoint.Key)
                    .ToArray();

                foreach (var endpointName in endpoints)
                {
                    owner.Value.Endpoints.Remove(endpointName);
                }

                if (owner.Value.Endpoints.Count == 0 &&
                    owner.Value.LastSeenAt < disconnectedBefore)
                {
                    expiredOwners.Add(owner.Key);
                }
            }

            foreach (var ownerKey in expiredOwners)
            {
                _owners.Remove(ownerKey);
            }
        }

        return ValueTask.CompletedTask;
    }

    private OwnerSessionState GetCurrentSessionState(GameSessionKey session)
    {
        if (!_owners.TryGetValue(session.OwnerKey, out var state) ||
            !state.Session.Equals(session))
        {
            throw new InvalidOperationException("Session was not found or generation changed.");
        }

        return state;
    }

    private static void ValidateEndpoint(SessionEndpointKey endpoint)
    {
        ValidateSession(endpoint.Session);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.EndpointName);
    }

    private static void ValidateSession(GameSessionKey session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session.OwnerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.SessionId);
        if (session.Generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(session), "Session generation must be positive.");
        }
    }

    private sealed class OwnerSessionState
    {
        public OwnerSessionState(GameSessionKey session)
        {
            Session = session;
            LastSeenAt = DateTimeOffset.UtcNow;
        }

        public GameSessionKey Session { get; }

        public DateTimeOffset LastSeenAt { get; set; }

        public Dictionary<string, EndpointBinding> Endpoints { get; } = new(StringComparer.Ordinal);
    }

    private sealed record EndpointBinding(
        string ConnectionId,
        object Callback,
        Type CallbackType,
        DateTimeOffset BoundAt,
        DateTimeOffset? DisconnectedAt = null)
    {
        public EndpointBinding Disconnect(DateTimeOffset disconnectedAt)
        {
            return this with { DisconnectedAt = disconnectedAt };
        }
    }
}

