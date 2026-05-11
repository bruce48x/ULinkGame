using ULinkGame.Client.ReliablePush;

namespace ULinkGame.Client.Sessions
{
    public readonly struct ClientSessionSnapshot
    {
        public ClientSessionSnapshot(
            ClientSessionPhase phase,
            ReliablePushSession? reliablePushSession,
            long lastReliableSequence)
        {
            Phase = phase;
            ReliablePushSession = reliablePushSession;
            LastReliableSequence = lastReliableSequence;
        }

        public ClientSessionPhase Phase { get; }

        public ReliablePushSession? ReliablePushSession { get; }

        public long LastReliableSequence { get; }
    }
}

