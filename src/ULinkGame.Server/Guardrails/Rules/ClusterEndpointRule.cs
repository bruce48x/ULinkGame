namespace ULinkGame.Server.Guardrails.Rules;

public sealed class ClusterEndpointRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (runtime.ClusterEndpoint is null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(runtime.ClusterEndpoint.Endpoint.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK040",
                ULinkGameDiagnosticSeverity.Error,
                "ULinkGame:Cluster:Endpoint is required when Cluster is configured.",
                "Set ULinkGame:Cluster:Endpoint to a URI such as tcp://127.0.0.1:21001.");
            yield break;
        }

        if (!Uri.TryCreate(runtime.ClusterEndpoint.Endpoint.Value, UriKind.Absolute, out var uri)
            || !IsSupportedTcpUri(uri))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK043",
                ULinkGameDiagnosticSeverity.Error,
                "ULinkGame:Cluster:Endpoint must be a tcp URI with host and explicit port.",
                "Use a value such as tcp://127.0.0.1:21001.");
            yield break;
        }

        foreach (var endpoint in runtime.Endpoints)
        {
            if (endpoint.Port.Value == uri.Port)
            {
                yield return new ULinkGameDiagnostic(
                    "ULINK042",
                    ULinkGameDiagnosticSeverity.Error,
                    $"Cluster endpoint port {uri.Port} conflicts with a business endpoint.",
                    "Use a different port for ULinkGame:Cluster:Endpoint.");
            }
        }
    }

    private static bool IsSupportedTcpUri(Uri uri)
    {
        return string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(uri.Host)
            && !uri.IsDefaultPort
            && uri.Port is >= 1 and <= 65535;
    }
}
