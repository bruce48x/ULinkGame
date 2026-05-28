using Microsoft.CodeAnalysis;
using Xunit;

namespace ULinkGame.Server.Hotfix.Generators.Tests;

public sealed class HotfixGeneratorTests
{
    [Fact]
    public void Generator_emits_accessor_for_private_field()
    {
        var source = """
            using ULinkGame.Server.Hotfix.Abstractions;

            namespace Demo;

            [HotfixState]
            public partial class PlayerState
            {
                private int exp;
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("partial class PlayerState", result.GeneratedSource);
        Assert.Contains("public int __hotfix_exp()", result.GeneratedSource);
        Assert.Contains("return exp;", result.GeneratedSource);
    }

    [Fact]
    public void Generator_emits_accessor_for_underscore_private_field()
    {
        var source = """
            using ULinkGame.Server.Hotfix.Abstractions;

            namespace Demo;

            [HotfixState]
            public partial class PlayerState
            {
                private int _exp;
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("public int __hotfix_exp()", result.GeneratedSource);
        Assert.Contains("return _exp;", result.GeneratedSource);
    }

    [Fact]
    public void Generator_reports_diagnostic_for_non_partial_state()
    {
        var source = """
            using ULinkGame.Server.Hotfix.Abstractions;

            namespace Demo;

            [HotfixState]
            public class PlayerState
            {
                private int exp;
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var diagnostic = Assert.Single(result.ErrorDiagnostics, static diagnostic => diagnostic.Id == "ULGHOTFIX001");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Generated_accessor_output_compiles()
    {
        var source = """
            using ULinkGame.Server.Hotfix.Abstractions;

            namespace Demo;

            [HotfixState]
            public partial class PlayerState
            {
                private int exp;
            }

            public static class Reader
            {
                public static int Read(PlayerState state)
                {
                    return state.__hotfix_exp();
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
    }
}
