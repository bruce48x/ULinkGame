using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Client.ReliablePush
{
    public sealed class InMemoryReliablePushCursorStore : IReliablePushCursorStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<ReliablePushSession, long> _sequences = new Dictionary<ReliablePushSession, long>();

        public ValueTask<long> LoadAsync(
            ReliablePushSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return new ValueTask<long>(_sequences.TryGetValue(session, out var sequence) ? sequence : 0);
            }
        }

        public ValueTask SaveAsync(
            ReliablePushSession session,
            long sequence,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _sequences[session] = sequence <= 0 ? 0 : sequence;
            }

            return default;
        }

        public ValueTask ClearAsync(
            ReliablePushSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _sequences.Remove(session);
            }

            return default;
        }
    }
}

