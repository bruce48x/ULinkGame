using System.ComponentModel;
using System.Diagnostics;

internal sealed class ToolProcessRunner
{
    public async Task<int> RunStarterNewAsync(string projectName, string outputDirectory, NewCommandOptions options)
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

        foreach (var invocation in EnumerateStarterInvocations(arguments))
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

    public async Task<int> RunCodegenAsync(string projectRoot, ToolConfig config, bool noRestore)
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

    private static IEnumerable<ProcessInvocation> EnumerateStarterInvocations(IReadOnlyList<string> commandArguments)
    {
        yield return new ProcessInvocation("ulinkrpc-starter", commandArguments, true);
        yield return new ProcessInvocation("dotnet", ["tool", "run", "ulinkrpc-starter", "--", .. commandArguments], true);
    }
}
