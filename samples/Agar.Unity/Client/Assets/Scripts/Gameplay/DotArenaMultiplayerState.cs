#nullable enable

using Shared.Interfaces;
using ULinkGame.Client.ReliablePush;

namespace SampleClient.Gameplay
{
    internal sealed class DotArenaMultiplayerState
    {
        public SessionMode SessionMode { get; set; } = SessionMode.None;
        public string LocalPlayerId { get; set; } = string.Empty;
        public bool HasAuthenticatedProfile { get; set; }
        public string AuthenticatedPlayerId { get; set; } = string.Empty;
        public int LocalWinCount { get; set; }
        public PendingUiRequest PendingUiRequest { get; set; }
        public bool ControlReconnectInProgress { get; set; }
        public float MatchmakingStartedAt { get; set; } = -1f;
        public RealtimeConnectionInfo? LastRealtimeConnection { get; set; }
        public ReliablePushInbox ReliablePushInbox { get; } = new();

        public bool HasPendingUiRequest => PendingUiRequest != PendingUiRequest.None;
        public bool HasRecoverableLogin => SessionMode == SessionMode.Multiplayer && HasAuthenticatedProfile;

        public DotArenaAuthenticatedProfile CaptureAuthenticatedProfile()
        {
            return new DotArenaAuthenticatedProfile(HasAuthenticatedProfile, AuthenticatedPlayerId, LocalWinCount);
        }

        public void ClearAuthenticatedProfile()
        {
            HasAuthenticatedProfile = false;
            AuthenticatedPlayerId = string.Empty;
            LocalWinCount = 0;
        }

        public void ApplyAuthenticatedProfile(string playerId, int winCount)
        {
            HasAuthenticatedProfile = true;
            AuthenticatedPlayerId = playerId;
            LocalWinCount = winCount < 0 ? 0 : winCount;
        }

        public void RestoreAuthenticatedProfile(DotArenaAuthenticatedProfile profile)
        {
            HasAuthenticatedProfile = profile.HasAuthenticatedProfile;
            AuthenticatedPlayerId = profile.PlayerId;
            LocalWinCount = profile.WinCount;
        }

        public void ApplyMultiplayerLogin(string playerId, string sessionToken, int winCount)
        {
            LocalPlayerId = playerId;
            SessionMode = SessionMode.Multiplayer;
            ApplyAuthenticatedProfile(playerId, winCount);
            StartReliablePushSession(playerId, sessionToken);
        }

        public void ApplyControlReconnect(string playerId, string sessionToken, int winCount)
        {
            LocalPlayerId = playerId;
            SessionMode = SessionMode.Multiplayer;
            ApplyAuthenticatedProfile(playerId, winCount);
            StartReliablePushSession(playerId, sessionToken);
        }

        public void ClearSession()
        {
            SessionMode = SessionMode.None;
            LocalPlayerId = string.Empty;
        }

        public void ClearAll()
        {
            ClearSession();
            ClearAuthenticatedProfile();
            ClearRequestState(resetReliablePush: true);
        }

        public void ClearRequestState(bool resetReliablePush)
        {
            PendingUiRequest = PendingUiRequest.None;
            ControlReconnectInProgress = false;
            LastRealtimeConnection = null;
            MatchmakingStartedAt = -1f;

            if (resetReliablePush)
            {
                ReliablePushInbox.Reset();
            }
        }

        private void StartReliablePushSession(string playerId, string sessionToken)
        {
            var sessionId = string.IsNullOrWhiteSpace(sessionToken) ? playerId : sessionToken;
            ReliablePushInbox.StartSession(new ReliablePushSession(playerId, sessionId, generation: 1));
        }
    }

    internal readonly struct DotArenaAuthenticatedProfile
    {
        public DotArenaAuthenticatedProfile(bool hasAuthenticatedProfile, string playerId, int winCount)
        {
            HasAuthenticatedProfile = hasAuthenticatedProfile;
            PlayerId = playerId;
            WinCount = winCount;
        }

        public bool HasAuthenticatedProfile { get; }
        public string PlayerId { get; }
        public int WinCount { get; }
    }
}
