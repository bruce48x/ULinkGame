using ULinkGame.Server.Configuration;

namespace Gateway.Hosting;

internal sealed class GatewayRpcServerOptions
{
    public string Transport { get; init; } = "websocket";
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = "";

    public static GatewayRpcServerOptions FromEndpoint(ULinkGameEndpointOptions endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return new GatewayRpcServerOptions
        {
            Transport = NormalizeTransport(endpoint.Transport),
            Host = string.IsNullOrWhiteSpace(endpoint.AdvertisedHost)
                ? (string.IsNullOrWhiteSpace(endpoint.Host) ? "127.0.0.1" : endpoint.Host)
                : endpoint.AdvertisedHost,
            Port = endpoint.Port,
            Path = endpoint.Path
        };
    }

    private static string NormalizeTransport(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? ""
            : rawValue.Trim().ToLowerInvariant();
    }
}
