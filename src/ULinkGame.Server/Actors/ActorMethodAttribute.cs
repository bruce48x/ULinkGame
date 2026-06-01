namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ActorMethodAttribute : Attribute
{
    public ActorMethodAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
}
