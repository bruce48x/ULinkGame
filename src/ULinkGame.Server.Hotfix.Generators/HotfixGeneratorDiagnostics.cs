using Microsoft.CodeAnalysis;

namespace ULinkGame.Server.Hotfix.Generators
{
    internal static class HotfixGeneratorDiagnostics
    {
        public static readonly DiagnosticDescriptor StateMustBePartial = new DiagnosticDescriptor(
            "ULGHOTFIX001",
            "Hotfix state must be partial",
            "Hotfix state type '{0}' must be partial so friend accessors can be generated",
            "ULinkGame.Hotfix",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
