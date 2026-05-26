using System.Text;
using ULinkGame.Cluster;

var now = DateTimeOffset.UtcNow;
var routes = new InMemoryRouteDirectory();
var messenger = new InMemoryLoopbackNodeMessenger();
var localHandler = new DemoHandler("local");
var remoteHandler = new DemoHandler("remote");

messenger.RegisterNode("node-b", remoteHandler);

await routes.RegisterAsync(
    new RouteLocation(
        "control/local",
        "node-a",
        new NodeEndpoint("in-memory://node-a"),
        now.AddMinutes(1),
        nodeEpoch: 1,
        generation: 1));

await routes.RegisterAsync(
    new RouteLocation(
        ClusterActorRouteKeys.ForActor("worker/demo"),
        "node-b",
        new NodeEndpoint("in-memory://node-b"),
        now.AddMinutes(1),
        nodeEpoch: 1,
        generation: 1));

var router = new ClusterRouter(
    "node-a",
    routes,
    localHandler,
    messenger,
    () => now);

var localStatus = await router.SendAsync(NewMessage("control/local", "local-ping", now.AddMinutes(1), "node-a"));
var remoteStatus = await router.SendAsync(
    new ClusterActorEnvelope(
        ClusterActorRouteKeys.ForActor("worker/demo"),
        "worker/demo",
        "remote-ping",
        Encoding.UTF8.GetBytes("hello"),
        now.AddMinutes(1),
        "node-a",
        correlationId: "sample-correlation",
        traceId: "sample-trace",
        orderedBy: "worker/demo").ToClusterMessage());
var missingStatus = await router.SendAsync(NewMessage("missing/route", "missing", now.AddMinutes(1), "node-a"));
var expiredStatus = await router.SendAsync(NewMessage("control/local", "expired", now.AddSeconds(-1), "node-a"));
var timeoutStatus = await router.SendAsync(
    new ClusterActorEnvelope(
        ClusterActorRouteKeys.ForActor("worker/demo"),
        "worker/demo",
        "timeout",
        ReadOnlyMemory<byte>.Empty,
        now.AddMinutes(1),
        "node-a").ToClusterMessage());
var backpressureStatus = await router.SendAsync(
    new ClusterActorEnvelope(
        ClusterActorRouteKeys.ForActor("worker/demo"),
        "worker/demo",
        "busy",
        ReadOnlyMemory<byte>.Empty,
        now.AddMinutes(1),
        "node-a").ToClusterMessage());

Console.WriteLine($"local={localStatus}");
Console.WriteLine($"remote={remoteStatus}");
Console.WriteLine($"missing={missingStatus}");
Console.WriteLine($"expired={expiredStatus}");
Console.WriteLine($"timeout={timeoutStatus}");
Console.WriteLine($"backpressure={backpressureStatus}");
Console.WriteLine($"localHandled={string.Join(",", localHandler.HandledKinds)}");
Console.WriteLine($"remoteHandled={string.Join(",", remoteHandler.HandledKinds)}");

return localStatus == ClusterSendStatus.Accepted &&
    remoteStatus == ClusterSendStatus.Accepted &&
    missingStatus == ClusterSendStatus.RouteNotFound &&
    expiredStatus == ClusterSendStatus.Expired &&
    timeoutStatus == ClusterSendStatus.Timeout &&
    backpressureStatus == ClusterSendStatus.Backpressure
        ? 0
        : 1;

static ClusterMessage NewMessage(
    RouteKey route,
    string kind,
    DateTimeOffset expiresAt,
    NodeId sourceNode)
{
    return new ClusterMessage(
        route,
        kind,
        ReadOnlyMemory<byte>.Empty,
        expiresAt,
        sourceNode);
}

sealed class DemoHandler : IClusterMessageHandler
{
    private readonly string _name;

    public DemoHandler(string name)
    {
        _name = name;
    }

    public List<string> HandledKinds { get; } = new();

    public ValueTask<ClusterSendStatus> HandleAsync(
        ClusterMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HandledKinds.Add(_name + ":" + message.Kind);

        var status = message.Kind switch
        {
            "busy" => ClusterSendStatus.Backpressure,
            "timeout" => ClusterSendStatus.Timeout,
            _ => ClusterSendStatus.Accepted
        };

        return ValueTask.FromResult(status);
    }
}
