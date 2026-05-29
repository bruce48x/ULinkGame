namespace ULinkGame.Server.Features;

public interface INodeRole
{
    string Name { get; }

    IFeature[] Features { get; }
}
