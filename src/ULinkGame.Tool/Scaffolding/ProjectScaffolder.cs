internal sealed class ProjectScaffolder
{
    public async Task AugmentProjectWithULinkGameAsync(string projectRoot, NewCommandOptions options)
    {
        EnsureStarterServerProjectDirectory(projectRoot);
        await WriteClientPackageReferenceAsync(projectRoot, options).ConfigureAwait(false);
        await WriteServerSolutionAsync(projectRoot).ConfigureAwait(false);
        await WriteServerProgramAsync(projectRoot, options).ConfigureAwait(false);
        await WriteServerProjectAsync(projectRoot, options).ConfigureAwait(false);
        await WriteServerAppSettingsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteServerConfiguratorsAsync(projectRoot, options).ConfigureAwait(false);
        await WriteOperationsScaffoldingAsync(projectRoot, options).ConfigureAwait(false);
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

    private static Task WriteServerSolutionAsync(string projectRoot)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server.slnx"), ToolTemplates.RenderServerSolution());
    }

    private static void EnsureStarterServerProjectDirectory(string projectRoot)
    {
        var starterServerDirectory = Path.Combine(projectRoot, ToNativePath(ProjectConventions.StarterServerProjectPath));

        Directory.CreateDirectory(starterServerDirectory);
    }

    private static Task WriteServerProgramAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server", "Program.cs"), ToolTemplates.RenderServerProgram(options));
    }

    private static async Task WriteServerProjectAsync(string projectRoot, NewCommandOptions options)
    {
        var path = Path.Combine(projectRoot, "Server", "Server", "Server.csproj");
        if (!File.Exists(path))
        {
            await WriteAsync(path, ToolTemplates.RenderServerProject(options)).ConfigureAwait(false);
            return;
        }

        var document = System.Xml.Linq.XDocument.Load(path);
        var project = document.Root ?? throw new InvalidOperationException($"Invalid project file: {path}");

        SetProperty(project, "OutputType", "Exe");
        SetProperty(project, "TargetFramework", "net10.0");
        RemoveProperty(project, "TargetFrameworks");
        SetProperty(project, "ImplicitUsings", "enable");
        SetProperty(project, "Nullable", "enable");
        SetProperty(project, "RootNamespace", "Server");
        SetProperty(project, "BuildInParallel", "false");
        SetProperty(project, "RestoreBuildInParallel", "false");
        SetProperty(project, "ULinkRPCGenerateServer", "true");
        SetProperty(project, "ULinkRPCServerGeneratedNamespace", ProjectConventions.StarterServerGeneratedNamespace);

        EnsureProjectReference(project, @"..\..\Shared\Shared.csproj", "net10.0");
        EnsurePackageReference(project, "ULinkGame.Server", ToolPackageVersions.ULinkGameServer);
        EnsureClusterPackageReferences(project, options);
        EnsurePersistenceProviderReference(project, options.Persistence, includeDapper: true);
        EnsureNoneUpdate(project, "appsettings.json", "PreserveNewest");

        await File.WriteAllTextAsync(path, document.ToString() + Environment.NewLine).ConfigureAwait(false);
    }

    private static Task WriteServerAppSettingsAsync(string projectRoot, NewCommandOptions options)
    {
        return WriteAsync(Path.Combine(projectRoot, "Server", "Server", "appsettings.json"), ToolTemplates.RenderServerAppSettings(options));
    }

    private static Task WriteServerConfiguratorsAsync(string projectRoot, NewCommandOptions options)
    {
        var hostingDirectory = Path.Combine(projectRoot, "Server", "Server", "Hosting");
        Directory.CreateDirectory(hostingDirectory);

        if (ProjectConventions.IsRealtimeNetworkProfile(options.NetworkProfile))
        {
            return Task.WhenAll(
                WriteAsync(Path.Combine(hostingDirectory, "ServerRpcServerOptions.cs"), ToolTemplates.RenderServerRpcServerOptions()),
                WriteAsync(Path.Combine(hostingDirectory, "ControlPlaneRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("ControlPlaneRpcServerOptions")),
                WriteAsync(Path.Combine(hostingDirectory, "RealtimeRpcServerOptions.cs"), ToolTemplates.RenderNamedRpcServerOptions("RealtimeRpcServerOptions")),
                WriteAsync(Path.Combine(hostingDirectory, "DefaultControlPlaneRpcServerConfigurator.cs"), ToolTemplates.RenderControlPlaneConfigurator(options)),
                WriteAsync(Path.Combine(hostingDirectory, "DefaultRealtimeRpcServerConfigurator.cs"), ToolTemplates.RenderRealtimeConfigurator(options)));
        }

        var writes = new List<Task>
        {
            WriteAsync(Path.Combine(hostingDirectory, "ServerRpcServerOptions.cs"), ToolTemplates.RenderServerRpcServerOptions()),
            WriteAsync(Path.Combine(hostingDirectory, "DefaultRpcServerConfigurator.cs"), ToolTemplates.RenderDefaultConfigurator(options))
        };

        if (ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile))
        {
            writes.Add(WriteAsync(Path.Combine(hostingDirectory, "ClusterOptions.cs"), ToolTemplates.RenderClusterOptions()));
            writes.Add(WriteAsync(Path.Combine(hostingDirectory, "ClusterHealthCheck.cs"), ToolTemplates.RenderClusterHealthCheck()));
        }

        return Task.WhenAll(writes);
    }

    private static Task WriteOperationsScaffoldingAsync(string projectRoot, NewCommandOptions options)
    {
        if (!ProjectConventions.UsesComposeDeployProfile(options.DeployProfile))
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(
            WriteAsync(Path.Combine(projectRoot, "Server", "Dockerfile"), ToolTemplates.RenderServerDockerfile()),
            WriteAsync(Path.Combine(projectRoot, "docker-compose.cluster.yml"), ToolTemplates.RenderClusterCompose(options)),
            WriteAsync(Path.Combine(projectRoot, ".env.cluster.example"), ToolTemplates.RenderClusterEnvExample()),
            WriteAsync(Path.Combine(projectRoot, "ops", "CLUSTER_OPERATIONS.md"), ToolTemplates.RenderClusterOperationsGuide()));
    }

    private static Task WriteAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
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

    private static void EnsureClusterPackageReferences(System.Xml.Linq.XElement project, NewCommandOptions options)
    {
        if (!ProjectConventions.IsClusterNetworkProfile(options.NetworkProfile))
        {
            return;
        }

        EnsurePackageReference(project, "ULinkGame.Cluster", ToolPackageVersions.ULinkGameCluster);
        EnsurePackageReference(project, "ULinkGame.Cluster.ULinkRPC", ToolPackageVersions.ULinkGameClusterULinkRpc);
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
