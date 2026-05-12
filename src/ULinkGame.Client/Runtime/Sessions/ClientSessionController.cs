using ULinkGame.Abstractions;
using ULinkGame.Client.ReliablePush;

namespace ULinkGame.Client.Sessions
{
    public sealed class ClientSessionController
    {
        private readonly ReliablePushInbox _reliablePushInbox;

        public ClientSessionController(ReliablePushInbox? reliablePushInbox = null)
        {
            _reliablePushInbox = reliablePushInbox ?? new ReliablePushInbox();
            Snapshot = new ClientSessionSnapshot(ClientSessionPhase.SignedOut, null, 0);
        }

        public ClientSessionSnapshot Snapshot { get; private set; }

        public void MarkConnecting()
        {
            if (Snapshot.Phase != ClientSessionPhase.StateLost)
            {
                SetPhase(ClientSessionPhase.Connecting);
            }
        }

        public void StartSession(GameSessionKey session, long lastReliableSequence = 0)
        {
            _reliablePushInbox.StartSession(session, lastReliableSequence);
            Snapshot = new ClientSessionSnapshot(
                ClientSessionPhase.Active,
                session,
                _reliablePushInbox.LastAppliedSequence);
        }

        public void MarkReconnecting()
        {
            if (Snapshot.Phase != ClientSessionPhase.StateLost)
            {
                SetPhase(ClientSessionPhase.Reconnecting);
            }
        }

        public void ApplyAckOutcome(ReliablePushAckOutcome outcome)
        {
            if (Snapshot.Phase == ClientSessionPhase.StateLost)
            {
                return;
            }

            switch (outcome.Status)
            {
                case ReliablePushAckStatus.Accepted:
                case ReliablePushAckStatus.Duplicate:
                    break;
                case ReliablePushAckStatus.StateRefreshRequired:
                    SetPhase(ClientSessionPhase.RefreshRequired);
                    break;
                case ReliablePushAckStatus.StateLost:
                case ReliablePushAckStatus.SessionMismatch:
                    MarkStateLost();
                    break;
            }
        }

        public void MarkStateLost()
        {
            _reliablePushInbox.Reset();
            Snapshot = new ClientSessionSnapshot(ClientSessionPhase.StateLost, null, 0);
        }

        public void EndSession()
        {
            _reliablePushInbox.Reset();
            Snapshot = new ClientSessionSnapshot(ClientSessionPhase.SignedOut, null, 0);
        }

        private void SetPhase(ClientSessionPhase phase)
        {
            Snapshot = new ClientSessionSnapshot(
                phase,
                Snapshot.Session,
                _reliablePushInbox.LastAppliedSequence);
        }
    }
}
