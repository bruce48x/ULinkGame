using ULinkGame.Server.Configuration;

namespace ULinkGame.Server.Features;

public sealed class ULinkGameEndpointCatalog
{
    private readonly IReadOnlyList<ULinkGameEndpointOptions> _endpoints;

    public ULinkGameEndpointCatalog(IReadOnlyList<ULinkGameEndpointOptions> endpoints)
    {
        _endpoints = endpoints;
    }

    public ULinkGameEndpointOptions RequireTransport(string transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);

        var endpoint = _endpoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Transport, transport, StringComparison.OrdinalIgnoreCase));

        return endpoint ?? throw new InvalidOperationException(
            $"ULinkGame endpoint transport '{transport}' is required but was not configured.");
    }
}
