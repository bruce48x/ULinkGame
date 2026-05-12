internal sealed class ProjectScaffolder
{
    public async Task AugmentProjectWithULinkGameAsync(string projectRoot, NewCommandOptions options)
    {
        await MoveStarterServerProjectToEdgeAsync(projectRoot).ConfigureAwait(false);
        await WriteClientPackageReferenceAsync(projectRoot, options).ConfigureAwait(false);
        await WriteDotNetToolManifestAsync(projectRoot).ConfigureAwait(false);
        await WriteServerSolutionAsync(projectRoot).ConfigureAwait(false);
        await WriteEdgeProgramAsync(projectRoot, options).ConfigureAwait(false);
        await WriteEdgeProjectAsync(projectRoot, options).ConfigureAwait(false);
        await WriteEdgeAppSettingsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteEdgeConfiguratorsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteSiloProjectAsync(projectRoot, options).ConfigureAwait(false);
        await WriteSiloProgramAsync(projectRoot).ConfigureAwait(false);
        await WriteSiloAppSettingsAsync(projectRoot, options).ConfigureAwait(false);
    }

    private static Task WriteClientPackageReferenceAsync(string projectRoot, NewCommandOptions options)
    {
        return ProjectConventions.IsGodot(options.ClientEngine)
            ? WriteGodotClientPackageReferenceAsync(projectRoot)
            : WriteUnityClientPackageReferenceAsync(projectRoot);
    }

    private static async Task WriteGodotClientPackageReferenceAsync(string projectRoot)
    {
        var clientDirectory = Path.Combine(projectRoot, "Client");
        if (!Directory.Exists(clientDirectory))
        {
            return;
        }

        var projectFiles = Directory.EnumerateFiles(clientDirectory, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
        if (projectFiles.Length == 0)
        {
            return;
        }

        if (projectFiles.Length > 1)
        {
            throw new InvalidOperationException($"Multiple client project files were found in: {clientDirectory}");
        }

        var path = projectFiles[0];
        var document = System.Xml.Linq.XDocument.Load(path);
        var project = document.Root ?? throw new InvalidOperationException($"Invalid project file: {path}");

        EnsurePackageReference(project, "ULinkGame.Client", ToolPackageVersions.ULinkGameClient);

        await File.WriteAllTextAsync(path, document.ToString() + Environment.NewLine).ConfigureAwait(false);
    }

    private static async Task WriteUnityClientPackageReferenceAsync(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Client", "Assets", "packages.config");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);

        System.Xml.Linq.XDocument document;
        if (File.Exists(path))
        {
            document = System.Xml.Linq.XDocument.Load(path);
        }
        else
        {
            document = new System.Xml.Linq.XDocument(
                new System.Xml.Linq.XDeclaration("1.0", "utf-8", null),
                new System.Xml.Linq.XElement("packages"));
        }

        var packages = document.Root ?? throw new InvalidOperationException($"Invalid packages.config file: {path}");
        if (!string.Equals(packages.Name.LocalName, "packages", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid packages.config root element in: {path}");
        }

        EnsureNuGetForUnityPackage(packages, "ULinkGame.Client", ToolPackageVersions.ULinkGameClient);

        await File.WriteAllTextAsync(path, document.ToString() + Environment.NewLine).ConfigureAwait(false);
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
        MoveStarterServerProjectFileToEdge(edgeDirectory);
    }

    private static void MoveStarterServerProjectFileToEdge(string edgeDirectory)
    {
        var starterProject = Path.Combine(edgeDirectory, "Server.csproj");
        var edgeProject = Path.Combine(edgeDirectory, "Edge.csproj");

        if (!File.Exists(starterProject))
        {
            return;
        }

        if (File.Exists(edgeProject))
        {
            throw new InvalidOperationException($"Both generated server project files exist: {starterProject} and {edgeProject}");
        }

        File.Move(starterProject, edgeProject);
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

    private static Task WriteEdgeProgramAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Edge", "Program.cs"), ToolTemplates.RenderEdgeProgram(options));
    }

    private static async Task WriteEdgeProjectAsync(string projectRoot, NewCommandOptions options)
    {
        var path = Path.Combine(projectRoot, "Server", "Edge", "Edge.csproj");
        if (!File.Exists(path))
        {
            await WriteAsync(path, ToolTemplates.RenderEdgeProject(options)).ConfigureAwait(false);
            return;
        }

        var document = System.Xml.Linq.XDocument.Load(path);
        var project = document.Root ?? throw new InvalidOperationException($"Invalid project file: {path}");

        SetProperty(project, "OutputType", "Exe");
        SetProperty(project, "TargetFramework", "net10.0");
        RemoveProperty(project, "TargetFrameworks");
        SetProperty(project, "ImplicitUsings", "enable");
        SetProperty(project, "Nullable", "enable");
        SetProperty(project, "RootNamespace", "Edge");
        SetProperty(project, "BuildInParallel", "false");
        SetProperty(project, "RestoreBuildInParallel", "false");

        EnsureProjectReference(project, @"..\..\Shared\Shared.csproj", "net10.0");
        EnsurePackageReference(project, "ULinkGame.Server", ToolPackageVersions.ULinkGameServer);
        EnsurePersistenceProviderReference(project, options.Persistence, includeDapper: false);
        EnsureNoneUpdate(project, "appsettings.json", "PreserveNewest");

        await File.WriteAllTextAsync(path, document.ToString() + Environment.NewLine).ConfigureAwait(false);
    }

    private static Task WriteEdgeAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Edge", "appsettings.json"), ToolTemplates.RenderEdgeAppSettings(options));
    }

    private static Task WriteEdgeConfiguratorsAsync(string projectRoot, NewCommandOptions options)
    {
        var hostingDirectory = Path.Combine(projectRoot, "Server", "Edge", "Hosting");
        Directory.CreateDirectory(hostingDirectory);

        if (ProjectConventions.IsRealtimeNetworkProfile(options.NetworkProfile))
        {
            return Task.WhenAll(
                WriteAsync(Path.Combine(hostingDirectory, "EdgeRpcServerOptions.cs"), ToolTemplates.RenderEdgeRpcServerOptions()),
                WriteAsync(Path.Combine(hostingDirectory, "ControlPlaneRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("ControlPlaneRpcServerOptions")),
                WriteAsync(Path.Combine(hostingDirectory, "RealtimeRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("RealtimeRpcServerOptions")),
                WriteAsync(Path.Combine(hostingDirectory, "DefaultControlPlaneRpcServerConfigurator.cs"), ToolTemplates.RenderControlPlaneConfigurator(options)),
                WriteAsync(Path.Combine(hostingDirectory, "DefaultRealtimeRpcServerConfigurator.cs"), ToolTemplates.RenderRealtimeConfigurator(options)));
        }

        return Task.WhenAll(
            WriteAsync(Path.Combine(hostingDirectory, "EdgeRpcServerOptions.cs"), ToolTemplates.RenderEdgeRpcServerOptions()),
            WriteAsync(Path.Combine(hostingDirectory, "DefaultRpcServerConfigurator.cs"), ToolTemplates.RenderDefaultConfigurator(options)));
    }

    private static Task WriteSiloProjectAsync(string projectRoot, NewCommandOptions options)
    {
        var siloDirectory = Path.Combine(projectRoot, "Server", "Silo");
        Directory.CreateDirectory(siloDirectory);
        return WriteAsync(Path.Combine(siloDirectory, "Silo.csproj"), ToolTemplates.RenderSiloProject(options));
    }

    private static Task WriteSiloProgramAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Silo", "Program.cs"), ToolTemplates.RenderSiloProgram());
    }

    private static Task WriteSiloAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Silo", "appsettings.json"), ToolTemplates.RenderSiloAppSettings(options));
    }

    private static Task WriteAsync(string path, string content)
    {
        return File.WriteAllTextAsync(path, content + Environment.NewLine);
    }

    private static string ToNativePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private static void SetProperty(System.Xml.Linq.XElement project, string name, string value)
    {
        var property = project.Elements("PropertyGroup").SelectMany(group => group.Elements(name)).FirstOrDefault();
        if (property is null)
        {
            var propertyGroup = project.Elements("PropertyGroup").FirstOrDefault() ?? AddElement(project, "PropertyGroup");
            propertyGroup.Add(new System.Xml.Linq.XElement(name, value));
            return;
        }

        property.Value = value;
    }

    private static void RemoveProperty(System.Xml.Linq.XElement project, string name)
    {
        foreach (var property in project.Elements("PropertyGroup").SelectMany(group => group.Elements(name)).ToArray())
        {
            property.Remove();
        }
    }

    private static void EnsureProjectReference(System.Xml.Linq.XElement project, string include, string targetFramework)
    {
        var reference = project
            .Descendants("ProjectReference")
            .FirstOrDefault(element => string.Equals(element.Attribute("Include")?.Value, include, StringComparison.OrdinalIgnoreCase));

        if (reference is null)
        {
            var itemGroup = FindOrAddItemGroup(project);
            reference = new System.Xml.Linq.XElement("ProjectReference", new System.Xml.Linq.XAttribute("Include", include));
            itemGroup.Add(reference);
        }

        reference.SetAttributeValue("TargetFramework", targetFramework);
        var setTargetFramework = reference.Elements("SetTargetFramework").FirstOrDefault();
        if (setTargetFramework is null)
        {
            reference.Add(new System.Xml.Linq.XElement("SetTargetFramework", $"TargetFramework={targetFramework}"));
        }
        else
        {
            setTargetFramework.Value = $"TargetFramework={targetFramework}";
        }
    }

    private static void EnsurePackageReference(System.Xml.Linq.XElement project, string include, string version)
    {
        var reference = project
            .Descendants("PackageReference")
            .FirstOrDefault(element => string.Equals(element.Attribute("Include")?.Value, include, StringComparison.OrdinalIgnoreCase));

        if (reference is null)
        {
            FindOrAddItemGroup(project).Add(new System.Xml.Linq.XElement(
                "PackageReference",
                new System.Xml.Linq.XAttribute("Include", include),
                new System.Xml.Linq.XAttribute("Version", version)));
            return;
        }

        reference.SetAttributeValue("Version", version);
    }

    private static void EnsurePersistenceProviderReference(System.Xml.Linq.XElement project, string persistence, bool includeDapper)
    {
        if (!ProjectConventions.UsesExternalPersistence(persistence))
        {
            return;
        }

        if (includeDapper)
        {
            EnsurePackageReference(project, "Dapper", ToolPackageVersions.Dapper);
        }

        if (string.Equals(persistence, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            EnsurePackageReference(project, "MySqlConnector", ToolPackageVersions.MySqlConnector);
            return;
        }

        EnsurePackageReference(project, "Npgsql", ToolPackageVersions.Npgsql);
    }

    private static void EnsureNuGetForUnityPackage(System.Xml.Linq.XElement packages, string id, string version)
    {
        var package = packages
            .Elements("package")
            .FirstOrDefault(element => string.Equals(element.Attribute("id")?.Value, id, StringComparison.OrdinalIgnoreCase));

        if (package is null)
        {
            packages.Add(new System.Xml.Linq.XElement(
                "package",
                new System.Xml.Linq.XAttribute("id", id),
                new System.Xml.Linq.XAttribute("version", version),
                new System.Xml.Linq.XAttribute("manuallyInstalled", "true")));
            return;
        }

        package.SetAttributeValue("version", version);
        package.SetAttributeValue("manuallyInstalled", "true");
    }

    private static void EnsureNoneUpdate(System.Xml.Linq.XElement project, string update, string copyToOutputDirectory)
    {
        var none = project
            .Descendants("None")
            .FirstOrDefault(element => string.Equals(element.Attribute("Update")?.Value, update, StringComparison.OrdinalIgnoreCase));

        if (none is null)
        {
            none = new System.Xml.Linq.XElement("None", new System.Xml.Linq.XAttribute("Update", update));
            FindOrAddItemGroup(project).Add(none);
        }

        var copy = none.Elements("CopyToOutputDirectory").FirstOrDefault();
        if (copy is null)
        {
            none.Add(new System.Xml.Linq.XElement("CopyToOutputDirectory", copyToOutputDirectory));
        }
        else
        {
            copy.Value = copyToOutputDirectory;
        }
    }

    private static System.Xml.Linq.XElement FindOrAddItemGroup(System.Xml.Linq.XElement project)
    {
        return project.Elements("ItemGroup").FirstOrDefault() ?? AddElement(project, "ItemGroup");
    }

    private static System.Xml.Linq.XElement AddElement(System.Xml.Linq.XElement parent, string name)
    {
        var element = new System.Xml.Linq.XElement(name);
        parent.Add(element);
        return element;
    }

}
