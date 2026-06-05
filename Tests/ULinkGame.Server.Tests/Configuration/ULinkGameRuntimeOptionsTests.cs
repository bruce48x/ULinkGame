using Microsoft.Extensions.Configuration;
using ULinkGame.Server.Configuration;
using Xunit;

namespace ULinkGame.Server.Tests.Configuration;

public sealed class ULinkGameRuntimeOptionsTests
{
    [Fact]
    public void FromConfiguration_binds_node_endpoints_feature_and_cluster()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "game-c",
            ["ULinkGame:Endpoints:0:Transport"] = "websocket",
            ["ULinkGame:Endpoints:0:Host"] = "0.0.0.0",
            ["ULinkGame:Endpoints:0:Port"] = "20000",
            ["ULinkGame:Endpoints:0:Path"] = "/ws",
            ["ULinkGame:Endpoints:1:Transport"] = "kcp",
            ["ULinkGame:Endpoints:1:Host"] = "0.0.0.0",
            ["ULinkGame:Endpoints:1:Port"] = "20001",
            ["ULinkGame:Feature:0"] = "battle",
            ["ULinkGame:Feature:1"] = "battle-settlement",
            ["ULinkGame:Cluster:Endpoint"] = "tcp://10.0.0.3:21003",
            ["ULinkGame:Cluster:Seeds:0"] = "tcp://10.0.0.1:21001"
        });

        var options = ULinkGameRuntimeOptions.FromConfiguration(configuration);

        Assert.Equal("game-c", options.Node.Id);
        Assert.Collection(
            options.Endpoints,
            endpoint =>
            {
                Assert.Equal("websocket", endpoint.Transport);
                Assert.Equal("0.0.0.0", endpoint.Host);
                Assert.Equal(20000, endpoint.Port);
                Assert.Equal("/ws", endpoint.Path);
            },
            endpoint =>
            {
                Assert.Equal("kcp", endpoint.Transport);
                Assert.Equal("0.0.0.0", endpoint.Host);
                Assert.Equal(20001, endpoint.Port);
                Assert.Equal("", endpoint.Path);
            });
        Assert.Equal(["battle", "battle-settlement"], options.Feature);
        Assert.NotNull(options.Cluster);
        Assert.Equal("tcp://10.0.0.3:21003", options.Cluster.Endpoint);
        Assert.Equal(["tcp://10.0.0.1:21001"], options.Cluster.Seeds);
    }

    [Fact]
    public void FromConfiguration_defaults_feature_to_null_and_cluster_to_null()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ULinkGame:Node:Id"] = "dev-1"
        });

        var options = ULinkGameRuntimeOptions.FromConfiguration(configuration);

        Assert.Equal("dev-1", options.Node.Id);
        Assert.Empty(options.Endpoints);
        Assert.Null(options.Feature);
        Assert.Null(options.Cluster);
    }

    [Fact]
    public void ToAdvertisedEndpoint_maps_websocket_to_ws()
    {
        var endpoint = new ULinkGameEndpointOptions
        {
            Transport = "websocket",
            Host = "127.0.0.1",
            Port = 20000,
            Path = "/ws"
        };

        Assert.Equal("ws://127.0.0.1:20000/ws", endpoint.ToAdvertisedEndpoint());
    }

    [Fact]
    public void ToAdvertisedEndpoint_uses_advertised_host_when_present()
    {
        var endpoint = new ULinkGameEndpointOptions
        {
            Transport = "kcp",
            Host = "0.0.0.0",
            AdvertisedHost = "game.example.com",
            Port = 20001
        };

        Assert.Equal("kcp://game.example.com:20001", endpoint.ToAdvertisedEndpoint());
    }

    [Fact]
    public void ToAdvertisedEndpoint_preserves_unknown_transport()
    {
        var endpoint = new ULinkGameEndpointOptions
        {
            Transport = "quic",
            Host = "127.0.0.1",
            Port = 20002
        };

        Assert.Equal("quic://127.0.0.1:20002", endpoint.ToAdvertisedEndpoint());
    }

    [Fact]
    public void ToAdvertisedEndpoint_normalizes_transport_case()
    {
        var endpoint = new ULinkGameEndpointOptions
        {
            Transport = "WebSocket",
            Host = "127.0.0.1",
            Port = 20003,
            Path = "/ws"
        };

        Assert.Equal("ws://127.0.0.1:20003/ws", endpoint.ToAdvertisedEndpoint());
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
