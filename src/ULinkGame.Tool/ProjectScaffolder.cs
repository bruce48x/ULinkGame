internal sealed class ProjectScaffolder
{
    public async Task AugmentProjectWithULinkGameServerAsync(string projectRoot, NewCommandOptions options)
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

    public async Task ReplaceGeneratedClientWithGodotClientAsync(string projectRoot, string projectName, NewCommandOptions options)
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
            WriteAsync(Path.Combine(clientDirectory, $"{projectName}.csproj"), ToolTemplates.RenderGodotProject(projectName, options)),
            WriteAsync(Path.Combine(clientDirectory, "project.godot"), ToolTemplates.RenderGodotProjectSettings(projectName)),
            WriteAsync(Path.Combine(clientDirectory, "Scenes", "Main.tscn"), ToolTemplates.RenderGodotMainScene()),
            WriteAsync(Path.Combine(clientDirectory, "Scripts", "Main.cs"), ToolTemplates.RenderGodotMainScript(projectName))).ConfigureAwait(false);
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

    private static Task WriteGatewayProgramAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server", "Program.cs"), ToolTemplates.RenderGatewayProgram());
    }

    private static Task WriteGatewayProjectAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server", "Server.csproj"), ToolTemplates.RenderGatewayProject(options));
    }

    private static Task WriteGatewayAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server", "appsettings.json"), ToolTemplates.RenderGatewayAppSettings(options));
    }

    private static Task WriteGatewayConfiguratorsAsync(string projectRoot, NewCommandOptions options)
    {
        var hostingDirectory = Path.Combine(projectRoot, "Server", "Server", "Hosting");
        Directory.CreateDirectory(hostingDirectory);

        return Task.WhenAll(
            WriteAsync(Path.Combine(hostingDirectory, "GatewayRpcServerOptions.cs"), ToolTemplates.RenderGatewayRpcServerOptions()),
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
}
