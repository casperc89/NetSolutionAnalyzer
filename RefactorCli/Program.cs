using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Analysis.Roslyn;
using RefactorCli.Commands.SystemWebCatalog;
using RefactorCli.Infrastructure;
using System.CommandLine;

var rootCommand = new RootCommand("Refactor CLI for incremental .NET migrations")
{
    Name = "refactor"
};

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLogging(builder => builder.AddConsole());

        services.AddInfrastructure();
        services.AddRoslynAnalysis();
        services.AddModule<SystemWebCatalogCommandModule>(rootCommand);
    })
    .Build();

CommandRuntime.ServiceProvider = host.Services;
var exitCode = await rootCommand.InvokeAsync(args);
return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
