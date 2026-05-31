namespace ULinkGame.Server.Guardrails.Rules;

public sealed class ClusterServiceGraphRule : IULinkGameValidationRule
{
    public IEnumerable<ULinkGameDiagnostic> Validate(ULinkGameResolvedRuntime runtime)
    {
        var duplicated = runtime.Cluster.Services
            .GroupBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            yield return new ULinkGameDiagnostic(
                "ULINK041",
                ULinkGameDiagnosticSeverity.Error,
                $"Cluster service name '{duplicated.Key}' is duplicated.",
                "Use unique service names in the resolved cluster service list.");
        }
    }
}
