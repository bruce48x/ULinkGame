using Xunit;

namespace ULinkGame.Server.Generators.Tests;

public sealed class TypedActorGeneratorTests
{
    [Fact]
    public void Generator_emits_local_and_remote_refs_for_actor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct RoomId(string Value);

            public sealed class JoinRoomRequest
            {
            }

            public sealed class JoinRoomReply
            {
            }

            public sealed class RoomActor : Actor<RoomId>
            {
                public ValueTask<JoinRoomReply> JoinAsync(JoinRoomRequest request, CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(new JoinRoomReply());
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("public sealed class RoomActors", result.GeneratedSource);
        Assert.Contains("public RoomLocalRef Local(RoomId id)", result.GeneratedSource);
        Assert.Contains("public RoomRemoteRef Remote(global::ULinkGame.Cluster.NodeId node, RoomId id)", result.GeneratedSource);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync", result.GeneratedSource);
        Assert.Contains("private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;", result.GeneratedSource);
        Assert.Contains("return _runtime.AskAsync<global::Game.Server.RoomActor, JoinRoomReply>", result.GeneratedSource);
        Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"room/\" + _id.Value)", result.GeneratedSource);
    }

    [Fact]
    public void Generator_ignores_non_actor_classes()
    {
        var source = """
            namespace Game.Server;

            public sealed class RoomActor
            {
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Equal(string.Empty, result.GeneratedSource);
    }
}
