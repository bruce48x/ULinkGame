using System.Globalization;

internal enum ToolLanguage
{
    English,
    SimplifiedChinese,
    TraditionalChinese
}

internal sealed class ToolText
{
    private ToolText(ToolLanguage language)
    {
        Language = language;
    }

    public ToolLanguage Language { get; }

    public static ToolText Current => ForCulture(CultureInfo.CurrentUICulture);

    public static ToolText ForCulture(CultureInfo culture) => new(DetectLanguage(culture));

    public static ToolLanguage DetectLanguage(CultureInfo culture)
    {
        var name = culture.Name;
        if (name.Length == 0)
            name = culture.TwoLetterISOLanguageName;

        var normalized = name.Replace('_', '-').ToLowerInvariant();
        if (!normalized.StartsWith("zh", StringComparison.Ordinal))
            return ToolLanguage.English;

        if (normalized.Contains("hant", StringComparison.Ordinal) ||
            normalized is "zh-cht" ||
            normalized is "zh-tw" or "zh-hk" or "zh-mo")
        {
            return ToolLanguage.TraditionalChinese;
        }

        return ToolLanguage.SimplifiedChinese;
    }

    public string ErrorPrefix => Language switch
    {
        ToolLanguage.SimplifiedChinese => "错误",
        ToolLanguage.TraditionalChinese => "錯誤",
        _ => "Error"
    };

    public string RunHelpForUsage => Language switch
    {
        ToolLanguage.SimplifiedChinese => "运行 `ulinkgame-tool help` 查看用法。",
        ToolLanguage.TraditionalChinese => "執行 `ulinkgame-tool help` 查看用法。",
        _ => "Run `ulinkgame-tool help` for usage."
    };

    public string HelpText => Language switch
    {
        ToolLanguage.SimplifiedChinese =>
            """
            ULinkGame.Tool

            命令:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--network-profile simple|realtime|cluster] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  通过 ulinkrpc-starter 生成 ULinkRPC 项目，然后补充 ULinkGame.Server、ULinkGame.Client 和 ULinkGame actor runtime。
                  默认使用 --network-profile simple，只创建一个 RPC endpoint。使用 realtime 可生成独立的 control 和 realtime endpoints；使用 cluster 可生成显式集群配置骨架。
            """,
        ToolLanguage.TraditionalChinese =>
            """
            ULinkGame.Tool

            命令:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--network-profile simple|realtime|cluster] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  透過 ulinkrpc-starter 生成 ULinkRPC 專案，然後補充 ULinkGame.Server、ULinkGame.Client 和 ULinkGame actor runtime。
                  預設使用 --network-profile simple，只建立一個 RPC endpoint。使用 realtime 可生成獨立的 control 和 realtime endpoints；使用 cluster 可生成明確的叢集設定骨架。
            """,
        _ =>
            """
            ULinkGame.Tool

            Commands:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--network-profile simple|realtime|cluster] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  Generate a ULinkRPC project via ulinkrpc-starter, then augment it with ULinkGame.Server, ULinkGame.Client, and the ULinkGame actor runtime.
                  Defaults to --network-profile simple, which creates one RPC endpoint. Use realtime for separate control and realtime endpoints; use cluster for explicit cluster configuration scaffolding.
            """
    };

    public string UnknownCommand(string command) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"未知命令: {command}",
        ToolLanguage.TraditionalChinese => $"未知命令: {command}",
        _ => $"Unknown command: {command}"
    };

    public string MissingValue(string optionName) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"{optionName} 缺少值。",
        ToolLanguage.TraditionalChinese => $"{optionName} 缺少值。",
        _ => $"Missing value for {optionName}."
    };

    public string UnsupportedValue(string value, string optionName, IReadOnlyCollection<string> supportedValues, string? suggestion)
    {
        var message = Language switch
        {
            ToolLanguage.SimplifiedChinese => $"{optionName} 不支持值 '{value}'。应为以下之一: {string.Join("|", supportedValues)}。",
            ToolLanguage.TraditionalChinese => $"{optionName} 不支援值 '{value}'。應為以下之一: {string.Join("|", supportedValues)}。",
            _ => $"Unsupported value '{value}' for {optionName}. Expected one of: {string.Join("|", supportedValues)}."
        };

        return suggestion is null ? message : $"{message} {DidYouMeanValue(suggestion)}";
    }

    public string UnexpectedArgument(string argument) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"意外参数: {argument}。",
        ToolLanguage.TraditionalChinese => $"非預期參數: {argument}。",
        _ => $"Unexpected argument: {argument}."
    };

    public string UnsupportedOption(string argument, string? suggestion)
    {
        var message = Language switch
        {
            ToolLanguage.SimplifiedChinese => $"不支持的选项: {argument}。",
            ToolLanguage.TraditionalChinese => $"不支援的選項: {argument}。",
            _ => $"Unsupported option: {argument}."
        };

        return suggestion is null ? message : $"{message} {DidYouMeanOption(suggestion)}";
    }

    public string GeneratedProjectRootNotFound(string projectRoot) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"未找到生成的项目根目录: {projectRoot}",
        ToolLanguage.TraditionalChinese => $"找不到生成的專案根目錄: {projectRoot}",
        _ => $"Generated project root not found: {projectRoot}"
    };

    public string ConfigAlreadyExists(string configPath) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"配置已存在: {configPath}",
        ToolLanguage.TraditionalChinese => $"設定已存在: {configPath}",
        _ => $"Config already exists: {configPath}"
    };

    public string CreatedToolConfig(string configPath) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"已创建工具配置: {configPath}",
        ToolLanguage.TraditionalChinese => $"已建立工具設定: {configPath}",
        _ => $"Created tool config: {configPath}"
    };

    public string NewProjectReadyHeader => Language switch
    {
        ToolLanguage.SimplifiedChinese => "ULinkGame 项目已就绪。下一步:",
        ToolLanguage.TraditionalChinese => "ULinkGame 專案已就緒。下一步:",
        _ => "ULinkGame project ready. Next steps:"
    };

    public string StartServerStep => Language switch
    {
        ToolLanguage.SimplifiedChinese => "  2) dotnet run --project \"Server/Edge/Edge.csproj\"",
        ToolLanguage.TraditionalChinese => "  2) dotnet run --project \"Server/Edge/Edge.csproj\"",
        _ => "  2) dotnet run --project \"Server/Edge/Edge.csproj\""
    };

    public string RebuildContractsStep => Language switch
    {
        ToolLanguage.SimplifiedChinese => "  3) 修改 Shared 合约后，重新构建 server 或重新打开/编译 client，使 ULinkRPC.Analyzers 重新生成 RPC glue。",
        ToolLanguage.TraditionalChinese => "  3) 修改 Shared 合約後，重新建置 server 或重新開啟/編譯 client，使 ULinkRPC.Analyzers 重新生成 RPC glue。",
        _ => "  3) After changing Shared contracts, rebuild the server or reopen/recompile the client so ULinkRPC.Analyzers regenerates RPC glue."
    };

    public string UnableToLocateStarter => Language switch
    {
        ToolLanguage.SimplifiedChinese => "无法找到 `ulinkrpc-starter`。",
        ToolLanguage.TraditionalChinese => "無法找到 `ulinkrpc-starter`。",
        _ => "Unable to locate `ulinkrpc-starter`."
    };

    public string InstallStarterBeforeNew => Language switch
    {
        ToolLanguage.SimplifiedChinese => "运行 `ulinkgame-tool new` 前，请全局安装它或将它加入 PATH。",
        ToolLanguage.TraditionalChinese => "執行 `ulinkgame-tool new` 前，請全域安裝它或將它加入 PATH。",
        _ => "Install it globally or expose it on PATH before running `ulinkgame-tool new`."
    };

    private string DidYouMeanValue(string suggestion) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"你是否想输入 '{suggestion}'?",
        ToolLanguage.TraditionalChinese => $"你是否想輸入 '{suggestion}'?",
        _ => $"Did you mean '{suggestion}'?"
    };

    private string DidYouMeanOption(string suggestion) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"你是否想输入 {suggestion}?",
        ToolLanguage.TraditionalChinese => $"你是否想輸入 {suggestion}?",
        _ => $"Did you mean {suggestion}?"
    };
}
