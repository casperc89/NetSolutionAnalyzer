using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode;
using RefactorCli.Commands.DependencyGraph;
using RefactorCli.Commands.MethodDependency;
using RefactorCli.Commands.SystemWebCatalog;
using RefactorCli.Infrastructure;
using System.CommandLine;

var rootCommand = new RootCommand("Refactor CLI for incremental .NET migrations")
{
    Name = "refactor"
};

ICommandModule[] modules =
[
    new SystemWebCatalogCommandModule(),
    new DependencyGraphCommandModule(),
    new MethodDependencyCommandModule(),
    new DeadCodeCommandModule()
];

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLogging(builder => builder.AddConsole());
        services.AddInfrastructure();

        foreach (var module in modules)
        {
            module.RegisterServices(services);
        }
    })
    .Build();

foreach (var module in modules)
{
    module.RegisterCommands(rootCommand, host.Services);
}

var exitCode = await rootCommand.InvokeAsync(args);
return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
