namespace ULinkGame.Server.Guardrails.Rules;

public sealed class NodeIdentityRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime.NodeId.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK001",
                ULinkGameDiagnosticSeverity.Error,
                "Node id is required.",
                "Set ULinkGame:Node:Id to a stable node id.");
        }
    }
}
