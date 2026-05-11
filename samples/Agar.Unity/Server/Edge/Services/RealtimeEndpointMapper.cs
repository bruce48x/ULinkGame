using Orleans.Contracts;
using Shared.Interfaces;

namespace Edge.Services;

internal static class RealtimeEndpointMapper
{
    public static RealtimeConnectionInfo ToRealtimeConnectionInfo(
        EdgeEndpointDescriptor edge,
        string roomId,
        string matchId,
        string sessionToken)
    {
        return new RealtimeConnectionInfo
        {
            Transport = ParseTransport(edge.Transport),
            Host = edge.Host,
            Port = edge.Port,
            Path = edge.Path,
            RoomId = roomId,
            MatchId = matchId,
            SessionToken = sessionToken
        };
    }

    public static RealtimeTransportKind ParseTransport(string? transport)
    {
        if (string.Equals(transport, "kcp", StringComparison.OrdinalIgnoreCase))
        {
            return RealtimeTransportKind.Kcp;
        }

        if (string.Equals(transport, "ws", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase))
        {
            return RealtimeTransportKind.WebSocket;
        }

        return RealtimeTransportKind.Unknown;
    }
}
