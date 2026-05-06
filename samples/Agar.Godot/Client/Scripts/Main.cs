using Godot;
using Shared.Gameplay;
using Shared.Interfaces;
using ULinkGame.Client.ReliablePush;

namespace Agar.Godot.Scripts;

public partial class Main : Node2D
{
    private readonly ReliablePushTracker _pushTracker = new();
    private ArenaSimulation _simulation = null!;
    private WorldState _worldState = new();

    public override void _Ready()
    {
        _simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = true,
            TargetParticipantCount = 4
        });

        _simulation.UpsertPlayer(new ArenaPlayerRegistration
        {
            PlayerId = "godot-player",
            Score = 1
        });

        for (var i = 1; i <= 3; i++)
        {
            _simulation.UpsertPlayer(new ArenaPlayerRegistration
            {
                PlayerId = $"bot-{i}",
                IsBot = true,
                BotNumber = i,
                Score = 1
            });
        }

        _worldState = _simulation.CreateWorldState();
        _pushTracker.MarkApplied(1);
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        _simulation.SubmitInput(new InputMessage
        {
            PlayerId = "godot-player",
            MoveX = direction.X,
            MoveY = direction.Y,
            Tick = _simulation.TickCount
        });

        _worldState = _simulation.Tick((float)delta).WorldState;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawString(ThemeDB.FallbackFont, new Vector2(24, 32), $"Agar.Godot local sim | tick {_worldState.Tick} | reliable seq {_pushTracker.LastAppliedSequence}");

        var center = GetViewportRect().Size * 0.5f;
        foreach (var pickup in _worldState.Pickups)
        {
            DrawCircle(center + new Vector2(pickup.X, pickup.Y) * 6f, 2.5f, new Color(0.42f, 0.85f, 0.52f));
        }

        foreach (var player in _worldState.Players)
        {
            var color = player.PlayerId == "godot-player"
                ? new Color(0.2f, 0.62f, 1f)
                : new Color(1f, 0.56f, 0.24f);

            DrawCircle(center + new Vector2(player.X, player.Y) * 6f, Mathf.Max(6f, player.Radius * 6f), color);
        }
    }
}
