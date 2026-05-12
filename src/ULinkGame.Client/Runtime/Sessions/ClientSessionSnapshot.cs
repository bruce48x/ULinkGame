using ULinkGame.Abstractions;

namespace ULinkGame.Client.Sessions
{
    public readonly struct ClientSessionSnapshot
    {
        public ClientSessionSnapshot(
            ClientSessionPhase phase,
            GameSessionKey? session,
            long lastReliableSequence)
        {
            Phase = phase;
            Session = session;
            LastReliableSequence = lastReliableSequence;
        }

        public ClientSessionPhase Phase { get; }

        public GameSessionKey? Session { get; }

        public long LastReliableSequence { get; }
    }
}
