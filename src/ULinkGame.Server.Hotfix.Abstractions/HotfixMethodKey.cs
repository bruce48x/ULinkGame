namespace ULinkGame.Server.Hotfix.Abstractions;

public sealed record HotfixMethodKey(
    string StateTypeName,
    string MethodName,
    string ReturnTypeName,
    IReadOnlyList<string> ParameterTypeNames)
{
    public override string ToString()
    {
        return $"{StateTypeName}.{MethodName}({string.Join(", ", ParameterTypeNames)}) -> {ReturnTypeName}";
    }
}
