namespace ULinkGame.Server.Actors;

public abstract class Actor : IActor
{
    public ActorContext Context { get; private set; } = ActorContext.Uninitialized;

    internal async ValueTask ActivateAsync(ActorContext context, CancellationToken cancellationToken)
    {
        Context = context;
        await OnActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual ValueTask OnActivateAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
