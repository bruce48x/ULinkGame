namespace ULinkGame.Server.Guardrails.Rules;

public sealed class EndpointRule : IULinkGameValidationRule
{
    private static readonly HashSet<string> KnownTransports = new(StringComparer.OrdinalIgnoreCase)
    {
        "kcp",
        "tcp",
        "websocket"
    };

    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        var transport = runtime.Endpoint.Transport.Value;
        if (!KnownTransports.Contains(transport))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK020",
                ULinkGameDiagnosticSeverity.Error,
                $"Endpoint transport '{transport}' is unknown.",
                "Use kcp, tcp, or websocket.");
            yield break;
        }

        if (string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(runtime.Endpoint.Path.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK023",
                ULinkGameDiagnosticSeverity.Error,
                "WebSocket endpoint path is required.",
                "Set ULinkGame:Endpoint:Path to /ws or another explicit WebSocket path.");
        }
    }
}
