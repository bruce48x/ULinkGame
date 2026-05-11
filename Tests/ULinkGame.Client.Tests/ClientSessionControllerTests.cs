using ULinkGame.Client.ReliablePush;
using ULinkGame.Client.Sessions;
using Xunit;

namespace ULinkGame.Client.Tests;

public sealed class ClientSessionControllerTests
{
    [Fact]
    public void StartSessionMakesControllerActive()
    {
        var controller = new ClientSessionController();
        var session = new ReliablePushSession("player-a", "session-a", 1);

        controller.StartSession(session, lastReliableSequence: 7);

        Assert.Equal(ClientSessionPhase.Active, controller.Snapshot.Phase);
        Assert.Equal(session, controller.Snapshot.ReliablePushSession);
        Assert.Equal(7, controller.Snapshot.LastReliableSequence);
    }

    [Fact]
    public void RefreshRequiredDoesNotClearSession()
    {
        var controller = new ClientSessionController();
        var session = new ReliablePushSession("player-a", "session-a", 1);
        controller.StartSession(session, lastReliableSequence: 7);

        controller.ApplyAckOutcome(ReliablePushAckOutcome.StateRefreshRequired());

        Assert.Equal(ClientSessionPhase.RefreshRequired, controller.Snapshot.Phase);
        Assert.Equal(session, controller.Snapshot.ReliablePushSession);
        Assert.Equal(7, controller.Snapshot.LastReliableSequence);
    }

    [Fact]
    public void StateLostClearsReliableStateAndIsTerminalUntilNewSession()
    {
        var controller = new ClientSessionController();
        var session = new ReliablePushSession("player-a", "session-a", 1);
        controller.StartSession(session, lastReliableSequence: 7);

        controller.ApplyAckOutcome(ReliablePushAckOutcome.SessionMismatch());
        controller.MarkReconnecting();

        Assert.Equal(ClientSessionPhase.StateLost, controller.Snapshot.Phase);
        Assert.Null(controller.Snapshot.ReliablePushSession);
        Assert.Equal(0, controller.Snapshot.LastReliableSequence);

        var next = new ReliablePushSession("player-a", "session-b", 2);
        controller.StartSession(next);

        Assert.Equal(ClientSessionPhase.Active, controller.Snapshot.Phase);
        Assert.Equal(next, controller.Snapshot.ReliablePushSession);
    }

    [Fact]
    public void DuplicateAckDoesNotChangePhase()
    {
        var controller = new ClientSessionController();
        controller.StartSession(new ReliablePushSession("player-a", "session-a", 1));
        controller.MarkReconnecting();

        controller.ApplyAckOutcome(ReliablePushAckOutcome.Duplicate());

        Assert.Equal(ClientSessionPhase.Reconnecting, controller.Snapshot.Phase);
    }
}

