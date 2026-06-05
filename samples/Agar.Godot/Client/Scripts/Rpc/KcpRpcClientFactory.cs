#nullable enable

using System;
using Rpc.Generated;
using ULinkRPC.Client;
using ULinkRPC.Core;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;

namespace Rpc;

public static class KcpRpcClientFactory
{
    public static RpcClient Create(string host, int port, RpcClient.RpcNotificationBindings callbacks)
    {
        return new RpcClient(
            new RpcClientOptions(
                new KcpTransport(host, port),
                new MemoryPackRpcSerializer())
            {
                KeepAlive = new RpcKeepAliveOptions
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(6)
                }
            },
            callbacks);
    }
}
