namespace ULinkGame.Server.Actors;

public sealed class RemoteActorOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
