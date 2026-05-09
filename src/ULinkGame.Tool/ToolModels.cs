internal static class ProjectConventions
{
    public const string ConfigFileName = "ulinkgame.tool.json";
    public const string DefaultProjectName = "MyGame";
    public const string DefaultClientEngine = "unity";
    public const string DefaultTransport = "kcp";
    public const string DefaultSerializer = "memorypack";
    public const string DefaultNuGetForUnitySource = "embedded";
    public const string StarterServerProjectPath = "Server/Server";
    public const string EdgeProjectPath = "Server/Edge";
    public const string StarterServerGeneratedNamespace = "Server.Generated";
    public const string EdgeGeneratedNamespace = "Edge.Generated";

    public static readonly string[] SupportedClientEngines = ["unity", "unity-cn", "tuanjie", "godot"];
    public static readonly string[] SupportedTransports = ["tcp", "websocket", "kcp"];
    public static readonly string[] SupportedSerializers = ["json", "memorypack"];
    public static readonly string[] SupportedNuGetForUnitySources = ["embedded", "openupm"];

    public static bool IsGodot(string clientEngine)
    {
        return string.Equals(clientEngine, "godot", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class ToolPackageVersions
{
    public const string ULinkGameClient = "0.1.3";
    public const string ULinkGameServer = "0.1.3";
    public const string ULinkRpcStarter = "0.2.58";
    public const string Orleans = "10.0.0";
}

internal sealed class ToolConfig
{
    public ProjectConfig Project { get; set; } = new();
    public CodegenConfig Codegen { get; set; } = new();

    public static ToolConfig CreateDefault(string projectName, NewCommandOptions options)
    {
        return new ToolConfig
        {
            Project = new ProjectConfig
            {
                Name = projectName,
                ClientEngine = options.ClientEngine,
                Transport = options.Transport,
                Serializer = options.Serializer,
                NuGetForUnitySource = options.NuGetForUnitySource
            },
            Codegen = new CodegenConfig
            {
                ContractsPath = "Shared",
                Server = new CodegenTargetConfig
                {
                    ProjectPath = ProjectConventions.EdgeProjectPath,
                    OutputPath = "Generated",
                    Namespace = ProjectConventions.EdgeGeneratedNamespace
                },
                UnityClient = new CodegenTargetConfig
                {
                    ProjectPath = "Client",
                    OutputPath = ProjectConventions.IsGodot(options.ClientEngine) ? "Scripts/Rpc/Generated" : "Assets/Scripts/Rpc/Generated",
                    Namespace = "Rpc.Generated"
                }
            }
        };
    }
}

internal sealed class CodegenConfig
{
    public string ContractsPath { get; set; } = "Shared";
    public CodegenTargetConfig? Server { get; set; }
    public CodegenTargetConfig? UnityClient { get; set; }
}

internal sealed class ProjectConfig
{
    public string Name { get; set; } = ProjectConventions.DefaultProjectName;
    public string ClientEngine { get; set; } = ProjectConventions.DefaultClientEngine;
    public string Transport { get; set; } = ProjectConventions.DefaultTransport;
    public string Serializer { get; set; } = ProjectConventions.DefaultSerializer;
    public string NuGetForUnitySource { get; set; } = ProjectConventions.DefaultNuGetForUnitySource;
}

internal sealed class CodegenTargetConfig
{
    public string ProjectPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Namespace { get; set; } = "";
}

internal readonly record struct NewCommandOptions(
    string? Name,
    string? OutputPath,
    string ClientEngine,
    string Transport,
    string Serializer,
    string NuGetForUnitySource);

internal readonly record struct RegenerateCodeOptions(string? ConfigPath, bool NoRestore);

internal readonly record struct ProcessInvocation(string FileName, IReadOnlyList<string> Arguments, bool CanFallback);

internal readonly record struct PackageArtifact(string PackageId, string Version, string Namespace);

internal sealed class CliUsageException(string message) : Exception(message);
