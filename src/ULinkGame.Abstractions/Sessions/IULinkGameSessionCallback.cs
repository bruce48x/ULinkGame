using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Abstractions
{
    public interface IULinkGameSessionCallback
    {
        ValueTask OnSessionTerminatedAsync(
            SessionTerminationNotice notice,
            CancellationToken cancellationToken = default);
    }
}
