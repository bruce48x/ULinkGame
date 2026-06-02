namespace ULinkGame.Server.Actors;

public sealed class ActorDirectoryUnavailableException : Exception
{
    public ActorDirectoryUnavailableException(string message)
        : base(message)
    {
    }

    public ActorDirectoryUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
