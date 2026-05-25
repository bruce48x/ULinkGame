using System.ComponentModel;
using System.Diagnostics;

internal sealed class ToolProcessRunner
{
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

    private static IEnumerable<ProcessInvocation> EnumerateStarterInvocations(IReadOnlyList<string> commandArguments)
    {
        yield return new ProcessInvocation("ulinkrpc-starter", commandArguments, true);
        yield return new ProcessInvocation("dotnet", ["tool", "run", "ulinkrpc-starter", "--", .. commandArguments], true);
    }
}
