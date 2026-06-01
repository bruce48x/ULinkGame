namespace ULinkGame.Server.Actors;

public sealed class RemoteActorOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public string ClusterName { get; set; } = "local";

    public string EndpointName { get; set; } = "cluster";
}
