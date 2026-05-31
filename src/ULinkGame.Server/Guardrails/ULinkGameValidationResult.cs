namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameValidationResult(
    IReadOnlyList<ULinkGameDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.All(static diagnostic =>
        diagnostic.Severity != ULinkGameDiagnosticSeverity.Error);

    public static ULinkGameValidationResult Success { get; } = new([]);
}
