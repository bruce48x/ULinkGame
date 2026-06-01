namespace ULinkGame.Server.Actors;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ActorNameAttribute : Attribute
{
    public ActorNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
}
