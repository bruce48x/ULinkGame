using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gateway.Features;
using ULinkGame.Server.Features;
using ULinkGame.Server.Hotfix;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddFeatures(builder.Configuration, features =>
{
    features.FromAssembly(typeof(GatewayRole).Assembly);
});

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var hotfix = scope.ServiceProvider.GetRequiredService<IHotfixManager>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Gateway.Hotfix");
    var result = await hotfix.ReloadAsync();
    if (result.Succeeded)
    {
        logger.LogInformation(
            "Initial hotfix load succeeded from {HotfixPath} with {MethodCount} method(s).",
            result.Current.SourcePath,
            result.Current.Methods.Count);
    }
    else
    {
        logger.LogWarning(
            "Initial hotfix load failed for {HotfixPath}: {ErrorMessage}",
            result.RequestedPath,
            result.ErrorMessage);
        foreach (var diagnostic in result.Diagnostics)
        {
            logger.LogWarning("Hotfix diagnostic: {Diagnostic}", diagnostic);
        }
    }
}

await host.RunAsync();
