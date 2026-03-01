using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DependencyGraph.Analysis;
using RefactorCli.Commands.DependencyGraph.Contracts;
using RefactorCli.Commands.DependencyGraph.Reporting;
using System.CommandLine;

namespace RefactorCli.Commands.DependencyGraph;

public sealed class DependencyGraphCommandModule : ICommandModule
{
    public string Name => "dependency";

    public void Register(Command root, IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<DependencyGraphOptions>, DependencyGraphCommandHandler>();
        services.AddSingleton<IReportWriter, JsonReportWriter>();
        services.AddSingleton<IReportWriter, MarkdownReportWriter>();
        services.AddDependencyGraphAnalysis();

        var solutionOption = new Option<string>("--solution", "Path to a .sln file");
        var outputOption = new Option<string>("--output", () => "./refactor-reports", "Output directory");
        var formatOption = new Option<string[]>(
            "--format",
            () => ["json", "md"],
            "Output formats: json|md");
        formatOption.AllowMultipleArgumentsPerToken = true;

        var maxClassesOption = new Option<int>("--max-classes-per-project", () => 50, "Maximum class entries listed per project in markdown reports");
        var verbosityOption = new Option<string>("--verbosity", () => "normal", "quiet|normal|diag");

        var dependency = new Command("dependency", "Dependency analysis");
        var graph = new Command("graph", "Build project dependency graph and upgrade order");
        graph.AddOption(solutionOption);
        graph.AddOption(outputOption);
        graph.AddOption(formatOption);
        graph.AddOption(maxClassesOption);
        graph.AddOption(verbosityOption);

        graph.SetHandler(async (string? solution, string output, string[] format, int maxClassesPerProject, string verbosity) =>
        {
            if (string.IsNullOrWhiteSpace(solution))
            {
                Console.Error.WriteLine("--solution is required.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            var serviceProvider = CommandRuntime.ServiceProvider
                ?? throw new InvalidOperationException("Service provider not initialized");

            var handler = serviceProvider.GetRequiredService<ICommandHandler<DependencyGraphOptions>>();
            var options = new DependencyGraphOptions
            {
                SolutionPath = solution,
                OutputPath = output,
                Formats = format,
                MaxClassesPerProject = maxClassesPerProject,
                Verbosity = verbosity.ToLowerInvariant() switch
                {
                    "quiet" => VerbosityLevel.Quiet,
                    "diag" => VerbosityLevel.Diag,
                    _ => VerbosityLevel.Normal
                }
            };

            var exitCode = await handler.ExecuteAsync(options, CancellationToken.None);
            Environment.ExitCode = exitCode;
        }, solutionOption, outputOption, formatOption, maxClassesOption, verbosityOption);

        dependency.AddCommand(graph);
        root.AddCommand(dependency);
    }
}
