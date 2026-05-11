namespace ULinkGame.Client.ReliablePush
{
    public readonly struct ReliablePushAckOutcome
    {
        public ReliablePushAckOutcome(ReliablePushAckStatus status, string? reason = null)
        {
            Status = status;
            Reason = reason;
        }

        public ReliablePushAckStatus Status { get; }

        public string? Reason { get; }

        public static ReliablePushAckOutcome Accepted()
        {
            return new ReliablePushAckOutcome(ReliablePushAckStatus.Accepted);
        }

        public static ReliablePushAckOutcome Duplicate()
        {
            return new ReliablePushAckOutcome(ReliablePushAckStatus.Duplicate);
        }

        public static ReliablePushAckOutcome StateRefreshRequired(string? reason = null)
        {
            return new ReliablePushAckOutcome(ReliablePushAckStatus.StateRefreshRequired, reason);
        }

        public static ReliablePushAckOutcome StateLost(string? reason = null)
        {
            return new ReliablePushAckOutcome(ReliablePushAckStatus.StateLost, reason);
        }

        public static ReliablePushAckOutcome SessionMismatch(string? reason = null)
        {
            return new ReliablePushAckOutcome(ReliablePushAckStatus.SessionMismatch, reason);
        }
    }
}

