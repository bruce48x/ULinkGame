using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ULinkGame.Cluster;
using ULinkGame.Cluster.ULinkRPC;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Transport.Tcp;

var options = SampleOptions.Parse(args);
return options.Mode switch
{
    "directory" => await RunDirectoryAsync(options),
    "worker" => await RunWorkerAsync(options),
    "driver" => await RunDriverAsync(),
    _ => Usage()
};

static async Task<int> RunDirectoryAsync(SampleOptions options)
{
    if (options.Port is null)
    {
        return Usage();
    }

    var directory = new InMemoryRouteDirectory();
    var serializer = new JsonSampleSerializer();
    var builder = RpcServerHostBuilder.Create()
        .UseSerializer(serializer)
        .UseAcceptor(new TcpConnectionAcceptor(options.Port.Value));
    ULinkRpcRouteDirectoryBinder.Bind(builder.ServiceRegistry, directory);

    Console.WriteLine($"directory-ready tcp://127.0.0.1:{options.Port.Value}");
    await builder.RunAsync(CancellationToken.None);
    return 0;
}

static async Task<int> RunWorkerAsync(SampleOptions options)
{
    if (options.Port is null ||
        options.DirectoryEndpoint is null ||
        options.NodeEpoch is null)
    {
        return Usage();
    }

    var serializer = new JsonSampleSerializer();
    var handler = new WorkerHandler();
    var builder = RpcServerHostBuilder.Create()
        .UseSerializer(serializer)
        .UseAcceptor(new TcpConnectionAcceptor(options.Port.Value));
    ULinkRpcClusterMessageBinder.Bind(builder.ServiceRegistry, handler);

    var serverTask = builder.RunAsync(CancellationToken.None).AsTask();
    await Task.Delay(100);

    await using var clientFactory = new ULinkRpcClusterClientFactory(
        new TcpULinkRpcClusterTransportFactory(),
        serializer);
    var directoryClient = await clientFactory.GetClientAsync(
        DirectoryLocation(options.DirectoryEndpoint),
        CancellationToken.None);
    var directory = new ULinkRpcRouteDirectory(directoryClient);
    var route = new RouteLocation(
        WorkerRoute(),
        "worker",
        new NodeEndpoint($"tcp://127.0.0.1:{options.Port.Value}"),
        DateTimeOffset.UtcNow.AddMinutes(5),
        nodeEpoch: options.NodeEpoch.Value,
        generation: options.NodeEpoch.Value);
    var status = await directory.RegisterAsync(route);
    if (status != RouteRegistrationStatus.Registered)
    {
        Console.Error.WriteLine($"worker-register={status}");
        return 2;
    }

    Console.WriteLine($"worker-ready epoch={options.NodeEpoch.Value} tcp://127.0.0.1:{options.Port.Value}");
    await serverTask;
    return 0;
}

static async Task<int> RunDriverAsync()
{
    var directoryPort = GetFreePort();
    var workerPort = GetFreePort();
    var restartedWorkerPort = GetFreePort();
    var directoryEndpoint = $"tcp://127.0.0.1:{directoryPort}";
    var serializer = new JsonSampleSerializer();
    using var directoryProcess = StartChild("--mode", "directory", "--port", directoryPort.ToString());
    try
    {
        await WaitForLineAsync(directoryProcess, "directory-ready", TimeSpan.FromSeconds(10));

        using var worker = StartChild(
            "--mode", "worker",
            "--port", workerPort.ToString(),
            "--directory", directoryEndpoint,
            "--epoch", "1");
        await WaitForLineAsync(worker, "worker-ready", TimeSpan.FromSeconds(10));

        await using var clientFactory = new ULinkRpcClusterClientFactory(
            new TcpULinkRpcClusterTransportFactory(),
            serializer);
        var directoryClient = await clientFactory.GetClientAsync(
            DirectoryLocation(directoryEndpoint),
            CancellationToken.None);
        var directory = new ULinkRpcRouteDirectory(directoryClient);
        var now = DateTimeOffset.UtcNow;
        var localRoute = new RouteLocation(
            "control/local",
            "driver",
            new NodeEndpoint("in-memory://driver"),
            now.AddMinutes(5),
            nodeEpoch: 1,
            generation: 1);
        var localRegister = await directory.RegisterAsync(localRoute);
        var staleRegister = await directory.RegisterAsync(
            new RouteLocation(
                WorkerRoute(),
                "worker",
                new NodeEndpoint($"tcp://127.0.0.1:{workerPort}"),
                now.AddMinutes(5),
                nodeEpoch: 1,
                generation: 0));

        var router = new ClusterRouter(
            "driver",
            directory,
            new DriverHandler(),
            new ULinkRpcClusterNodeMessenger(
                clientFactory,
                new ULinkRpcClusterNodeMessengerOptions
                {
                    SendTimeout = TimeSpan.FromSeconds(2)
                }),
            () => DateTimeOffset.UtcNow);

        var local = await router.SendAsync(NewMessage("control/local", "local-ping", now.AddMinutes(1)));
        var remote = await router.SendAsync(NewActorMessage("remote-ping", now.AddMinutes(1)));
        var missing = await router.SendAsync(NewMessage("missing/route", "missing", now.AddMinutes(1)));
        var expired = await router.SendAsync(NewMessage("control/local", "expired", now.AddSeconds(-1)));
        var timeout = await router.SendAsync(NewActorMessage("timeout", now.AddMinutes(1)));
        var backpressure = await router.SendAsync(NewActorMessage("busy", now.AddMinutes(1)));
        var handlerUnavailable = await router.SendAsync(NewActorMessage("unavailable", now.AddMinutes(1)));

        StopChild(worker);
        var clearedOldEpoch = await directory.ClearByNodeEpochAsync("worker", 1);
        var oldRoute = await directory.ResolveAsync(WorkerRoute(), DateTimeOffset.UtcNow);

        using var restartedWorker = StartChild(
            "--mode", "worker",
            "--port", restartedWorkerPort.ToString(),
            "--directory", directoryEndpoint,
            "--epoch", "2");
        await WaitForLineAsync(restartedWorker, "worker-ready", TimeSpan.FromSeconds(10));
        var afterRestart = await router.SendAsync(NewActorMessage("after-restart", DateTimeOffset.UtcNow.AddMinutes(1)));

        StopChild(restartedWorker);

        Console.WriteLine($"localRegister={localRegister}");
        Console.WriteLine($"staleRegister={staleRegister}");
        Console.WriteLine($"local={local}");
        Console.WriteLine($"remote={remote}");
        Console.WriteLine($"missing={missing}");
        Console.WriteLine($"expired={expired}");
        Console.WriteLine($"timeout={timeout}");
        Console.WriteLine($"backpressure={backpressure}");
        Console.WriteLine($"handlerUnavailable={handlerUnavailable}");
        Console.WriteLine($"clearedOldEpoch={clearedOldEpoch}");
        Console.WriteLine($"oldRouteAfterClear={(oldRoute is null ? "null" : "present")}");
        Console.WriteLine($"afterRestart={afterRestart}");

        return localRegister == RouteRegistrationStatus.Registered &&
            staleRegister == RouteRegistrationStatus.StaleLocation &&
            local == ClusterSendStatus.Accepted &&
            remote == ClusterSendStatus.Accepted &&
            missing == ClusterSendStatus.RouteNotFound &&
            expired == ClusterSendStatus.Expired &&
            timeout == ClusterSendStatus.Timeout &&
            backpressure == ClusterSendStatus.Backpressure &&
            handlerUnavailable == ClusterSendStatus.HandlerUnavailable &&
            clearedOldEpoch == 1 &&
            oldRoute is null &&
            afterRestart == ClusterSendStatus.Accepted
                ? 0
                : 1;
    }
    finally
    {
        StopChild(directoryProcess);
    }
}

static int Usage()
{
    Console.Error.WriteLine("Usage: --mode driver | --mode directory --port <port> | --mode worker --port <port> --directory <endpoint> --epoch <epoch>");
    return 64;
}

static Process StartChild(params string[] arguments)
{
    var dll = Assembly.GetEntryAssembly()?.Location ??
        throw new InvalidOperationException("Cannot locate sample assembly.");
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(dll);
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start child process.");
    return process;
}

static async Task WaitForLineAsync(Process process, string expectedPrefix, TimeSpan timeout)
{
    using var timeoutCts = new CancellationTokenSource(timeout);
    while (!timeoutCts.IsCancellationRequested)
    {
        var lineTask = process.StandardOutput.ReadLineAsync(timeoutCts.Token).AsTask();
        var line = await lineTask;
        if (line is null)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Process exited before '{expectedPrefix}'. stderr: {error}");
        }

        if (line.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return;
        }
    }

    throw new TimeoutException($"Timed out waiting for '{expectedPrefix}'.");
}

static void StopChild(Process process)
{
    if (process.HasExited)
    {
        return;
    }

    process.Kill(entireProcessTree: true);
    process.WaitForExit(5000);
}

static int GetFreePort()
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

static RouteLocation DirectoryLocation(string endpoint)
{
    return new RouteLocation(
        "directory",
        "directory",
        new NodeEndpoint(endpoint),
        DateTimeOffset.UtcNow.AddMinutes(5),
        nodeEpoch: 1,
        generation: 1);
}

static RouteKey WorkerRoute()
{
    return ClusterActorRouteKeys.ForActor("worker/demo");
}

static ClusterMessage NewMessage(
    RouteKey route,
    string kind,
    DateTimeOffset expiresAt)
{
    return new ClusterMessage(
        route,
        kind,
        ReadOnlyMemory<byte>.Empty,
        expiresAt,
        "driver",
        correlationId: $"sample-{kind}",
        traceId: "sample-trace",
        orderedBy: route.Value);
}

static ClusterMessage NewActorMessage(string kind, DateTimeOffset expiresAt)
{
    return new ClusterActorEnvelope(
        WorkerRoute(),
        "worker/demo",
        kind,
        Encoding.UTF8.GetBytes(kind),
        expiresAt,
        "driver",
        correlationId: $"sample-{kind}",
        traceId: "sample-trace",
        orderedBy: "worker/demo").ToClusterMessage();
}

sealed class DriverHandler : IClusterMessageHandler
{
    public ValueTask<ClusterSendStatus> HandleAsync(
        ClusterMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ClusterSendStatus.Accepted);
    }
}

sealed class WorkerHandler : IClusterMessageHandler
{
    public ValueTask<ClusterSendStatus> HandleAsync(
        ClusterMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = message.Kind switch
        {
            "busy" => ClusterSendStatus.Backpressure,
            "timeout" => ClusterSendStatus.Timeout,
            "unavailable" => ClusterSendStatus.HandlerUnavailable,
            _ => ClusterSendStatus.Accepted
        };

        return ValueTask.FromResult(status);
    }
}

sealed class JsonSampleSerializer : IRpcSerializer
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

sealed record SampleOptions
{
    public string Mode { get; init; } = "driver";

    public int? Port { get; init; }

    public string? DirectoryEndpoint { get; init; }

    public long? NodeEpoch { get; init; }

    public static SampleOptions Parse(string[] args)
    {
        var options = new SampleOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (key)
            {
                case "--mode" when value is not null:
                    options = options with { Mode = value };
                    i++;
                    break;
                case "--port" when value is not null && int.TryParse(value, out var port):
                    options = options with { Port = port };
                    i++;
                    break;
                case "--directory" when value is not null:
                    options = options with { DirectoryEndpoint = value };
                    i++;
                    break;
                case "--epoch" when value is not null && long.TryParse(value, out var epoch):
                    options = options with { NodeEpoch = epoch };
                    i++;
                    break;
            }
        }

        return options;
    }
}
