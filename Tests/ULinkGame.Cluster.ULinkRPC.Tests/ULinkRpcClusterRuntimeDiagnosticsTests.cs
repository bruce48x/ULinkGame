using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ULinkGame.Cluster;
using ULinkGame.Cluster.ULinkRPC;
using ULinkRPC.Client;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Transport.Tcp;
using Xunit;

namespace ULinkGame.Cluster.ULinkRPC.Tests;

public sealed class ULinkRpcClusterRuntimeTests
{
    [Fact]
    public async Task RpcClientRuntimeCanCallClusterBinderOverTcp()
    {
        var port = GetFreePort();
        var serializer = new JsonTestSerializer();
        var handler = new RecordingHandler(ClusterSendStatus.Accepted);
        using var stopServer = new CancellationTokenSource();
        var builder = RpcServerHostBuilder.Create()
            .UseSerializer(serializer)
            .UseAcceptor(new TcpConnectionAcceptor(port));
        ULinkRpcClusterMessageBinder.Bind(builder.ServiceRegistry, handler);

        var serverTask = builder.RunAsync(stopServer.Token).AsTask();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await using var runtime = new RpcClientRuntime(
            new RpcClientOptions(new TcpTransport("127.0.0.1", port), serializer));
        var runtimeTask = runtime.StartAsync(CancellationToken.None).AsTask();
        _ = runtimeTask.ContinueWith(
            task => _ = task.Exception,
            TaskContinuationOptions.OnlyOnFaulted);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var reply = await runtime.CallAsync(
            ULinkRpcClusterProtocol.SendMethod,
            new ULinkRpcClusterSendRequest
            {
                Route = "room/1",
                Kind = "command",
                Payload = new byte[] { 1, 2, 3 },
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
                SourceNode = "node-a"
            },
            timeout.Token);

        stopServer.Cancel();
        await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        Assert.Equal(ClusterSendStatus.Accepted, (ClusterSendStatus)reply.Status);
        Assert.Single(handler.Messages);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class RecordingHandler : IClusterMessageHandler
    {
        private readonly ClusterSendStatus _status;

        public RecordingHandler(ClusterSendStatus status)
        {
            _status = status;
        }

        public List<ClusterMessage> Messages { get; } = new();

        public ValueTask<ClusterSendStatus> HandleAsync(
            ClusterMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return ValueTask.FromResult(_status);
        }
    }

    private sealed class JsonTestSerializer : IRpcSerializer
    {
        public TransportFrame SerializeFrame<T>(T value)
        {
            return TransportFrame.CopyOf(JsonSerializer.SerializeToUtf8Bytes(value));
        }

        public T Deserialize<T>(ReadOnlySpan<byte> payload)
        {
            return JsonSerializer.Deserialize<T>(payload)!;
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> payload)
        {
            return Deserialize<T>(payload.Span);
        }
    }
}
