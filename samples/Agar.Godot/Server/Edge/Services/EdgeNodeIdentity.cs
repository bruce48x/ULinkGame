using Microsoft.Extensions.Configuration;
using Orleans.Contracts;
using Edge.Hosting;
using Shared.Interfaces;

namespace Edge.Services;

internal sealed class EdgeNodeIdentity
{
    public EdgeNodeIdentity(IConfiguration configuration, RealtimeRpcServerOptions realtimeOptions)
    {
        InstanceId = configuration["Edge:NodeId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(InstanceId))
        {
            InstanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
        }

        RealtimeEndpoint = new EdgeEndpointDescriptor
        {
            InstanceId = InstanceId,
            Transport = RealtimeTransportToString(realtimeOptions.Endpoint.Transport),
            Host = realtimeOptions.Endpoint.Host,
            Port = realtimeOptions.Endpoint.Port,
            Path = realtimeOptions.Endpoint.Path
        };
    }

    public string InstanceId { get; }

    public EdgeEndpointDescriptor RealtimeEndpoint { get; }

    public bool IsRuntimeOwner(EdgeEndpointDescriptor? edge)
    {
        return edge is not null
            && !string.IsNullOrWhiteSpace(edge.InstanceId)
            && string.Equals(edge.InstanceId, InstanceId, StringComparison.Ordinal);
    }

    private static string RealtimeTransportToString(string transport) =>
        string.IsNullOrWhiteSpace(transport) ? "unknown" : transport.ToLowerInvariant();
}
