namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameEndpointOptions
{
    public string Transport { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Path { get; init; } = "";
    public string AdvertisedHost { get; init; } = "";

    public string ToAdvertisedEndpoint()
    {
        var scheme = Transport switch
        {
            "websocket" => "ws",
            "tcp" => "tcp",
            _ => "kcp"
        };
        var host = string.IsNullOrWhiteSpace(AdvertisedHost) ? Host : AdvertisedHost;

        return string.IsNullOrWhiteSpace(Path)
            ? $"{scheme}://{host}:{Port}"
            : $"{scheme}://{host}:{Port}{Path}";
    }
}
