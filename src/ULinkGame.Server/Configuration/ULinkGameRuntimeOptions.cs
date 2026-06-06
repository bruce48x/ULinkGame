using Microsoft.Extensions.Configuration;

namespace ULinkGame.Server.Configuration;

public sealed class ULinkGameRuntimeOptions
{
    public ULinkGameNodeOptions Node { get; init; } = new();
    public IReadOnlyList<ULinkGameEndpointOptions> Endpoints { get; init; } = [];
    public IReadOnlyList<string>? Feature { get; init; }
    public ULinkGameClusterOptions? Cluster { get; init; }
    public string ClusterEndpoint { get; init; } = "tcp://127.0.0.1:21000";

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

    public ServerRpcServerOptions ToServerRpcServerOptions(string transport)
    {
        var endpoint = FindEndpoint(transport);
        return new ServerRpcServerOptions
        {
            Transport = endpoint.Transport,
            Host = endpoint.Host,
            Port = endpoint.Port,
            Path = string.IsNullOrWhiteSpace(endpoint.Path) ? endpoint.GetDefaultPath() : endpoint.Path
        };
    }

    public ClusterOptions ToClusterOptions(string transport)
    {
        var endpoint = FindEndpoint(transport);
        return new ClusterOptions
        {
            NodeId = Node.Id,
            AdvertisedEndpoints = new Dictionary<string, string>
            {
                [transport] = endpoint.ToAdvertisedEndpoint(),
                ["cluster"] = ClusterEndpoint
            }
        };
    }

    public ClusterOptions ToClusterOptions(IConfiguration configuration, string transport)
    {
        var defaults = ToClusterOptions(transport);
        var section = configuration.GetSection("ULinkGame:Cluster");

        return new ClusterOptions
        {
            NodeId = ReadString(section, "NodeId", defaults.NodeId),
            AdvertisedEndpoints = ReadDictionary(
                section.GetSection("AdvertisedEndpoints"), defaults.AdvertisedEndpoints),
            Bootstrap = ClusterBootstrapOptions.FromConfiguration(
                section.GetSection("Bootstrap"), defaults.Bootstrap),
            NodeDirectory = ClusterNodeDirectoryOptions.FromConfiguration(
                section.GetSection("NodeDirectory"), defaults.NodeDirectory),
            Services = ReadServices(section.GetSection("Services"), defaults.Services),
            RouteLeaseSeconds = ReadInt(section, "RouteLeaseSeconds", defaults.RouteLeaseSeconds),
            SendTimeoutMilliseconds = ReadInt(section, "SendTimeoutMilliseconds", defaults.SendTimeoutMilliseconds)
        };
    }

    private ULinkGameEndpointOptions FindEndpoint(string transport)
    {
        return Endpoints.FirstOrDefault(e =>
            string.Equals(e.Transport, transport, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No endpoint configured for transport '{transport}'.");
    }

    private static ULinkGameNodeOptions BindNode(IConfiguration section)
    {
        return ULinkGameNodeOptions.FromConfiguration(section);
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

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadInt(IConfiguration section, string name, int fallback)
    {
        return int.TryParse(section[name], out var parsed) ? parsed : fallback;
    }

    private static IReadOnlyDictionary<string, string> ReadDictionary(
        IConfigurationSection section,
        IReadOnlyDictionary<string, string> fallback)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            return fallback;
        }

        return children.ToDictionary(child => child.Key, child => child.Value ?? "");
    }

    private static IReadOnlyList<ClusterServiceOptions> ReadServices(
        IConfigurationSection section,
        IReadOnlyList<ClusterServiceOptions> fallback)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            return fallback;
        }

        return children.Select(child => new ClusterServiceOptions
        {
            Kind = child["Kind"] ?? "",
            Name = child["Name"] ?? ""
        }).ToArray();
    }
}

public sealed class ULinkGameNodeOptions
{
    public string Id { get; init; } = "dev-1";

    public static ULinkGameNodeOptions FromConfiguration(IConfiguration section)
    {
        return new ULinkGameNodeOptions
        {
            Id = ReadString(section, "Id", "dev-1")
        };
    }

    private static string ReadString(IConfiguration section, string name, string fallback)
    {
        var value = section[name];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
