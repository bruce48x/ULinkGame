internal sealed class ProjectScaffolder
{
    public async Task AugmentProjectWithULinkGameServerAsync(string projectRoot, NewCommandOptions options)
    {
        await MoveStarterServerProjectToEdgeAsync(projectRoot).ConfigureAwait(false);
        await WriteDotNetToolManifestAsync(projectRoot).ConfigureAwait(false);
        await WriteServerSolutionAsync(projectRoot).ConfigureAwait(false);
        await WriteEdgeProgramAsync(projectRoot).ConfigureAwait(false);
        await WriteEdgeProjectAsync(projectRoot, options).ConfigureAwait(false);
        await WriteEdgeAppSettingsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteEdgeConfiguratorsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteSiloProjectAsync(projectRoot).ConfigureAwait(false);
        await WriteSiloProgramAsync(projectRoot).ConfigureAwait(false);
        await WriteSiloAppSettingsAsync(projectRoot).ConfigureAwait(false);
    }

    public async Task ReplaceGeneratedClientWithGodotClientAsync(string projectRoot, string projectName, NewCommandOptions options)
    {
        var clientDirectory = Path.Combine(projectRoot, "Client");
        var generatedRpcFiles = await ReadDirectoryFilesAsync(
            Path.Combine(clientDirectory, "Scripts", "Rpc", "Generated")).ConfigureAwait(false);
        var nugetConfig = await ReadOptionalFileAsync(Path.Combine(clientDirectory, "NuGet.config")).ConfigureAwait(false);

        if (Directory.Exists(clientDirectory))
        {
            Directory.Delete(clientDirectory, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scenes"));
        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scripts"));
        Directory.CreateDirectory(Path.Combine(clientDirectory, "Scripts", "Rpc", "Generated"));

        await Task.WhenAll(
            WriteAsync(Path.Combine(clientDirectory, $"{projectName}.csproj"), ToolTemplates.RenderGodotProject(projectName, options)),
            WriteAsync(Path.Combine(clientDirectory, "project.godot"), ToolTemplates.RenderGodotProjectSettings(projectName)),
            WriteAsync(Path.Combine(clientDirectory, "Scenes", "Main.tscn"), ToolTemplates.RenderGodotMainScene()),
            WriteAsync(Path.Combine(clientDirectory, "Scripts", "Main.cs"), ToolTemplates.RenderGodotMainScript(projectName, options)),
            WriteAsync(Path.Combine(clientDirectory, "Scripts", "Rpc", "RpcClientFactory.cs"), ToolTemplates.RenderGodotRpcClientFactory(options))).ConfigureAwait(false);

        if (nugetConfig is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(clientDirectory, "NuGet.config"), nugetConfig).ConfigureAwait(false);
        }

        await WriteDirectoryFilesAsync(
            Path.Combine(clientDirectory, "Scripts", "Rpc", "Generated"),
            generatedRpcFiles).ConfigureAwait(false);
    }

    private static Task WriteDotNetToolManifestAsync(string projectRoot)
    {
        var toolManifestDirectory = Path.Combine(projectRoot, ".config");
        Directory.CreateDirectory(toolManifestDirectory);
        return WriteAsync(Path.Combine(toolManifestDirectory, "dotnet-tools.json"), ToolTemplates.RenderDotNetToolManifest());
    }

    private static Task WriteServerSolutionAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server.slnx"), ToolTemplates.RenderServerSolution());
    }

    private static async Task MoveStarterServerProjectToEdgeAsync(string projectRoot)
    {
        var starterServerDirectory = Path.Combine(projectRoot, ToNativePath(ProjectConventions.StarterServerProjectPath));
        var edgeDirectory = Path.Combine(projectRoot, ToNativePath(ProjectConventions.EdgeProjectPath));

        if (Directory.Exists(starterServerDirectory))
        {
            if (Directory.Exists(edgeDirectory))
            {
                throw new InvalidOperationException($"Both generated server directories exist: {starterServerDirectory} and {edgeDirectory}");
            }

            Directory.Move(starterServerDirectory, edgeDirectory);
        }
        else
        {
            Directory.CreateDirectory(edgeDirectory);
        }

        await RenameStarterServerNamespacesAsync(edgeDirectory).ConfigureAwait(false);
        DeleteIfExists(Path.Combine(edgeDirectory, "Server.csproj"));
    }

    private static async Task RenameStarterServerNamespacesAsync(string edgeDirectory)
    {
        if (!Directory.Exists(edgeDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(edgeDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            var updated = content.Replace(
                ProjectConventions.StarterServerGeneratedNamespace,
                ProjectConventions.EdgeGeneratedNamespace,
                StringComparison.Ordinal)
                .Replace("namespace Server.", "namespace Edge.", StringComparison.Ordinal)
                .Replace("using Server.", "using Edge.", StringComparison.Ordinal);

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(file, updated).ConfigureAwait(false);
            }
        }
    }

    private static Task WriteEdgeProgramAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Edge", "Program.cs"), ToolTemplates.RenderEdgeProgram());
    }

    private static Task WriteEdgeProjectAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Edge", "Edge.csproj"), ToolTemplates.RenderEdgeProject(options));
    }

    private static Task WriteEdgeAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Edge", "appsettings.json"), ToolTemplates.RenderEdgeAppSettings(options));
    }

    private static Task WriteEdgeConfiguratorsAsync(string projectRoot, NewCommandOptions options)
    {
        var hostingDirectory = Path.Combine(projectRoot, "Server", "Edge", "Hosting");
        Directory.CreateDirectory(hostingDirectory);

        return Task.WhenAll(
            WriteAsync(Path.Combine(hostingDirectory, "EdgeRpcServerOptions.cs"), ToolTemplates.RenderEdgeRpcServerOptions()),
            WriteAsync(Path.Combine(hostingDirectory, "ControlPlaneRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("ControlPlaneRpcServerOptions")),
            WriteAsync(Path.Combine(hostingDirectory, "RealtimeRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("RealtimeRpcServerOptions")),
            WriteAsync(Path.Combine(hostingDirectory, "DefaultControlPlaneRpcServerConfigurator.cs"), ToolTemplates.RenderControlPlaneConfigurator(options)),
            WriteAsync(Path.Combine(hostingDirectory, "DefaultRealtimeRpcServerConfigurator.cs"), ToolTemplates.RenderRealtimeConfigurator(options)));
    }

    private static Task WriteSiloProjectAsync(string projectRoot)
    {
        var siloDirectory = Path.Combine(projectRoot, "Server", "Silo");
        Directory.CreateDirectory(siloDirectory);
        return WriteAsync(Path.Combine(siloDirectory, "Silo.csproj"), ToolTemplates.RenderSiloProject());
    }

    private static Task WriteSiloProgramAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Silo", "Program.cs"), ToolTemplates.RenderSiloProgram());
    }

    private static Task WriteSiloAppSettingsAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Silo", "appsettings.json"), ToolTemplates.RenderSiloAppSettings());
    }

    private static Task WriteAsync(string path, string content)
    {
        return File.WriteAllTextAsync(path, content + Environment.NewLine);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string ToNativePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private static async Task<string?> ReadOptionalFileAsync(string path)
    {
        return File.Exists(path) ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : null;
    }

    private static async Task<IReadOnlyList<(string RelativePath, string Content)>> ReadDirectoryFilesAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        var result = new List<(string RelativePath, string Content)>(files.Length);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(directory, file);
            var content = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            result.Add((relativePath, content));
        }

        return result;
    }

    private static Task WriteDirectoryFilesAsync(
        string directory,
        IReadOnlyList<(string RelativePath, string Content)> files)
    {
        return Task.WhenAll(files.Select(file =>
        {
            var path = Path.Combine(directory, file.RelativePath);
            var parentDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            return File.WriteAllTextAsync(path, file.Content);
        }));
    }
}
