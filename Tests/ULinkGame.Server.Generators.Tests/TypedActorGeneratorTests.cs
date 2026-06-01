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
        Assert.Contains("public RoomRemoteRef Remote(global::ULinkGame.Cluster.NodeId nodeId, RoomId id)", result.GeneratedSource);
        Assert.Contains("return new RoomRemoteRef(_remote, _serializer, _options, nodeId, id);", result.GeneratedSource);
        Assert.Contains("global::ULinkGame.Cluster.NodeId nodeId,", result.GeneratedSource);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync", result.GeneratedSource);
        Assert.Contains("private readonly global::ULinkGame.Server.Actors.IActorRuntime _runtime;", result.GeneratedSource);
        Assert.Contains("return _runtime.AskAsync<global::Game.Server.RoomActor, JoinRoomReply>", result.GeneratedSource);
        Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"room/\" + _id.Value)", result.GeneratedSource);
        Assert.Contains("public async global::System.Threading.Tasks.ValueTask<JoinRoomReply> JoinAsync", result.GeneratedSource);
        Assert.Contains("var payload = _serializer.Serialize(request);", result.GeneratedSource);
        Assert.Contains("new global::ULinkGame.Server.Actors.RemoteActorInvocation(_node, actorId, \"room\", \"join\", payload, deadline, correlationId)", result.GeneratedSource);
        Assert.Contains("var result = await _remote.AskAsync(invocation, cancellationToken).ConfigureAwait(false);", result.GeneratedSource);
        Assert.Contains("if (result.Status != global::ULinkGame.Server.Actors.RemoteActorStatus.Replied)", result.GeneratedSource);
        Assert.Contains("return _serializer.Deserialize<JoinRoomReply>(result.Payload);", result.GeneratedSource);
    }

    [Fact]
    public void Generator_uses_ToString_for_key_without_Value_property()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public sealed class PingRequest
            {
            }

            public sealed class PingReply
            {
            }

            public sealed class SessionActor : Actor<Guid>
            {
                public ValueTask<PingReply> PingAsync(PingRequest request, CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(new PingReply());
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"session/\" + _id.ToString())", result.GeneratedSource);
    }

    [Fact]
    public void Generator_uses_string_key_directly()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public sealed class PingRequest
            {
            }

            public sealed class PingReply
            {
            }

            public sealed class SessionActor : Actor<string>
            {
                public ValueTask<PingReply> PingAsync(PingRequest request, CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(new PingReply());
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"session/\" + _id)", result.GeneratedSource);
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

    [Fact]
    public void Generator_uses_explicit_actor_and_method_names()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct RoomId(string Value);
            public sealed record JoinRoomRequest(string PlayerId);
            public sealed record JoinRoomReply(bool Accepted);

            [ActorName("battle-room")]
            public sealed class BattleRoomActor : Actor<RoomId>
            {
                [ActorMethod("join")]
                public ValueTask<JoinRoomReply> EnterAsync(
                    JoinRoomRequest request,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(new JoinRoomReply(true));
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("global::ULinkGame.Server.Actors.ActorId.From(\"battle-room/\" + _id.Value)", result.GeneratedSource);
        Assert.Contains("\"battle-room\", \"join\"", result.GeneratedSource);
    }

    [Fact]
    public void Generator_skips_actor_ignore_methods()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct RoomId(string Value);
            public sealed record PingRequest;

            public sealed class RoomActor : Actor<RoomId>
            {
                [ActorIgnore]
                public ValueTask HiddenAsync(PingRequest request, CancellationToken cancellationToken = default)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.DoesNotContain("HiddenAsync", result.GeneratedSource);
    }

    [Fact]
    public void Generator_skips_remote_ref_for_local_only_actor()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct MetricsId(string Value);
            public sealed record PingRequest;

            [ActorLocalOnly]
            public sealed class MetricsActor : Actor<MetricsId>
            {
                public ValueTask PingAsync(PingRequest request, CancellationToken cancellationToken = default)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Empty(result.ErrorDiagnostics);
        Assert.Contains("public MetricsLocalRef Local(MetricsId id)", result.GeneratedSource);
        Assert.DoesNotContain("MetricsRemoteRef", result.GeneratedSource);
        Assert.DoesNotContain("Remote(global::ULinkGame.Cluster.NodeId nodeId", result.GeneratedSource);
    }

    [Fact]
    public void Generator_reports_warning_for_unsupported_public_method()
    {
        var source = """
            using ULinkGame.Server.Actors;

            namespace Game.Server;

            public readonly record struct RoomId(string Value);

            public sealed class RoomActor : Actor<RoomId>
            {
                public int Count()
                {
                    return 1;
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ULINKACTOR001");
    }
}
