namespace Edge.Hosting;

internal sealed class ControlPlaneRpcServerOptions
{
    public ControlPlaneRpcServerOptions(EdgeRpcServerOptions endpoint)
    {
        Endpoint = endpoint;
    }

    public EdgeRpcServerOptions Endpoint { get; }
}
