using ULinkRPC.Core;

namespace ULinkGame.Cluster.ULinkRPC
{
    public static class ULinkRpcClusterProtocol
    {
        public const int ServiceId = 0x554C4301;

        public const int SendMethodId = 1;

        public const int RegisterRouteMethodId = 10;

        public const int ResolveRouteMethodId = 11;

        public const int RefreshRouteLeaseMethodId = 12;

        public const int ExpireRoutesMethodId = 13;

        public const int ClearRoutesByNodeMethodId = 14;

        public const int ClearRoutesByNodeEpochMethodId = 15;

        public static readonly RpcMethod<ULinkRpcClusterSendRequest, ULinkRpcClusterSendReply> SendMethod =
            new RpcMethod<ULinkRpcClusterSendRequest, ULinkRpcClusterSendReply>(ServiceId, SendMethodId);

        public static readonly RpcMethod<ULinkRpcRouteRegisterRequest, ULinkRpcRouteRegisterReply> RegisterRouteMethod =
            new RpcMethod<ULinkRpcRouteRegisterRequest, ULinkRpcRouteRegisterReply>(ServiceId, RegisterRouteMethodId);

        public static readonly RpcMethod<ULinkRpcRouteResolveRequest, ULinkRpcRouteResolveReply> ResolveRouteMethod =
            new RpcMethod<ULinkRpcRouteResolveRequest, ULinkRpcRouteResolveReply>(ServiceId, ResolveRouteMethodId);

        public static readonly RpcMethod<ULinkRpcRouteRefreshLeaseRequest, ULinkRpcRouteRefreshLeaseReply> RefreshRouteLeaseMethod =
            new RpcMethod<ULinkRpcRouteRefreshLeaseRequest, ULinkRpcRouteRefreshLeaseReply>(ServiceId, RefreshRouteLeaseMethodId);

        public static readonly RpcMethod<ULinkRpcRouteExpireRequest, ULinkRpcRouteExpireReply> ExpireRoutesMethod =
            new RpcMethod<ULinkRpcRouteExpireRequest, ULinkRpcRouteExpireReply>(ServiceId, ExpireRoutesMethodId);

        public static readonly RpcMethod<ULinkRpcRouteClearByNodeRequest, ULinkRpcRouteClearReply> ClearRoutesByNodeMethod =
            new RpcMethod<ULinkRpcRouteClearByNodeRequest, ULinkRpcRouteClearReply>(ServiceId, ClearRoutesByNodeMethodId);

        public static readonly RpcMethod<ULinkRpcRouteClearByNodeEpochRequest, ULinkRpcRouteClearReply> ClearRoutesByNodeEpochMethod =
            new RpcMethod<ULinkRpcRouteClearByNodeEpochRequest, ULinkRpcRouteClearReply>(ServiceId, ClearRoutesByNodeEpochMethodId);
    }
}
