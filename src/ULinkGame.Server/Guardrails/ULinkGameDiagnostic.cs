namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameDiagnostic(
    string Code,
    ULinkGameDiagnosticSeverity Severity,
    string Message,
    string? Repair = null);
