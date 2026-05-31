namespace ULinkGame.Server.Guardrails;

public sealed class ULinkGameRuntimeValidator
{
    private readonly IReadOnlyList<IULinkGameValidationRule> _rules;

    public ULinkGameRuntimeValidator(IEnumerable<IULinkGameValidationRule> rules)
    {
        _rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    }

    public ULinkGameValidationResult Validate(ULinkGameResolvedRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var diagnostics = new List<ULinkGameDiagnostic>();
        foreach (var rule in _rules)
        {
            diagnostics.AddRange(rule.Validate(runtime));
        }

        return new ULinkGameValidationResult(diagnostics);
    }
}
