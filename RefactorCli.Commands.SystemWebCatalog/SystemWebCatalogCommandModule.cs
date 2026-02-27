using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using System.CommandLine;

namespace RefactorCli.Commands.SystemWebCatalog;

public sealed class SystemWebCatalogCommandModule : ICommandModule
{
    public string Name => "systemweb";

    public void Register(Command root, IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<SystemWebCatalogOptions>, SystemWebCatalogCommandHandler>();

        var solutionOption = new Option<string>("--solution", "Path to a .sln file");
        var repoOption = new Option<string?>("--repo", "Path to a repository root (reserved for future use)");
        var outputOption = new Option<string>("--output", () => "./refactor-reports", "Output directory");
        var formatOption = new Option<string[]>(
            "--format",
            () => ["json", "md"],
            "Output formats: json|md|sarif");
        formatOption.AllowMultipleArgumentsPerToken = true;

        var verbosityOption = new Option<string>("--verbosity", () => "normal", "quiet|normal|diag");

        root.AddGlobalOption(solutionOption);
        root.AddGlobalOption(repoOption);
        root.AddGlobalOption(outputOption);
        root.AddGlobalOption(formatOption);
        root.AddGlobalOption(verbosityOption);

        var systemweb = new Command("systemweb", "System.Web migration catalog commands");
        var catalog = new Command("catalog", "Catalog System.Web usage");

        catalog.SetHandler(async (string? solution, string output, string[] format, string verbosity) =>
        {
            if (string.IsNullOrWhiteSpace(solution))
            {
                Console.Error.WriteLine("--solution is required.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            var serviceProvider = CommandRuntime.ServiceProvider
                ?? throw new InvalidOperationException("Service provider not initialized");

            var handler = serviceProvider.GetRequiredService<ICommandHandler<SystemWebCatalogOptions>>();
            var options = new SystemWebCatalogOptions
            {
                SolutionPath = solution,
                OutputPath = output,
                Formats = format,
                Verbosity = verbosity.ToLowerInvariant() switch
                {
                    "quiet" => VerbosityLevel.Quiet,
                    "diag" => VerbosityLevel.Diag,
                    _ => VerbosityLevel.Normal
                }
            };

            var exitCode = await handler.ExecuteAsync(options, CancellationToken.None);
            Environment.ExitCode = exitCode;
        }, solutionOption, outputOption, formatOption, verbosityOption);

        systemweb.AddCommand(catalog);
        root.AddCommand(systemweb);
    }
}
