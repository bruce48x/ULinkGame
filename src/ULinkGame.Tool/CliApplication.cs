using System.Text.Json;

internal sealed class CliApplication(
    ToolProcessRunner processRunner,
    ProjectScaffolder projectScaffolder,
    ToolConfigStore configStore)
{
    public async Task<int> RunAsync(string[] args)
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

    private async Task<int> NewAsync(string[] args)
    {
        var options = CliParser.ParseNewOptions(args);
        var outputDirectory = Path.GetFullPath(options.OutputPath ?? Directory.GetCurrentDirectory());
        Directory.CreateDirectory(outputDirectory);

        var projectName = string.IsNullOrWhiteSpace(options.Name) ? ProjectConventions.DefaultProjectName : options.Name;
        var projectRoot = Path.Combine(outputDirectory, projectName);

        var starterExitCode = await processRunner.RunStarterNewAsync(projectName, outputDirectory, options).ConfigureAwait(false);
        if (starterExitCode != 0)
        {
            return starterExitCode;
        }

        if (!Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"Generated project root not found: {projectRoot}");
            return 1;
        }

        if (ProjectConventions.IsGodot(options.ClientEngine))
        {
            await projectScaffolder.ReplaceGeneratedClientWithGodotClientAsync(projectRoot, projectName, options).ConfigureAwait(false);
        }

        await projectScaffolder.AugmentProjectWithULinkGameServerAsync(projectRoot, options).ConfigureAwait(false);

        var configPath = Path.Combine(projectRoot, ProjectConventions.ConfigFileName);
        if (File.Exists(configPath))
        {
            Console.Error.WriteLine($"Config already exists: {configPath}");
            return 1;
        }

        await configStore.SaveAsync(configPath, ToolConfig.CreateDefault(projectName, options)).ConfigureAwait(false);
        Console.WriteLine($"Created tool config: {configPath}");
        PrintNewProjectNextSteps(projectRoot);
        return 0;
    }

    private async Task<int> RegenerateCodeAsync(string[] args)
    {
        var options = CliParser.ParseRegenerateCodeOptions(args);
        var configPath = options.ConfigPath ?? Path.Combine(Directory.GetCurrentDirectory(), ProjectConventions.ConfigFileName);

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Missing tool config: {configPath}");
            Console.Error.WriteLine("Run `ulinkgame-tool new` first or pass --config <path>.");
            return 1;
        }

        var config = await configStore.LoadAsync(configPath).ConfigureAwait(false);
        var rootPath = Path.GetDirectoryName(Path.GetFullPath(configPath))
            ?? Directory.GetCurrentDirectory();

        var starterExitCode = await processRunner.RunCodegenAsync(rootPath, config, options.NoRestore).ConfigureAwait(false);
        if (starterExitCode != 0)
        {
            return starterExitCode;
        }

        Console.WriteLine("Code generation completed.");
        return 0;
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

    private static void PrintNewProjectNextSteps(string projectRoot)
    {
        Console.WriteLine("ULinkGame project ready. Next steps:");
        Console.WriteLine($"  1) cd \"{projectRoot}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Silo/Silo.csproj\"");
        Console.WriteLine("  3) dotnet run --project \"Server/Edge/Edge.csproj\"");
        Console.WriteLine("  4) After changing Shared contracts, run `ulinkgame-tool codegen` from the project root.");
    }
}

internal sealed class ToolConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task SaveAsync(string configPath, ToolConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        return File.WriteAllTextAsync(configPath, json);
    }

    public async Task<ToolConfig> LoadAsync(string configPath)
    {
        await using var stream = File.OpenRead(configPath);
        var config = await JsonSerializer.DeserializeAsync<ToolConfig>(stream, JsonOptions).ConfigureAwait(false);
        return config ?? throw new InvalidOperationException($"Failed to parse tool config: {configPath}");
    }
}
