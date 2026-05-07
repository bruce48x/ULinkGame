internal static class CliParser
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

    public static RegenerateCodeOptions ParseRegenerateCodeOptions(string[] args)
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

    public static NewCommandOptions ParseNewOptions(string[] args)
    {
        string? name = null;
        string? outputPath = null;
        var clientEngine = ProjectConventions.DefaultClientEngine;
        var transport = ProjectConventions.DefaultTransport;
        var serializer = ProjectConventions.DefaultSerializer;
        var nuGetForUnitySource = ProjectConventions.DefaultNuGetForUnitySource;

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
                    clientEngine = ValidateChoice("--client-engine", ReadOptionValue(args, ref index, "--client-engine"), ProjectConventions.SupportedClientEngines);
                    break;
                case "--transport":
                    transport = ValidateChoice("--transport", ReadOptionValue(args, ref index, "--transport"), ProjectConventions.SupportedTransports);
                    break;
                case "--serializer":
                    serializer = ValidateChoice("--serializer", ReadOptionValue(args, ref index, "--serializer"), ProjectConventions.SupportedSerializers);
                    break;
                case "--nugetforunity-source":
                    nuGetForUnitySource = ValidateChoice("--nugetforunity-source", ReadOptionValue(args, ref index, "--nugetforunity-source"), ProjectConventions.SupportedNuGetForUnitySources);
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
}
