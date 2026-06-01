namespace ULinkGame.Server.Actors;

public sealed record RemoteActorInvocationResult(
    RemoteActorStatus Status,
    ReadOnlyMemory<byte> Payload,
    string? Message = null)
{
    public ReadOnlyMemory<byte> Payload { get; init; } = Payload.ToArray();

    public static RemoteActorInvocationResult Accepted()
    {
        return new RemoteActorInvocationResult(RemoteActorStatus.Accepted, ReadOnlyMemory<byte>.Empty);
    }

    public static RemoteActorInvocationResult Replied(ReadOnlyMemory<byte> payload)
    {
        return new RemoteActorInvocationResult(RemoteActorStatus.Replied, payload);
    }

    public static RemoteActorInvocationResult Failed(RemoteActorStatus status, string message)
    {
        return new RemoteActorInvocationResult(status, ReadOnlyMemory<byte>.Empty, message);
    }
}
