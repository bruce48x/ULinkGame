namespace Edge.Hosting;

internal sealed class RealtimeRpcServerOptions
{
    public RealtimeRpcServerOptions(EdgeRpcServerOptions endpoint)
    {
        Endpoint = endpoint;
    }

    public EdgeRpcServerOptions Endpoint { get; }
}
