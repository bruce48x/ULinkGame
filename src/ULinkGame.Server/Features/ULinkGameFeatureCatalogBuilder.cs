using ULinkGame.Server.Configuration;

namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureCatalogBuilder
{
    private readonly List<ULinkGameFeatureDefinition> _definitions = [];
    private readonly Dictionary<string, ULinkGameFeatureDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public ULinkGameFeatureDefinition Feature<TFeature>(string name)
        where TFeature : ULinkGameFeature
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_byName.ContainsKey(name))
        {
            throw new InvalidOperationException($"ULinkGame feature '{name}' is already registered.");
        }

        var definition = new ULinkGameFeatureDefinition(name, typeof(TFeature));
        _definitions.Add(definition);
        _byName.Add(name, definition);
        return definition;
    }

    internal ULinkGameFeatureCatalog Build(ULinkGameRuntimeOptions options)
    {
        var active = ResolveActiveDefinitions(options.Feature);
        ValidateRequiredFeatures(active);
        return new ULinkGameFeatureCatalog(SortAfterDependencies(active));
    }

    private IReadOnlyList<ULinkGameFeatureDefinition> ResolveActiveDefinitions(IReadOnlyList<string>? configuredFeatures)
    {
        if (configuredFeatures is null)
        {
            return _definitions.ToArray();
        }

        var active = new List<ULinkGameFeatureDefinition>();
        foreach (var featureName in configuredFeatures)
        {
            if (!_byName.TryGetValue(featureName, out var definition))
            {
                var available = string.Join(", ", _definitions.Select(candidate => candidate.Name));
                throw new InvalidOperationException(
                    $"ULinkGame feature '{featureName}' was configured but is not registered. Available features: {available}.");
            }

            active.Add(definition);
        }

        return active;
    }

    private static void ValidateRequiredFeatures(IReadOnlyList<ULinkGameFeatureDefinition> active)
    {
        var activeNames = active
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in active)
        {
            foreach (var requiredFeature in definition.RequiredFeatures)
            {
                if (!activeNames.Contains(requiredFeature))
                {
                    throw new InvalidOperationException(
                        $"ULinkGame feature '{definition.Name}' requires feature '{requiredFeature}', but '{requiredFeature}' is not active.");
                }
            }
        }
    }

    private static IReadOnlyList<ULinkGameFeatureDefinition> SortAfterDependencies(
        IReadOnlyList<ULinkGameFeatureDefinition> active)
    {
        var remaining = active.ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ULinkGameFeatureDefinition>(active.Count);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in active)
        {
            Visit(definition, remaining, ordered, visiting, visited);
        }

        return ordered;
    }

    private static void Visit(
        ULinkGameFeatureDefinition definition,
        IReadOnlyDictionary<string, ULinkGameFeatureDefinition> active,
        List<ULinkGameFeatureDefinition> ordered,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(definition.Name))
        {
            return;
        }

        if (!visiting.Add(definition.Name))
        {
            throw new InvalidOperationException($"ULinkGame feature dependency cycle includes '{definition.Name}'.");
        }

        foreach (var dependencyName in definition.AfterFeatures)
        {
            if (active.TryGetValue(dependencyName, out var dependency))
            {
                Visit(dependency, active, ordered, visiting, visited);
            }
        }

        visiting.Remove(definition.Name);
        visited.Add(definition.Name);
        ordered.Add(definition);
    }
}
