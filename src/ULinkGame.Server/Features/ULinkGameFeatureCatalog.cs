namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureCatalog
{
    public ULinkGameFeatureCatalog(IReadOnlyList<ULinkGameFeatureDefinition> activeDefinitions)
    {
        ActiveDefinitions = activeDefinitions;
        ActiveNames = activeDefinitions.Select(definition => definition.Name).ToArray();
    }

    public IReadOnlyList<ULinkGameFeatureDefinition> ActiveDefinitions { get; }

    public IReadOnlyList<string> ActiveNames { get; }
}
