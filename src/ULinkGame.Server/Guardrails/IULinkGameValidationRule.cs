namespace ULinkGame.Server.Guardrails;

public interface IULinkGameValidationRule
{
    IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime);
}
