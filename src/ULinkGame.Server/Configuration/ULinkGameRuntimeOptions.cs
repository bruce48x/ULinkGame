using Microsoft.Extensions.Configuration;

namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameRuntimeOptions
{
    public ULinkGameNodeOptions Node { get; init; } = new();
    public IReadOnlyList<ULinkGameEndpointOptions> Endpoints { get; init; } = [];
    public IReadOnlyList<string>? Feature { get; init; }
    public ULinkGameClusterOptions? Cluster { get; init; }

    public static ULinkGameRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("ULinkGame");

        return new ULinkGameRuntimeOptions
        {
            Node = BindNode(section.GetSection("Node")),
            Endpoints = BindEndpoints(section.GetSection("Endpoints")),
            Feature = BindOptionalStringArray(section.GetSection("Feature")),
            Cluster = BindCluster(section.GetSection("Cluster"))
        };
    }

    private static ULinkGameNodeOptions BindNode(IConfiguration section)
    {
        return new ULinkGameNodeOptions
        {
            Id = section["Id"] ?? ""
        };
    }

    private static IReadOnlyList<ULinkGameEndpointOptions> BindEndpoints(IConfiguration section)
    {
        return section
            .GetChildren()
            .Select(endpoint => new ULinkGameEndpointOptions
            {
                Transport = endpoint["Transport"] ?? "",
                Host = endpoint["Host"] ?? "",
                Port = ReadInt(endpoint["Port"]),
                Path = endpoint["Path"] ?? "",
                AdvertisedHost = endpoint["AdvertisedHost"] ?? ""
            })
            .ToArray();
    }

    private static IReadOnlyList<string>? BindOptionalStringArray(IConfiguration section)
    {
        var values = section
            .GetChildren()
            .Select(child => child.Value ?? "")
            .ToArray();

        return values.Length == 0 ? null : values;
    }

    private static ULinkGameClusterOptions? BindCluster(IConfiguration section)
    {
        if (!section.GetChildren().Any())
        {
            return null;
        }

        return new ULinkGameClusterOptions
        {
            Endpoint = section["Endpoint"] ?? "",
            Seeds = BindStringArray(section.GetSection("Seeds"))
        };
    }

    private static IReadOnlyList<string> BindStringArray(IConfiguration section)
    {
        return section
            .GetChildren()
            .Select(child => child.Value ?? "")
            .ToArray();
    }

    private static int ReadInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }
}

public sealed class ULinkGameNodeOptions
{
    public string Id { get; init; } = "";
}
