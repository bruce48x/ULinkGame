using System;
using ULinkRPC.Core;

namespace ULinkGame.Cluster.ULinkRPC
{
    public sealed class ULinkRpcClusterClientFactoryOptions
    {
        public RpcKeepAliveOptions KeepAlive { get; set; } = RpcKeepAliveOptions.Disabled;

        public TimeSpan? ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
