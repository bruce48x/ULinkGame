var exitCode = await new CliApplication(
        new ToolProcessRunner(),
        new ProjectScaffolder(),
        new ToolConfigStore())
    .RunAsync(args)
    .ConfigureAwait(false);

Environment.ExitCode = exitCode;
