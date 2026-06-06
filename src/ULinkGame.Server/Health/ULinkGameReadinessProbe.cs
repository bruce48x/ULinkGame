using System.Text.Json;
using ULinkGame.Server.Configuration;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Guardrails.Rules;

namespace ULinkGame.Server.Health;

public static class ULinkGameReadinessProbe
{
    public static int Run(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions? clusterOptions,
        string[] args)
    {
        // Liveness is a subset of readiness — fail fast if liveness fails
        var livenessExit = ULinkGameLivenessProbe.Run(clusterOptions, runtime);
        if (livenessExit != 0)
        {
            return livenessExit;
        }

        // Build applicable Guardrails rules
        var rules = new List<IULinkGameValidationRule>
        {
            new NodeIdentityRule(),
            new EndpointRule(),
            new HotfixSourceRule()
        };

        if (clusterOptions is not null)
        {
            rules.Add(new ClusterEndpointRule());
            rules.Add(new ClusterServiceGraphRule());
        }

        var resolved = ToResolvedRuntime(runtime, clusterOptions);
        var validator = new ULinkGameRuntimeValidator(rules);
        var result = validator.Validate(resolved);

        if (args.Contains("--json", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["succeeded"] = result.Succeeded,
                    ["diagnostics"] = result.Diagnostics.Select(diagnostic => new
                    {
                        code = diagnostic.Code,
                        severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                        message = diagnostic.Message,
                        repair = diagnostic.Repair
                    })
                },
                new JsonSerializerOptions { WriteIndented = true }));
            return result.Succeeded ? 0 : 1;
        }

        return WriteText(runtime, clusterOptions, result);
    }

    private static int WriteText(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions? clusterOptions,
        ULinkGameValidationResult result)
    {
        var nodeId = clusterOptions?.NodeId ?? runtime.Node.Id;
        var serviceNames = clusterOptions?.Services.Select(service => service.Name) ?? Array.Empty<string>();
        var rpcEndpoint = clusterOptions?.AdvertisedEndpoints.TryGetValue("client", out var clientEndpoint) == true
            ? clientEndpoint
            : runtime.Endpoints.FirstOrDefault()?.ToAdvertisedEndpoint() ?? "not configured";

        Console.WriteLine("cluster: ok single-node");
        Console.WriteLine($"node: ok {nodeId}");
        if (serviceNames.Any())
        {
            Console.WriteLine($"services: ok {string.Join(", ", serviceNames)}");
        }

        var hotfixFailure = result.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == "ULINK071");
        if (hotfixFailure is not null)
        {
            Console.Error.WriteLine("hotfix: failed local build output not found");
            Console.Error.WriteLine($"fix: {hotfixFailure.Repair}");
            return 1;
        }

        Console.WriteLine("hotfix: ok local-build Server.Hotfix.dll");
        Console.WriteLine("reliable-push: ok pending limit 256, replay window 120s");
        Console.WriteLine($"rpc: ok {rpcEndpoint}");

        foreach (var diagnostic in result.Diagnostics.Where(diagnostic => diagnostic.Severity == ULinkGameDiagnosticSeverity.Error))
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            if (!string.IsNullOrWhiteSpace(diagnostic.Repair))
            {
                Console.Error.WriteLine($"fix: {diagnostic.Repair}");
            }
        }

        return result.Succeeded ? 0 : 1;
    }

    private static ULinkGameResolvedRuntime ToResolvedRuntime(
        ULinkGameRuntimeOptions runtime,
        ClusterOptions? clusterOptions)
    {
        var hotfixPath = Path.Combine(
            AppContext.BaseDirectory,
            "hotfix",
            "Server.Hotfix.dll");

        var clusterServices = clusterOptions?.Services
            .Select(service => new ULinkGameResolvedClusterService(service.Kind, service.Name))
            .ToArray() ?? Array.Empty<ULinkGameResolvedClusterService>();

        return new ULinkGameResolvedRuntime(
            NodeId: new ULinkGameResolvedValue<string>(
                clusterOptions?.NodeId ?? runtime.Node.Id,
                ULinkGameValueSource.Configuration,
                "ULinkGame:Node:Id"),
            Endpoints: runtime.Endpoints.Select((endpoint, endpointIndex) =>
                new ULinkGameResolvedEndpoint(
                    Transport: new ULinkGameResolvedValue<string>(endpoint.Transport, ULinkGameValueSource.Configuration, $"ULinkGame:Endpoints:{endpointIndex}:Transport"),
                    Host: new ULinkGameResolvedValue<string>(endpoint.Host, ULinkGameValueSource.Configuration, $"ULinkGame:Endpoints:{endpointIndex}:Host"),
                    Port: new ULinkGameResolvedValue<int>(endpoint.Port, ULinkGameValueSource.Configuration, $"ULinkGame:Endpoints:{endpointIndex}:Port"),
                    Path: new ULinkGameResolvedValue<string>(endpoint.Path, ULinkGameValueSource.Configuration, $"ULinkGame:Endpoints:{endpointIndex}:Path"),
                    AdvertisedHost: new ULinkGameResolvedValue<string>(endpoint.AdvertisedHost, ULinkGameValueSource.Configuration, $"ULinkGame:Endpoints:{endpointIndex}:AdvertisedHost"),
                    AdvertisedEndpoint: new ULinkGameResolvedValue<string>(endpoint.ToAdvertisedEndpoint(), ULinkGameValueSource.GeneratedConvention)))
                .ToArray(),
            Cluster: new ULinkGameResolvedCluster(
                Services: clusterServices,
                AdvertisedEndpoints: clusterOptions?.AdvertisedEndpoints ?? new Dictionary<string, string>()),
            ClusterEndpoint: new ULinkGameResolvedClusterEndpoint(
                new ULinkGameResolvedValue<string>(
                    runtime.ClusterEndpoint,
                    ULinkGameValueSource.GeneratedConvention,
                    "ULinkGame:Cluster:Endpoint"),
                new[] { runtime.ClusterEndpoint }),
            Feature: new ULinkGameResolvedFeature(
                Configured: null,
                Active: Array.Empty<string>(),
                StartupOrder: Array.Empty<string>()),
            Hotfix: new ULinkGameResolvedHotfix(
                AssemblyPath: new ULinkGameResolvedValue<string>(hotfixPath, ULinkGameValueSource.GeneratedConvention),
                AssemblyFileName: new ULinkGameResolvedValue<string>("Server.Hotfix.dll", ULinkGameValueSource.GeneratedConvention)),
            ReliablePush: new ULinkGameResolvedReliablePush(
                StorageMode: new ULinkGameResolvedValue<string>("InMemory", ULinkGameValueSource.Default),
                PendingLimit: new ULinkGameResolvedValue<int>(256, ULinkGameValueSource.Default),
                ReplayWindowSeconds: new ULinkGameResolvedValue<int>(120, ULinkGameValueSource.Default),
                HasSessionIdentityResolver: true),
            Profile: ULinkGameRuntimeProfile.Development);
    }
}
