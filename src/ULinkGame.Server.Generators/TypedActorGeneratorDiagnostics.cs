using Microsoft.CodeAnalysis;

namespace ULinkGame.Server.Generators
{
    internal static class TypedActorGeneratorDiagnostics
    {
        public static readonly DiagnosticDescriptor UnsupportedMethodSignature = new DiagnosticDescriptor(
            "ULINKACTOR001",
            "Typed actor method signature is not supported",
            "Typed actor method '{0}' has an unsupported signature",
            "ULinkGame.Actor",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
