using ULinkGame.Server.Guardrails;
using Xunit;

namespace ULinkGame.Server.Tests.Guardrails;

public sealed class ULinkGameRuntimeValidatorTests
{
    [Fact]
    public void ValidationResult_Succeeds_WhenNoErrorDiagnosticsExist()
    {
        var result = new ULinkGameValidationResult(
            [
                new ULinkGameDiagnostic("ULINK000", ULinkGameDiagnosticSeverity.Info, "ok"),
                new ULinkGameDiagnostic("ULINK050", ULinkGameDiagnosticSeverity.Warning, "local default")
            ]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidationResult_Fails_WhenAnyErrorDiagnosticExists()
    {
        var result = new ULinkGameValidationResult(
            [
                new ULinkGameDiagnostic("ULINK001", ULinkGameDiagnosticSeverity.Error, "Node id is required.")
            ]);

        Assert.False(result.Succeeded);
    }
}
