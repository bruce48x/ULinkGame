using System.ComponentModel;
using System.Diagnostics;

internal sealed class ToolProcessRunner(ToolText? text = null)
{
    private readonly ToolText text = text ?? ToolText.Current;
    private const string StarterCommandName = "ulinkrpc-starter";
    private const string StarterPackageId = "ULinkRPC.Starter";

    public async Task<int> RunStarterNewAsync(string projectName, string outputDirectory, NewCommandOptions options)
    {
        var arguments = new[]
        {
            "new",
            "--name", projectName,
            "--output", outputDirectory,
            "--client-engine", options.ClientEngine,
            "--transport", options.Transport,
            "--serializer", options.Serializer,
            "--nugetforunity-source", options.NuGetForUnitySource,
            "--no-next-steps"
        };

        var directInvocation = new ProcessInvocation(StarterCommandName, arguments, true);
        var directResult = await TryRunProcessAsync(directInvocation).ConfigureAwait(false);
        if (directResult.Started)
        {
            return directResult.ExitCode;
        }

        var localToolInvocation = new ProcessInvocation("dotnet", ["tool", "run", StarterCommandName, "--", .. arguments], true);
        var localToolResult = await TryRunProcessAsync(localToolInvocation).ConfigureAwait(false);
        if (localToolResult.Started && localToolResult.ExitCode == 0)
        {
            return 0;
        }

        Console.Error.WriteLine(text.InstallingStarter(StarterPackageId, ToolPackageVersions.ULinkRpcStarter));
        var installInvocation = new ProcessInvocation(
            "dotnet",
            ["tool", "install", "--global", StarterPackageId, "--version", ToolPackageVersions.ULinkRpcStarter],
            true);
        var installResult = await TryRunProcessAsync(installInvocation).ConfigureAwait(false);
        if (!installResult.Started || installResult.ExitCode != 0)
        {
            Console.Error.WriteLine(text.UnableToInstallStarter(StarterPackageId));
            Console.Error.WriteLine(text.InstallStarterBeforeNew);
            return installResult.Started ? installResult.ExitCode : 1;
        }

        directResult = await TryRunProcessAsync(directInvocation).ConfigureAwait(false);
        if (directResult.Started)
        {
            return directResult.ExitCode;
        }

        Console.Error.WriteLine(text.UnableToLocateStarter);
        Console.Error.WriteLine(text.InstallStarterBeforeNew);
        return 1;
    }

    private static async Task<ProcessRunResult> TryRunProcessAsync(ProcessInvocation invocation)
    {
        try
        {
            var exitCode = await RunProcessAsync(invocation.FileName, invocation.Arguments, Directory.GetCurrentDirectory()).ConfigureAwait(false);
            return new ProcessRunResult(true, exitCode);
        }
        catch (Win32Exception) when (invocation.CanFallback)
        {
            return ProcessRunResult.NotStarted;
        }
        catch (InvalidOperationException) when (invocation.CanFallback)
        {
            return ProcessRunResult.NotStarted;
        }
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
}

internal readonly record struct ProcessRunResult(bool Started, int ExitCode)
{
    public static ProcessRunResult NotStarted => new(false, 1);
}
