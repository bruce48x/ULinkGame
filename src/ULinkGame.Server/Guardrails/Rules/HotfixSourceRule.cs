namespace ULinkGame.Server.Guardrails.Rules;

public sealed class HotfixSourceRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        if (!File.Exists(runtime.Hotfix.AssemblyPath.Value))
        {
            yield return new ULinkGameDiagnostic(
                "ULINK071",
                ULinkGameDiagnosticSeverity.Error,
                "Hotfix assembly was not found.",
                "dotnet build Server/Hotfix/Server.Hotfix.csproj");
        }
    }
}
