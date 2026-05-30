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
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  通过 ulinkrpc-starter 生成 ULinkRPC 项目，然后补充 ULinkGame.Server、ULinkGame.Client 和 ULinkGame actor runtime。
                  默认生成显式 cluster 配置骨架，无需传入 network profile 参数。
            """,
        ToolLanguage.TraditionalChinese =>
            """
            ULinkGame.Tool

            命令:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  透過 ulinkrpc-starter 生成 ULinkRPC 專案，然後補充 ULinkGame.Server、ULinkGame.Client 和 ULinkGame actor runtime。
                  預設生成明確的 cluster 設定骨架，無需傳入 network profile 參數。
            """,
        _ =>
            """
            ULinkGame.Tool

            Commands:
              new [--name MyGame] [--output .] [--client-engine unity|unity-cn|tuanjie|godot] [--transport tcp|websocket|kcp] [--serializer json|memorypack] [--persistence none|mysql|postgres] [--nugetforunity-source embedded|openupm] [--deploy-profile none|compose]
                  Generate a ULinkRPC project via ulinkrpc-starter, then augment it with ULinkGame.Server, ULinkGame.Client, and the ULinkGame actor runtime.
                  Generates explicit cluster configuration scaffolding by default; no network profile argument is required.
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
        ToolLanguage.SimplifiedChinese => "  2) dotnet run --project \"Server/Server/Server.csproj\"",
        ToolLanguage.TraditionalChinese => "  2) dotnet run --project \"Server/Server/Server.csproj\"",
        _ => "  2) dotnet run --project \"Server/Server/Server.csproj\""
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

    public string InstallingStarter(string packageId, string version) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"未找到 `ulinkrpc-starter`，正在自动安装 `{packageId}` `{version}`...",
        ToolLanguage.TraditionalChinese => $"找不到 `ulinkrpc-starter`，正在自動安裝 `{packageId}` `{version}`...",
        _ => $"`ulinkrpc-starter` was not found. Installing `{packageId}` `{version}`..."
    };

    public string UnableToInstallStarter(string packageId) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"无法自动安装 `{packageId}`。",
        ToolLanguage.TraditionalChinese => $"無法自動安裝 `{packageId}`。",
        _ => $"Unable to install `{packageId}` automatically."
    };

    public string InstallStarterBeforeNew => Language switch
    {
        ToolLanguage.SimplifiedChinese => "请运行 `dotnet tool install --global ULinkRPC.Starter`，或确认 .NET 全局工具目录已加入 PATH。",
        ToolLanguage.TraditionalChinese => "請執行 `dotnet tool install --global ULinkRPC.Starter`，或確認 .NET 全域工具目錄已加入 PATH。",
        _ => "Run `dotnet tool install --global ULinkRPC.Starter`, or make sure the .NET global tools directory is on PATH."
    };

    public string StarterVersionMismatch(string installed, string expected) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"检测到 `ulinkrpc-starter` 版本不匹配：已安装 {installed}，需要 {expected}。正在自动更新...",
        ToolLanguage.TraditionalChinese => $"偵測到 `ulinkrpc-starter` 版本不符：已安裝 {installed}，需要 {expected}。正在自動更新...",
        _ => $"`ulinkrpc-starter` version mismatch detected: installed {installed}, expected {expected}. Updating..."
    };

    public string StarterUpdated(string version) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"`ulinkrpc-starter` 已更新至 {version}。",
        ToolLanguage.TraditionalChinese => $"`ulinkrpc-starter` 已更新至 {version}。",
        _ => $"`ulinkrpc-starter` has been updated to {version}."
    };

    public string UnableToUpdateStarter(string packageId) => Language switch
    {
        ToolLanguage.SimplifiedChinese => $"无法自动更新 `{packageId}`。",
        ToolLanguage.TraditionalChinese => $"無法自動更新 `{packageId}`。",
        _ => $"Unable to update `{packageId}` automatically."
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
