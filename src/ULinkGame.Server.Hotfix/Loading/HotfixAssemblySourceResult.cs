namespace ULinkGame.Server.Hotfix.Loading;

public sealed record HotfixAssemblySourceResult(
    string SourceKind,
    string? Version,
    string AssemblyPath,
    string BaseDirectory);
