using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode.Analysis;
using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Commands.DeadCode.Contracts;
using RefactorCli.Commands.DeadCode.Reporting;
using System.CommandLine;

namespace RefactorCli.Commands.DeadCode;

public sealed class DeadCodeCommandModule : ICommandModule
{
    public string Name => "deadcode";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<DeadCodeOptions>, DeadCodeCommandHandler>();
        services.AddSingleton<IReportWriter, JsonReportWriter>();
        services.AddSingleton<IReportWriter, MarkdownReportWriter>();
        services.AddSingleton<DeadCodeConfigLoader>();
        services.AddDeadCodeAnalysis();
    }

    public void RegisterCommands(Command root, IServiceProvider serviceProvider)
    {
        var solutionOption = new Option<string>("--solution", "Path to a .sln file");
        var outputOption = new Option<string>("--output", () => "./refactor-reports", "Output directory");
        var formatOption = new Option<string[]>(
            "--format",
            () => ["json"],
            "Output formats: json|md");
        formatOption.AllowMultipleArgumentsPerToken = true;

        var excludeTestProjectsOption = new Option<bool>("--exclude-test-projects", () => true, "Exclude test projects");
        var minConfidenceOption = new Option<string>("--min-confidence", () => "definitely_dead", "definitely_dead|likely_dead|unknown");
        var configOption = new Option<string?>("--config", "Path to deadcode.yml config");

        var command = new Command("deadcode", "Dead code analysis");
        command.AddOption(solutionOption);
        command.AddOption(outputOption);
        command.AddOption(formatOption);
        command.AddOption(excludeTestProjectsOption);
        command.AddOption(minConfidenceOption);
        command.AddOption(configOption);

        command.SetHandler(async (string? solution, string output, string[] format, bool excludeTestProjects, string minConfidence, string? configPath) =>
        {
            if (string.IsNullOrWhiteSpace(solution))
            {
                Console.Error.WriteLine("--solution is required.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            DeadCodeConfidence parsedConfidence;
            try
            {
                parsedConfidence = ParseConfidence(minConfidence);
            }
            catch (InvalidCommandOptionsException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            var handler = serviceProvider.GetRequiredService<ICommandHandler<DeadCodeOptions>>();
            var options = new DeadCodeOptions
            {
                SolutionPath = solution,
                OutputPath = output,
                Formats = format,
                ExcludeTestProjects = excludeTestProjects,
                MinConfidence = parsedConfidence,
                ConfigPath = configPath
            };

            var exitCode = await handler.ExecuteAsync(options, CancellationToken.None);
            Environment.ExitCode = exitCode;
        }, solutionOption, outputOption, formatOption, excludeTestProjectsOption, minConfidenceOption, configOption);

        root.AddCommand(command);
    }

    private static DeadCodeConfidence ParseConfidence(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "definitely_dead" => DeadCodeConfidence.DefinitelyDead,
            "likely_dead" => DeadCodeConfidence.LikelyDead,
            "unknown" => DeadCodeConfidence.Unknown,
            _ => throw new InvalidCommandOptionsException($"Invalid --min-confidence value '{value}'. Allowed: definitely_dead|likely_dead|unknown")
        };
    }
}
