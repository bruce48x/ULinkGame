using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;

var exitCode = await ULinkGameToolCli.RunAsync(args).ConfigureAwait(false);
Environment.ExitCode = exitCode;

internal static class ULinkGameToolCli
{
    private static readonly string[] NewOptions =
    [
        "--name",
        "--output",
        "--client-engine",
        "--transport",
        "--serializer",
        "--nugetforunity-source"
    ];

    private static readonly string[] CodegenOptions =
    [
        "--config",
        "--no-restore"
    ];

    private static readonly string[] SupportedClientEngines = ["unity", "unity-cn", "tuanjie", "godot"];
    private static readonly string[] SupportedTransports = ["tcp", "websocket", "kcp"];
    private static readonly string[] SupportedSerializers = ["json", "memorypack"];
    private static readonly string[] SupportedNuGetForUnitySources = ["embedded", "openupm"];
    private const string ULinkGameClientVersion = "0.1.1";
    private const string ULinkGameServerVersion = "0.1.1";
    private const string ULinkRpcStarterVersion = "0.2.52";
    private const string OrleansVersion = "10.0.0";
    private const string NpgsqlVersion = "9.0.4";

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            return args[0] switch
            {
                "help" or "--help" or "-h" => HelpResult(),
                "new" or "init" => await NewAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                "codegen" => await RegenerateCodeAsync(args.Skip(1).ToArray()).ConfigureAwait(false),
                _ => UnknownCommand(args[0])
            };
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Run `ulinkgame-tool help` for usage.");
            return 1;
        }
    }

    private static int HelpResult()
    {
        PrintHelp();
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine();
        PrintHelp();
        return 1;
    }

    private static async Task<int> NewAsync(string[] args)
    {
        var options = ParseNewOptions(args);
        var outputDirectory = Path.GetFullPath(options.OutputPath ?? Directory.GetCurrentDirectory());
        Directory.CreateDirectory(outputDirectory);

        var projectName = string.IsNullOrWhiteSpace(options.Name) ? "MyGame" : options.Name;
        var projectRoot = Path.Combine(outputDirectory, projectName);

        var starterExitCode = await RunULinkRpcStarterNewAsync(projectName, outputDirectory, options).ConfigureAwait(false);
        if (starterExitCode != 0)
        {
            return starterExitCode;
        }

        if (!Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"Generated project root not found: {projectRoot}");
            return 1;
        }

        if (IsGodot(options.ClientEngine))
        {
            await ReplaceGeneratedClientWithGodotClientAsync(projectRoot, projectName, options).ConfigureAwait(false);
        }

        await AugmentProjectWithULinkGameServerAsync(projectRoot, options).ConfigureAwait(false);

        var configPath = Path.Combine(projectRoot, "ulinkgame.tool.json");
        if (File.Exists(configPath))
        {
            Console.Error.WriteLine($"Config already exists: {configPath}");
            return 1;
        }

        var config = ToolConfig.CreateDefault(projectName, options);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json).ConfigureAwait(false);
        Console.WriteLine($"Created tool config: {configPath}");
        return 0;
    }

    private static async Task<int> RegenerateCodeAsync(string[] args)
    {
        var options = ParseRegenerateCodeOptions(args);
        var configPath = options.ConfigPath ?? Path.Combine(Directory.GetCurrentDirectory(), "ulinkgame.tool.json");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Missing tool config: {configPath}");
            Console.Error.WriteLine("Run `ulinkgame-tool new` first or pass --config <path>.");
            return 1;
        }

        var config = await LoadConfigAsync(configPath).ConfigureAwait(false);
        var rootPath = Path.GetDirectoryName(Path.GetFullPath(configPath))
            ?? Directory.GetCurrentDirectory();

        var starterExitCode = await RunULinkRpcCodegenAsync(rootPath, config, options.NoRestore).ConfigureAwait(false);
        if (starterExitCode != 0)
        {
            return starterExitCode;
        }

        Console.WriteLine("Code generation completed.");
        return 0;
    }

    private static RegenerateCodeOptions ParseRegenerateCodeOptions(string[] args)
    {
        string? configPath = null;
        var noRestore = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--no-restore":
                    noRestore = true;
                    break;
                case "--config":
                    configPath = Path.GetFullPath(ReadOptionValue(args, ref index, "--config"));
                    break;
                default:
                    throw CreateUnsupportedArgumentException(args[index], CodegenOptions);
            }
        }

        return new RegenerateCodeOptions(configPath, noRestore);
    }

    private static NewCommandOptions ParseNewOptions(string[] args)
    {
        string? name = null;
        string? outputPath = null;
        var clientEngine = "unity";
        var transport = "kcp";
        var serializer = "memorypack";
        var nuGetForUnitySource = "embedded";

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--name":
                    name = ReadOptionValue(args, ref index, "--name");
                    break;
                case "--output":
                    outputPath = ReadOptionValue(args, ref index, "--output");
                    break;
                case "--client-engine":
                    clientEngine = ValidateChoice("--client-engine", ReadOptionValue(args, ref index, "--client-engine"), SupportedClientEngines);
                    break;
                case "--transport":
                    transport = ValidateChoice("--transport", ReadOptionValue(args, ref index, "--transport"), SupportedTransports);
                    break;
                case "--serializer":
                    serializer = ValidateChoice("--serializer", ReadOptionValue(args, ref index, "--serializer"), SupportedSerializers);
                    break;
                case "--nugetforunity-source":
                    nuGetForUnitySource = ValidateChoice("--nugetforunity-source", ReadOptionValue(args, ref index, "--nugetforunity-source"), SupportedNuGetForUnitySources);
                    break;
                default:
                    throw CreateUnsupportedArgumentException(args[index], NewOptions);
            }
        }

        return new NewCommandOptions(name, outputPath, clientEngine, transport, serializer, nuGetForUnitySource);
    }

    private static string ReadOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new CliUsageException($"Missing value for {optionName}.");
        }

        return args[++index];
    }

    private static string ValidateChoice(string optionName, string value, IReadOnlyCollection<string> supportedValues)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (supportedValues.Contains(normalized))
        {
            return normalized;
        }

        var suggestion = GetValueSuggestion(optionName, normalized, supportedValues);
        var message = $"Unsupported value '{value}' for {optionName}. Expected one of: {string.Join("|", supportedValues)}.";
        if (suggestion is not null)
        {
            message += $" Did you mean '{suggestion}'?";
        }

        throw new CliUsageException(message);
    }

    private static CliUsageException CreateUnsupportedArgumentException(string argument, IReadOnlyList<string> supportedOptions)
    {
        if (!argument.StartsWith("--", StringComparison.Ordinal))
        {
            return new CliUsageException($"Unexpected argument: {argument}.");
        }

        var suggestion = GetClosestMatch(argument, supportedOptions);
        var message = $"Unsupported option: {argument}.";
        if (suggestion is not null)
        {
            message += $" Did you mean {suggestion}?";
        }

        return new CliUsageException(message);
    }

    private static string? GetValueSuggestion(string optionName, string value, IReadOnlyCollection<string> supportedValues)
    {
        if (optionName == "--transport" && value is "ws" or "websocket transport")
        {
            return "websocket";
        }

        return GetClosestMatch(value, supportedValues);
    }

    private static string? GetClosestMatch(string value, IReadOnlyCollection<string> candidates)
    {
        var best = candidates
            .Select(candidate => new { Value = candidate, Distance = GetEditDistance(value, candidate) })
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();

        return best is not null && best.Distance <= 2 ? best.Value : null;
    }

    private static int GetEditDistance(string left, string right)
    {
        var distances = new int[left.Length + 1, right.Length + 1];

        for (var i = 0; i <= left.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            distances[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[left.Length, right.Length];
    }

    private static async Task<int> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    private static async Task<int> RunULinkRpcStarterNewAsync(string projectName, string outputDirectory, NewCommandOptions options)
    {
        var arguments = new[]
        {
            "--name", projectName,
            "--output", outputDirectory,
            "--client-engine", options.ClientEngine,
            "--transport", options.Transport,
            "--serializer", options.Serializer,
            "--nugetforunity-source", options.NuGetForUnitySource
        };

        foreach (var invocation in EnumerateULinkRpcStarterInvocations(arguments))
        {
            try
            {
                return await RunProcessAsync(invocation.FileName, invocation.Arguments, Directory.GetCurrentDirectory()).ConfigureAwait(false);
            }
            catch (Win32Exception) when (invocation.CanFallback)
            {
            }
            catch (InvalidOperationException) when (invocation.CanFallback)
            {
            }
        }

        Console.Error.WriteLine("Unable to locate `ulinkrpc-starter`.");
        Console.Error.WriteLine("Install it globally or expose it on PATH before running `ulinkgame-tool new`.");
        return 1;
    }

    private static async Task<int> RunULinkRpcCodegenAsync(string projectRoot, ToolConfig config, bool noRestore)
    {
        if (!noRestore)
        {
            var restoreExitCode = await RunProcessAsync("dotnet", ["tool", "restore"], projectRoot).ConfigureAwait(false);
            if (restoreExitCode != 0)
            {
                return restoreExitCode;
            }
        }

        foreach (var arguments in BuildCodegenInvocations(projectRoot, config))
        {
            var exitCode = await RunProcessAsync("dotnet", ["tool", "run", "ulinkrpc-codegen", "--", .. arguments], projectRoot).ConfigureAwait(false);
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    private static IEnumerable<string[]> BuildCodegenInvocations(string projectRoot, ToolConfig config)
    {
        var codegen = config.Codegen;
        var contractsPath = Path.Combine(projectRoot, codegen.ContractsPath);

        if (codegen.Server is not null)
        {
            yield return [
                "--mode", "server",
                "--contracts", contractsPath,
                "--server-output", Path.Combine(projectRoot, codegen.Server.ProjectPath, codegen.Server.OutputPath),
                "--server-namespace", codegen.Server.Namespace
            ];
        }

        if (codegen.UnityClient is not null)
        {
            yield return [
                "--mode", "unity",
                "--contracts", contractsPath,
                "--output", Path.Combine(projectRoot, codegen.UnityClient.ProjectPath, codegen.UnityClient.OutputPath),
                "--namespace", codegen.UnityClient.Namespace
            ];
        }
    }

    private static IEnumerable<ProcessInvocation> EnumerateULinkRpcStarterInvocations(IReadOnlyList<string> commandArguments)
    {
        yield return new ProcessInvocation("ulinkrpc-starter", commandArguments, true);
        yield return new ProcessInvocation("dotnet", ["tool", "run", "ulinkrpc-starter", "--", .. commandArguments], true);
    }

    private static async Task AugmentProjectWithULinkGameServerAsync(string projectRoot, NewCommandOptions options)
    {
        await WriteDotNetToolManifestAsync(projectRoot).ConfigureAwait(false);
        await WriteServerSolutionAsync(projectRoot).ConfigureAwait(false);
        await WriteGatewayProgramAsync(projectRoot).ConfigureAwait(false);
        await WriteGatewayProjectAsync(projectRoot, options).ConfigureAwait(false);
        await WriteGatewayAppSettingsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteGatewayConfiguratorsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteSiloProjectAsync(projectRoot).ConfigureAwait(false);
        await WriteSiloProgramAsync(projectRoot).ConfigureAwait(false);
        await WriteSiloAppSettingsAsync(projectRoot).ConfigureAwait(false);
    }

    private static async Task ReplaceGeneratedClientWithGodotClientAsync(string projectRoot, string projectName, NewCommandOptions options)
    {
        var clientDirectory = Path.Combine(projectRoot, "Client");
        if (Directory.Exists(clientDirectory))
        {
            Directory.Delete(clientDirectory, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scenes"));
        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scripts"));
        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scripts", "Rpc", "Generated"));

        await Task.WhenAll(
            File.WriteAllTextAsync(Path.Combine(clientDirectory, $"{projectName}.csproj"), RenderGodotProject(projectName, options) + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(clientDirectory, "project.godot"), RenderGodotProjectSettings(projectName) + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(clientDirectory, "Scenes", "Main.tscn"), RenderGodotMainScene() + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(clientDirectory, "Scripts", "Main.cs"), RenderGodotMainScript(projectName) + Environment.NewLine)).ConfigureAwait(false);
    }

    private static string RenderGodotProject(string projectName, NewCommandOptions options)
    {
        var (serializerPackage, _) = GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = GetTransportArtifacts(options.Transport);
        var rootNamespace = SanitizeCSharpIdentifier(projectName.Replace('.', '_'));

        return $"""
        <Project Sdk="Godot.NET.Sdk/4.3.0">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <RootNamespace>{rootNamespace}</RootNamespace>
            <Nullable>enable</Nullable>
            <EnableDynamicLoading>true</EnableDynamicLoading>
          </PropertyGroup>

          <ItemGroup>
            <ProjectReference Include="..\Shared\Shared.csproj" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="ULinkGame.Client" Version="{ULinkGameClientVersion}" />
            <PackageReference Include="ULinkRPC.Client" Version="0.11.1" />
            <PackageReference Include="{transportPackage.PackageId}" Version="{transportPackage.Version}" />
            <PackageReference Include="{serializerPackage.PackageId}" Version="{serializerPackage.Version}" />
          </ItemGroup>
        </Project>
        """;
    }

    private static string RenderGodotProjectSettings(string projectName)
    {
        return $$"""
        ; Engine configuration file.

        config_version=5

        [application]

        config/name="{{SanitizeGodotString(projectName)}}"
        run/main_scene="res://Scenes/Main.tscn"
        config/features=PackedStringArray("4.3", "C#", "Forward Plus")

        [dotnet]

        project/assembly_name="{{SanitizeGodotString(projectName)}}"
        """;
    }

    private static string RenderGodotMainScene()
    {
        return """
        [gd_scene load_steps=2 format=3 uid="uid://ulinkgame_starter_main"]

        [ext_resource type="Script" path="res://Scripts/Main.cs" id="1_main"]

        [node name="Main" type="Node2D"]
        script = ExtResource("1_main")
        """;
    }

    private static string RenderGodotMainScript(string projectName)
    {
        var namespaceName = SanitizeCSharpIdentifier(projectName.Replace('.', '_'));

        return $$"""
        using Godot;
        using Shared.Interfaces;

        namespace {{namespaceName}}.Scripts;

        public partial class Main : Node2D
        {
            public override void _Draw()
            {
                var request = new PingRequest { Message = "Hello from Godot" };
                DrawString(ThemeDB.FallbackFont, new Vector2(24, 40), $"ULinkGame Godot client ready: {request.Message}");
            }
        }
        """;
    }

    private static bool IsGodot(string clientEngine)
    {
        return string.Equals(clientEngine, "godot", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteDotNetToolManifestAsync(string projectRoot)
    {
        var toolManifestDirectory = Path.Combine(projectRoot, ".config");
        Directory.CreateDirectory(toolManifestDirectory);

        var content =
            $$"""
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "ulinkrpc.starter": {
                  "version": "{{ULinkRpcStarterVersion}}",
                  "commands": [
                    "ulinkrpc-starter",
                    "ulinkrpc-codegen"
                  ],
                  "rollForward": false
                }
              }
            }
            """;

        return File.WriteAllTextAsync(Path.Combine(toolManifestDirectory, "dotnet-tools.json"), content + Environment.NewLine);
    }

    private static Task WriteServerSolutionAsync(string projectRoot)
    {
        const string content =
            """
            <Solution>
              <Project Path="../Shared/Shared.csproj" />
              <Project Path="Server/Server.csproj" />
              <Project Path="Silo/Silo.csproj" />
            </Solution>
            """;
        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Server.slnx"), content + Environment.NewLine);
    }

    private static Task WriteGatewayProgramAsync(string projectRoot)
    {
        const string content =
            """
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using Server.Hosting;
            using ULinkGame.Server.Hosting;

            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.AddULinkGameServerOrleansClient();
            builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
                GatewayRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "ControlPlane",
                    new GatewayRpcServerOptions { Transport = "websocket", Port = 20000, Path = "/ws" })));
            builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
                GatewayRpcServerOptions.FromConfiguration(
                    builder.Configuration,
                    "Realtime",
                    new GatewayRpcServerOptions { Transport = "kcp", Port = 20001, Path = "" })));
            builder.Services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
            builder.Services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
            builder.Services.AddULinkGameServerGateway();

            var host = builder.Build();
            await host.RunAsync();
            """;
        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Server", "Program.cs"), content + Environment.NewLine);
    }

    private static Task WriteGatewayProjectAsync(string projectRoot, NewCommandOptions options)
    {
        var (serializerPackage, _) = GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = GetTransportArtifacts(options.Transport);

        var content =
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RootNamespace>Server</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\Shared\Shared.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="ULinkGame.Server" Version="{ULinkGameServerVersion}" />
                <PackageReference Include="{transportPackage.PackageId}" Version="{transportPackage.Version}" />
                <PackageReference Include="{serializerPackage.PackageId}" Version="{serializerPackage.Version}" />
                <PackageReference Include="Npgsql" Version="{NpgsqlVersion}" />
              </ItemGroup>

              <ItemGroup>
                <None Update="appsettings.json">
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                </None>
              </ItemGroup>
            </Project>
            """;

        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Server", "Server.csproj"), content + Environment.NewLine);
    }

    private static Task WriteGatewayAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        var realtimePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/realtime" : "";
        var controlPlanePath = string.Equals(options.Transport, "websocket", StringComparison.OrdinalIgnoreCase) ? "/ws" : "";

        var content =
            $$"""
            {
              "Orleans": {
                "ClusterId": "dev",
                "ServiceId": "{{SanitizeJson(options.Name ?? "MyGame")}}-Server",
                "Invariant": "Npgsql",
                "ConnectionString": "Host=127.0.0.1;Port=5432;Database={{SanitizeJson((options.Name ?? "mygame").ToLowerInvariant())}};Username=postgres;Password=postgres"
              },
              "ControlPlane": {
                "Port": 20000,
                "Path": "{{SanitizeJson(controlPlanePath)}}"
              },
              "Realtime": {
                "Transport": "{{SanitizeJson(options.Transport)}}",
                "Host": "127.0.0.1",
                "Port": 20001,
                "Path": "{{SanitizeJson(realtimePath)}}"
              }
            }
            """;

        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Server", "appsettings.json"), content + Environment.NewLine);
    }

    private static Task WriteGatewayConfiguratorsAsync(string projectRoot, NewCommandOptions options)
    {
        var hostingDirectory = Path.Combine(projectRoot, "Server", "Server", "Hosting");
        Directory.CreateDirectory(hostingDirectory);

        return Task.WhenAll(
            File.WriteAllTextAsync(Path.Combine(hostingDirectory, "GatewayRpcServerOptions.cs"), RenderGatewayRpcServerOptions() + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(hostingDirectory, "ControlPlaneRpcServerOptions.cs"), RenderNamedRpcServerOptions("ControlPlaneRpcServerOptions") + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(hostingDirectory, "RealtimeRpcServerOptions.cs"), RenderNamedRpcServerOptions("RealtimeRpcServerOptions") + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(hostingDirectory, "DefaultControlPlaneRpcServerConfigurator.cs"), RenderControlPlaneConfigurator(options) + Environment.NewLine),
            File.WriteAllTextAsync(Path.Combine(hostingDirectory, "DefaultRealtimeRpcServerConfigurator.cs"), RenderRealtimeConfigurator(options) + Environment.NewLine));
    }

    private static string RenderGatewayRpcServerOptions()
    {
        return @"using Microsoft.Extensions.Configuration;

namespace Server.Hosting;

internal sealed class GatewayRpcServerOptions
{
    public string Transport { get; init; } = ""websocket"";
    public string Host { get; init; } = ""127.0.0.1"";
    public int Port { get; init; } = 20000;
    public string Path { get; init; } = """";

    public static GatewayRpcServerOptions FromConfiguration(
        IConfiguration configuration,
        string sectionName,
        GatewayRpcServerOptions defaults)
    {
        var section = configuration.GetSection(sectionName);
        var transport = NormalizeTransport(section[""Transport""], defaults.Transport);
        var host = section[""Host""];
        var path = section[""Path""];

        return new GatewayRpcServerOptions
        {
            Transport = transport,
            Host = string.IsNullOrWhiteSpace(host) ? defaults.Host : host,
            Port = ParsePort(section[""Port""], defaults.Port),
            Path = string.IsNullOrWhiteSpace(path) ? defaults.Path : path
        };
    }

    private static string NormalizeTransport(string? rawValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? fallback
            : rawValue.Trim().ToLowerInvariant();
    }

    private static int ParsePort(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var port) && port > 0
            ? port
            : fallback;
    }
}";
    }

    private static string RenderNamedRpcServerOptions(string typeName)
    {
        return $@"namespace Server.Hosting;

internal sealed class {typeName}
{{
    public {typeName}(GatewayRpcServerOptions endpoint)
    {{
        Endpoint = endpoint;
    }}

    public GatewayRpcServerOptions Endpoint {{ get; }}
}}";
    }

    private static Task WriteSiloProjectAsync(string projectRoot)
    {
        var content =
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="ULinkGame.Server" Version="{{ULinkGameServerVersion}}" />
                <PackageReference Include="Microsoft.Orleans.Clustering.AdoNet" Version="{{OrleansVersion}}" />
                <PackageReference Include="Microsoft.Orleans.Persistence.AdoNet" Version="{{OrleansVersion}}" />
                <PackageReference Include="Microsoft.Orleans.Server" Version="{{OrleansVersion}}" />
                <PackageReference Include="Npgsql" Version="{{NpgsqlVersion}}" />
              </ItemGroup>

              <ItemGroup>
                <None Update="appsettings.json">
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                </None>
              </ItemGroup>
            </Project>
            """;

        var siloDirectory = Path.Combine(projectRoot, "Server", "Silo");
        Directory.CreateDirectory(siloDirectory);
        return File.WriteAllTextAsync(Path.Combine(siloDirectory, "Silo.csproj"), content + Environment.NewLine);
    }

    private static Task WriteSiloProgramAsync(string projectRoot)
    {
        const string content =
            """
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.Hosting;
            using Orleans.Hosting;
            using ULinkGame.Server.Hosting;

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(configuration =>
                {
                    configuration
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables();
                })
                .UseULinkGameServerOrleansSilo((context, silo) =>
                {
                    var configuration = context.Configuration;
                    var invariant = configuration["Orleans:Invariant"] ?? "Npgsql";
                    var connectionString = configuration["Orleans:ConnectionString"]
                        ?? throw new InvalidOperationException("Missing configuration: Orleans:ConnectionString");

                    silo.AddAdoNetGrainStorage("users", options =>
                    {
                        options.Invariant = invariant;
                        options.ConnectionString = connectionString;
                    });
                    silo.AddAdoNetGrainStorage("sessions", options =>
                    {
                        options.Invariant = invariant;
                        options.ConnectionString = connectionString;
                    });
                    silo.AddAdoNetGrainStorage("matchmaking", options =>
                    {
                        options.Invariant = invariant;
                        options.ConnectionString = connectionString;
                    });
                    silo.AddAdoNetGrainStorage("rooms", options =>
                    {
                        options.Invariant = invariant;
                        options.ConnectionString = connectionString;
                    });
                })
                .Build();

            await host.RunAsync();
            """;
        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Silo", "Program.cs"), content + Environment.NewLine);
    }

    private static Task WriteSiloAppSettingsAsync(string projectRoot)
    {
        const string content =
            """
            {
              "Orleans": {
                "ClusterId": "dev",
                "ServiceId": "ULinkGame-Server",
                "Invariant": "Npgsql",
                "ConnectionString": "Host=127.0.0.1;Port=5432;Database=ulinkgame;Username=postgres;Password=postgres",
                "SiloPort": 11111,
                "GatewayPort": 30000
              }
            }
            """;
        return File.WriteAllTextAsync(Path.Combine(projectRoot, "Server", "Silo", "appsettings.json"), content + Environment.NewLine);
    }

    private static string RenderControlPlaneConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = GetTransportArtifacts(options.Transport);

        return $@"using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Server.Hosting;

internal sealed class DefaultControlPlaneRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly GatewayRpcServerOptions _options;

    public DefaultControlPlaneRpcServerConfigurator(ControlPlaneRpcServerOptions options)
    {{
        _options = options.Endpoint;
    }}

    public string Name => ""control"";

    public void Configure(ULinkGameServerRpcContext context)
    {{
        var builder = context.Builder;
        builder.UseSerializer(new {serializerType}());
{IndentBlock(RenderControlPlaneAcceptor(options.Transport), 2)}
    }}
}}";
    }

    private static string RenderRealtimeConfigurator(NewCommandOptions options)
    {
        var (serializerPackage, serializerType) = GetSerializerArtifacts(options.Serializer);
        var (transportPackage, _) = GetTransportArtifacts(options.Transport);

        return $@"using ULinkGame.Server.Hosting;
using {serializerPackage.Namespace};
using {transportPackage.Namespace};

namespace Server.Hosting;

internal sealed class DefaultRealtimeRpcServerConfigurator : IULinkRpcServerConfigurator
{{
    private readonly GatewayRpcServerOptions _options;

    public DefaultRealtimeRpcServerConfigurator(RealtimeRpcServerOptions options)
    {{
        _options = options.Endpoint;
    }}

    public string Name => ""realtime"";

    public void Configure(ULinkGameServerRpcContext context)
    {{
        var builder = context.Builder;
        builder.UseSerializer(new {serializerType}());
{IndentBlock(RenderRealtimeAcceptor(options.Transport), 2)}
    }}
}}";
    }

    private static string RenderControlPlaneAcceptor(string transport)
    {
        return transport switch
        {
            "websocket" => """
                var path = string.IsNullOrWhiteSpace(_options.Path) ? "/ws" : _options.Path;
                builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
                    builder.ResolvePort(_options.Port),
                    path,
                    builder.Limits.MaxPendingAcceptedConnections,
                    ct));
                """,
            "tcp" => """
                builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(_options.Port)));
                """,
            _ => """
                builder.UseAcceptor(new KcpConnectionAcceptor(
                    builder.ResolvePort(_options.Port),
                    builder.Limits.MaxPendingAcceptedConnections));
                """
        };
    }

    private static string RenderRealtimeAcceptor(string transport)
    {
        return transport switch
        {
            "websocket" => """
                var path = string.IsNullOrWhiteSpace(_options.Path) ? "/realtime" : _options.Path;
                builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
                    builder.ResolvePort(_options.Port),
                    path,
                    builder.Limits.MaxPendingAcceptedConnections,
                    ct));
                """,
            "tcp" => """
                builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(_options.Port)));
                """,
            _ => """
                builder.UseAcceptor(new KcpConnectionAcceptor(
                    builder.ResolvePort(_options.Port),
                    builder.Limits.MaxPendingAcceptedConnections));
                """
        };
    }

    private static (PackageArtifact PackageId, string SerializerType) GetSerializerArtifacts(string serializer)
    {
        return serializer switch
        {
            "json" => (new PackageArtifact("ULinkRPC.Serializer.Json", "0.11.0", "ULinkRPC.Serializer.Json"), "JsonRpcSerializer"),
            _ => (new PackageArtifact("ULinkRPC.Serializer.MemoryPack", "0.11.0", "ULinkRPC.Serializer.MemoryPack"), "MemoryPackRpcSerializer")
        };
    }

    private static (PackageArtifact PackageId, string AcceptorType) GetTransportArtifacts(string transport)
    {
        return transport switch
        {
            "tcp" => (new PackageArtifact("ULinkRPC.Transport.Tcp", "0.11.2", "ULinkRPC.Transport.Tcp"), "TcpConnectionAcceptor"),
            "websocket" => (new PackageArtifact("ULinkRPC.Transport.WebSocket", "0.11.3", "ULinkRPC.Transport.WebSocket"), "WsConnectionAcceptor"),
            _ => (new PackageArtifact("ULinkRPC.Transport.Kcp", "0.11.8", "ULinkRPC.Transport.Kcp"), "KcpConnectionAcceptor")
        };
    }

    private static string SanitizeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string SanitizeGodotString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string SanitizeCSharpIdentifier(string value)
    {
        var sanitized = new string(value.Select(static c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "Game";
        }

        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }

    private static string IndentBlock(string block, int level)
    {
        var indent = new string(' ', level * 4);
        var lines = block.Replace("\r\n", "\n").Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : indent + line));
    }

    private static async Task<ToolConfig> LoadConfigAsync(string configPath)
    {
        await using var stream = File.OpenRead(configPath);
        var config = await JsonSerializer.DeserializeAsync<ToolConfig>(stream, JsonOptions).ConfigureAwait(false);
        return config ?? throw new InvalidOperationException($"Failed to parse tool config: {configPath}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            ULinkGame.Tool

            Commands:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack] [--nugetforunity-source embedded|openupm]
                  Generate a ULinkRPC project via ulinkrpc-starter, then augment it with ULinkGame.Server and Microsoft Orleans.

              codegen [--config <path>] [--no-restore]
                  Delegate code generation to ulinkrpc-starter codegen.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly record struct RegenerateCodeOptions(string? ConfigPath, bool NoRestore);
    private readonly record struct ProcessInvocation(string FileName, IReadOnlyList<string> Arguments, bool CanFallback);
    private readonly record struct PackageArtifact(string PackageId, string Version, string Namespace);
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
                    ProjectPath = "Server/Server",
                    OutputPath = "Generated",
                    Namespace = "Server.Generated"
                },
                UnityClient = new CodegenTargetConfig
                {
                    ProjectPath = "Client",
                    OutputPath = string.Equals(options.ClientEngine, "godot", StringComparison.OrdinalIgnoreCase) ? "Scripts/Rpc/Generated" : "Assets/Scripts/Rpc/Generated",
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
    public string Name { get; set; } = "MyGame";
    public string ClientEngine { get; set; } = "unity";
    public string Transport { get; set; } = "kcp";
    public string Serializer { get; set; } = "memorypack";
    public string NuGetForUnitySource { get; set; } = "embedded";
}

internal readonly record struct NewCommandOptions(
    string? Name,
    string? OutputPath,
    string ClientEngine,
    string Transport,
    string Serializer,
    string NuGetForUnitySource);

internal sealed class CliUsageException(string message) : Exception(message);

internal sealed class CodegenTargetConfig
{
    public string ProjectPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Namespace { get; set; } = "";
}
