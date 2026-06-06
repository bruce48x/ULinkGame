using System.Text.Json;
using Gateway.Hosting;
using ULinkGame.Server.Configuration;
using Xunit;

namespace Agar.Unity.Tests;

public sealed class GatewayConfigurationTests
{
    [Fact]
    public void AppsettingsUsesCanonicalULinkGameEndpointConfiguration()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Agar.Unity",
            "Server",
            "Gateway",
            "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("ControlPlane", out _));
        Assert.False(root.TryGetProperty("Realtime", out _));
        Assert.False(root.TryGetProperty("Gateway", out _));
        Assert.False(root.TryGetProperty("Hotfix", out _));
        Assert.False(root.TryGetProperty("Deployment", out _));
        Assert.False(root.TryGetProperty("Services", out _));
        Assert.False(root.TryGetProperty("Cluster", out _));

        var ulinkGame = root.GetProperty("ULinkGame");
        Assert.Equal("gateway-dev-1", ulinkGame.GetProperty("Node").GetProperty("Id").GetString());

        var endpoints = ulinkGame.GetProperty("Endpoints").EnumerateArray().ToArray();
        Assert.Equal(2, endpoints.Length);

        var control = Assert.Single(endpoints, endpoint =>
            string.Equals(endpoint.GetProperty("Transport").GetString(), "websocket", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("127.0.0.1", control.GetProperty("Host").GetString());
        Assert.Equal(20000, control.GetProperty("Port").GetInt32());
        Assert.Equal("/ws", control.GetProperty("Path").GetString());

        var realtime = Assert.Single(endpoints, endpoint =>
            string.Equals(endpoint.GetProperty("Transport").GetString(), "kcp", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("127.0.0.1", realtime.GetProperty("Host").GetString());
        Assert.Equal(20001, realtime.GetProperty("Port").GetInt32());
        Assert.Equal("", realtime.GetProperty("Path").GetString());
    }

    [Fact]
    public void GatewayOptionsUseAdvertisedHostFromCanonicalEndpoint()
    {
        var endpoint = new ULinkGameEndpointOptions
        {
            Transport = "kcp",
            Host = "0.0.0.0",
            AdvertisedHost = "game.example.com",
            Port = 20001
        };

        var options = GatewayRpcServerOptions.FromEndpoint(endpoint);

        Assert.Equal("kcp", options.Transport);
        Assert.Equal("game.example.com", options.Host);
        Assert.Equal(20001, options.Port);
        Assert.Equal("", options.Path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CONTRIBUTING.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "samples", "Agar.Unity")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository root from '{AppContext.BaseDirectory}'.");
    }
}
