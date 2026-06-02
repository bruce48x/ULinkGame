using ULinkGame.Server.Features;

namespace Gateway.Features;

public sealed class GatewayRole : INodeRole
{
    public string Name => "gateway";

    public IFeature[] Features => [new GatewayCoreFeature(), new GatewayBusinessFeature()];
}
