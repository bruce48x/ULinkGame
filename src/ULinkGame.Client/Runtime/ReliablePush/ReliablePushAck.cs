namespace ULinkGame.Client.ReliablePush
{
    public readonly struct ReliablePushAck
    {
        public ReliablePushAck(ReliablePushSession session, ReliablePushSequence sequence)
        {
            Session = session;
            Sequence = sequence;
        }

        public ReliablePushSession Session { get; }

        public ReliablePushSequence Sequence { get; }
    }
}

