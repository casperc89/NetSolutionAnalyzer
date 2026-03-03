using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Reporting;
using System.CommandLine;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Commands.SystemWebCatalog;

public sealed class SystemWebCatalogCommandModule : ICommandModule
{
    public string Name => "systemweb";

    public void Register(Command root, IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<SystemWebCatalogOptions>, SystemWebCatalogCommandHandler>();
        services.AddSingleton<IReportWriter, JsonReportWriter>();
        services.AddSingleton<IReportWriter, MarkdownReportWriter>();
        
        services.AddRoslynAnalysis();

        var solutionOption = new Option<string>("--solution", "Path to a .sln file");
        var outputOption = new Option<string>("--output", () => "./refactor-reports", "Output directory");
        var formatOption = new Option<string[]>(
            "--format",
            () => ["json", "md"],
            "Output formats: json|md");
        formatOption.AllowMultipleArgumentsPerToken = true;
        var includeRuleOption = new Option<string[]>(
            aliases: ["--include-rule", "--include-rules"],
            getDefaultValue: () => [],
            description: "Only run the specified rule IDs (for example: SW0001, SW0003).");
        includeRuleOption.AllowMultipleArgumentsPerToken = true;
        var excludeTestProjectsOption = new Option<bool>("--exclude-test-projects", "Exclude projects with names ending in '.Tests'");

        var verbosityOption = new Option<string>("--verbosity", () => "normal", "quiet|normal|diag");

        root.AddGlobalOption(solutionOption);
        root.AddGlobalOption(outputOption);
        root.AddGlobalOption(formatOption);
        root.AddGlobalOption(verbosityOption);

        var systemweb = new Command("systemweb", "System.Web analysis");
        var catalog = new Command("catalog", "Catalog System.Web usage");
        
        catalog.AddOption(includeRuleOption);
        catalog.AddOption(excludeTestProjectsOption);

        catalog.SetHandler(async (string? solution, string output, string[] format, string[] includeRules, bool excludeTestProjects, string verbosity) =>
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
            var normalizedIncludedRules = includeRules
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var options = new SystemWebCatalogOptions
            {
                SolutionPath = solution,
                OutputPath = output,
                Formats = format,
                IncludedRules = normalizedIncludedRules,
                ExcludeTestProjects = excludeTestProjects,
                Verbosity = verbosity.ToLowerInvariant() switch
                {
                    "quiet" => VerbosityLevel.Quiet,
                    "diag" => VerbosityLevel.Diag,
                    _ => VerbosityLevel.Normal
                }
            };

            var exitCode = await handler.ExecuteAsync(options, CancellationToken.None);
            Environment.ExitCode = exitCode;
        }, solutionOption, outputOption, formatOption, includeRuleOption, excludeTestProjectsOption, verbosityOption);

        systemweb.AddCommand(catalog);
        root.AddCommand(systemweb);
    }
}
