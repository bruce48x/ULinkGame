using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Client.ReliablePush
{
    public interface IReliablePushCursorStore
    {
        ValueTask<long> LoadAsync(
            ReliablePushSession session,
            CancellationToken cancellationToken = default);

        ValueTask SaveAsync(
            ReliablePushSession session,
            long sequence,
            CancellationToken cancellationToken = default);

        ValueTask ClearAsync(
            ReliablePushSession session,
            CancellationToken cancellationToken = default);
    }
}

