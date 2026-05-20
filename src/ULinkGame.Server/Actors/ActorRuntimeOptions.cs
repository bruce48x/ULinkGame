namespace ULinkGame.Server.Actors;

public sealed class ActorRuntimeOptions
{
    public int MailboxCapacity { get; set; } = 4096;

    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
