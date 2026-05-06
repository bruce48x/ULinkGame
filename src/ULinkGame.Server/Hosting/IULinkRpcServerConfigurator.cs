namespace ULinkGame.Server.Hosting;

public interface IULinkRpcServerConfigurator
{
    string Name { get; }

    void Configure(ULinkGameServerRpcContext context);
}
