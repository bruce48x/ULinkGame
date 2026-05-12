using System.Threading;
using System.Threading.Tasks;
using ULinkGame.Abstractions;

namespace ULinkGame.Client.ReliablePush
{
    public interface IReliablePushCursorStore
    {
        ValueTask<long> LoadAsync(
            GameSessionKey session,
            CancellationToken cancellationToken = default);

        ValueTask SaveAsync(
            GameSessionKey session,
            long sequence,
            CancellationToken cancellationToken = default);

        ValueTask ClearAsync(
            GameSessionKey session,
            CancellationToken cancellationToken = default);
    }
}
