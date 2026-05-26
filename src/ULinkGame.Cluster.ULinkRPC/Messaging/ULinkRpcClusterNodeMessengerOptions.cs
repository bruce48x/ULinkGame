using System;
using ULinkGame.Cluster;

namespace ULinkGame.Cluster.ULinkRPC
{
    public sealed class ULinkRpcClusterNodeMessengerOptions
    {
        public TimeSpan? SendTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public Func<Exception, ClusterSendStatus>? ExceptionMapper { get; set; }
    }
}
