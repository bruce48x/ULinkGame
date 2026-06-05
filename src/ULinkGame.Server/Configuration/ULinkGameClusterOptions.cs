namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameClusterOptions
{
    public string Endpoint { get; init; } = "";
    public IReadOnlyList<string> Seeds { get; init; } = [];
}
