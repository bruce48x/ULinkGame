using Agar.Godot.Sample.State.Contracts.Matchmaking;
using Agar.Godot.Sample.State.Contracts.Rooms;
using Agar.Godot.Sample.State.Contracts.Sessions;
using Agar.Godot.Sample.State.Contracts.Users;
using Agar.Godot.Sample.State.Matchmaking;
using Agar.Godot.Sample.State.Rooms;
using Agar.Godot.Sample.State.Sessions;
using Agar.Godot.Sample.State.Users;
using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Actors;

namespace Agar.Godot.Sample.State;

public interface IUserStateStore
{
    Task<UserLoginResult> LoginAsync(string userId, string password, bool reconnect);
    Task<UserProfileSnapshot> GetProfileAsync(string userId);
    Task SetOnlineAsync(string userId, bool isOnline);
    Task SetScoreAsync(string userId, int score);
    Task AddScoreAsync(string userId, int delta);
    Task AddWinAsync(string userId);
}

public interface IPlayerSessionStateStore
{
    Task<PlayerSessionSnapshot> AttachAsync(PlayerSessionAttachRequest request);
    Task<PlayerSessionSnapshot> ReconnectAsync(PlayerSessionReconnectRequest request);
    Task<PlayerSessionSnapshot> MarkQueuedAsync(PlayerSessionQueueRequest request);
    Task<PlayerSessionSnapshot> ClearQueueAsync(PlayerSessionQueueClearRequest request);
    Task<PlayerSessionSnapshot> AssignRoomAsync(PlayerRoomAssignment request);
    Task<PlayerSessionSnapshot> ClearRoomAsync(PlayerRoomClearRequest request);
    Task<PlayerSessionSnapshot> MarkDisconnectedAsync(PlayerSessionDisconnectRequest request);
    Task<PlayerSessionSnapshot> HeartbeatAsync(PlayerSessionHeartbeatRequest request);
    Task<PlayerSessionSnapshot> GetSnapshotAsync(string userId);
}

public interface IMatchmakingStateStore
{
    Task<MatchmakingEnqueueResult> EnqueueAsync(MatchmakingEnqueueRequest request);
    Task<MatchmakingCancelResult> CancelAsync(MatchmakingCancelRequest request);
    Task TickAsync(MatchmakingTickRequest request);
    Task<MatchmakingStatusSnapshot> GetStatusAsync();
}

public interface IRoomStateStore
{
    Task<RoomSettlementResult> CreateAsync(RoomCreateRequest request);
    Task<RoomSettlementResult> JoinAsync(PlayerRoomAssignment request);
    Task<RoomSettlementResult> LeaveAsync(RoomPlayerLeaveRequest request);
    Task<RoomSettlementResult> SetReadyAsync(RoomPlayerReadyRequest request);
    Task<RoomSettlementResult> StartAsync(RoomStartRequest request);
    Task<RoomSettlementResult> CompleteAsync(RoomMatchCompletion request);
    Task<RoomSnapshot> GetSnapshotAsync(string roomId);
}

public static class GodotSampleStateServiceCollectionExtensions
{
    public static IServiceCollection AddAgarGodotSampleState(this IServiceCollection services)
    {
        services.AddULinkGameServerActors();
        services.AddSingleton<IUserStateStore, ActorUserStateStore>();
        services.AddSingleton<IPlayerSessionStateStore, ActorPlayerSessionStateStore>();
        services.AddSingleton<IMatchmakingStateStore, ActorMatchmakingStateStore>();
        services.AddSingleton<IRoomStateStore, ActorRoomStateStore>();
        return services;
    }
}

internal sealed class ActorUserStateStore(IActorRuntime runtime) : IUserStateStore
{
    public Task<UserLoginResult> LoginAsync(string userId, string password, bool reconnect)
    {
        return runtime.AskAsync<UserActor, UserLoginResult>(
            UserId(userId),
            (actor, _) => new ValueTask<UserLoginResult>(actor.LoginAsync(password, reconnect))).AsTask();
    }

    public Task<UserProfileSnapshot> GetProfileAsync(string userId)
    {
        return runtime.AskAsync<UserActor, UserProfileSnapshot>(
            UserId(userId),
            static (actor, _) => new ValueTask<UserProfileSnapshot>(actor.GetProfileAsync())).AsTask();
    }

    public Task SetOnlineAsync(string userId, bool isOnline)
    {
        return runtime.TellAsync<UserActor>(
            UserId(userId),
            (actor, _) => new ValueTask(actor.SetOnlineAsync(isOnline))).AsTask();
    }

    public Task SetScoreAsync(string userId, int score)
    {
        return runtime.TellAsync<UserActor>(
            UserId(userId),
            (actor, _) => new ValueTask(actor.SetScoreAsync(score))).AsTask();
    }

    public Task AddScoreAsync(string userId, int delta)
    {
        return runtime.TellAsync<UserActor>(
            UserId(userId),
            (actor, _) => new ValueTask(actor.AddScoreAsync(delta))).AsTask();
    }

    public Task AddWinAsync(string userId)
    {
        return runtime.TellAsync<UserActor>(
            UserId(userId),
            static (actor, _) => new ValueTask(actor.AddWinAsync())).AsTask();
    }

    private static ActorId UserId(string userId) => ActorId.From(userId);
}

internal sealed class ActorPlayerSessionStateStore(IActorRuntime runtime) : IPlayerSessionStateStore
{
    public Task<PlayerSessionSnapshot> AttachAsync(PlayerSessionAttachRequest request) => Ask(request.UserId, actor => actor.AttachAsync(request));
    public Task<PlayerSessionSnapshot> ReconnectAsync(PlayerSessionReconnectRequest request) => Ask(request.UserId, actor => actor.ReconnectAsync(request));
    public Task<PlayerSessionSnapshot> MarkQueuedAsync(PlayerSessionQueueRequest request) => Ask(request.UserId, actor => actor.MarkQueuedAsync(request));
    public Task<PlayerSessionSnapshot> ClearQueueAsync(PlayerSessionQueueClearRequest request) => Ask(request.UserId, actor => actor.ClearQueueAsync(request));
    public Task<PlayerSessionSnapshot> AssignRoomAsync(PlayerRoomAssignment request) => Ask(request.UserId, actor => actor.AssignRoomAsync(request));
    public Task<PlayerSessionSnapshot> ClearRoomAsync(PlayerRoomClearRequest request) => Ask(request.UserId, actor => actor.ClearRoomAsync(request));
    public Task<PlayerSessionSnapshot> MarkDisconnectedAsync(PlayerSessionDisconnectRequest request) => Ask(request.UserId, actor => actor.MarkDisconnectedAsync(request));
    public Task<PlayerSessionSnapshot> HeartbeatAsync(PlayerSessionHeartbeatRequest request) => Ask(request.UserId, actor => actor.HeartbeatAsync(request));
    public Task<PlayerSessionSnapshot> GetSnapshotAsync(string userId) => Ask(userId, static actor => actor.GetSnapshotAsync());

    private static ActorId SessionId(string userId) => ActorId.From($"session:{userId}");

    private Task<PlayerSessionSnapshot> Ask(string userId, Func<PlayerSessionActor, Task<PlayerSessionSnapshot>> call)
    {
        return runtime.AskAsync<PlayerSessionActor, PlayerSessionSnapshot>(
            SessionId(userId),
            (actor, _) => new ValueTask<PlayerSessionSnapshot>(call(actor))).AsTask();
    }
}

internal sealed class ActorMatchmakingStateStore(IActorRuntime runtime) : IMatchmakingStateStore
{
    private static readonly ActorId DefaultQueueId = ActorId.From("default");

    public Task<MatchmakingEnqueueResult> EnqueueAsync(MatchmakingEnqueueRequest request)
    {
        return runtime.AskAsync<MatchmakingActor, MatchmakingEnqueueResult>(
            DefaultQueueId,
            (actor, _) => new ValueTask<MatchmakingEnqueueResult>(actor.EnqueueAsync(request))).AsTask();
    }

    public Task<MatchmakingCancelResult> CancelAsync(MatchmakingCancelRequest request)
    {
        return runtime.AskAsync<MatchmakingActor, MatchmakingCancelResult>(
            DefaultQueueId,
            (actor, _) => new ValueTask<MatchmakingCancelResult>(actor.CancelAsync(request))).AsTask();
    }

    public Task TickAsync(MatchmakingTickRequest request)
    {
        return runtime.TellAsync<MatchmakingActor>(
            DefaultQueueId,
            (actor, _) => new ValueTask(actor.TickAsync(request))).AsTask();
    }

    public Task<MatchmakingStatusSnapshot> GetStatusAsync()
    {
        return runtime.AskAsync<MatchmakingActor, MatchmakingStatusSnapshot>(
            DefaultQueueId,
            static (actor, _) => new ValueTask<MatchmakingStatusSnapshot>(actor.GetStatusAsync())).AsTask();
    }
}

internal sealed class ActorRoomStateStore(IActorRuntime runtime) : IRoomStateStore
{
    public Task<RoomSettlementResult> CreateAsync(RoomCreateRequest request) => Ask(request.RoomId, actor => actor.CreateAsync(request));
    public Task<RoomSettlementResult> JoinAsync(PlayerRoomAssignment request) => Ask(request.RoomId, actor => actor.JoinAsync(request));
    public Task<RoomSettlementResult> LeaveAsync(RoomPlayerLeaveRequest request) => Ask(request.RoomId, actor => actor.LeaveAsync(request));
    public Task<RoomSettlementResult> SetReadyAsync(RoomPlayerReadyRequest request) => Ask(request.RoomId, actor => actor.SetReadyAsync(request));
    public Task<RoomSettlementResult> StartAsync(RoomStartRequest request) => Ask(request.RoomId, actor => actor.StartAsync(request));
    public Task<RoomSettlementResult> CompleteAsync(RoomMatchCompletion request) => Ask(request.RoomId, actor => actor.CompleteAsync(request));

    public Task<RoomSnapshot> GetSnapshotAsync(string roomId)
    {
        return runtime.AskAsync<RoomActor, RoomSnapshot>(
            ActorId.From(roomId),
            static (actor, _) => new ValueTask<RoomSnapshot>(actor.GetSnapshotAsync())).AsTask();
    }

    private Task<RoomSettlementResult> Ask(string roomId, Func<RoomActor, Task<RoomSettlementResult>> call)
    {
        return runtime.AskAsync<RoomActor, RoomSettlementResult>(
            ActorId.From(roomId),
            (actor, _) => new ValueTask<RoomSettlementResult>(call(actor))).AsTask();
    }
}
