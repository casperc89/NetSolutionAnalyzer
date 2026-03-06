using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using RefactorCli.Commands.MethodDependency.Analysis;
using RefactorCli.Commands.MethodDependency.Contracts;
using RefactorCli.Commands.MethodDependency.Reporting;
using System.CommandLine;

namespace RefactorCli.Commands.MethodDependency;

public sealed class MethodDependencyCommandModule : ICommandModule
{
    public string Name => "methoddependency";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<MethodDependencyOptions>, MethodDependencyCommandHandler>();
        services.AddSingleton<IReportWriter, JsonReportWriter>();
        services.AddSingleton<IReportWriter, MarkdownReportWriter>();
        services.AddMethodDependencyAnalysis();
    }

    public void RegisterCommands(Command root, IServiceProvider serviceProvider)
    {
        var solutionOption = new Option<string>("--solution", "Path to a .sln file");
        var fileOption = new Option<string>("--file", "Path to a C# file within the solution");
        var lineOption = new Option<int>("--line", "1-based line number within the target method");
        var outputOption = new Option<string>("--output", () => "./refactor-reports", "Output directory");
        var formatOption = new Option<string[]>(
            "--format",
            () => ["json", "md"],
            "Output formats: json|md");
        formatOption.AllowMultipleArgumentsPerToken = true;

        var verbosityOption = new Option<string>("--verbosity", () => "normal", "quiet|normal|diag");

        var dependency = root.Subcommands.FirstOrDefault(c => c.Name.Equals("dependency", StringComparison.Ordinal)) as Command
            ?? new Command("dependency", "Dependency analysis");

        var method = new Command("method", "Analyze direct and transitive method dependencies from a file+line location");
        method.AddOption(solutionOption);
        method.AddOption(fileOption);
        method.AddOption(lineOption);
        method.AddOption(outputOption);
        method.AddOption(formatOption);
        method.AddOption(verbosityOption);

        method.SetHandler(async (string? solution, string? file, int line, string output, string[] format, string verbosity) =>
        {
            if (string.IsNullOrWhiteSpace(solution))
            {
                Console.Error.WriteLine("--solution is required.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            if (string.IsNullOrWhiteSpace(file))
            {
                Console.Error.WriteLine("--file is required.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            if (line <= 0)
            {
                Console.Error.WriteLine("--line must be greater than zero.");
                Environment.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            var handler = serviceProvider.GetRequiredService<ICommandHandler<MethodDependencyOptions>>();
            var options = new MethodDependencyOptions
            {
                SolutionPath = solution,
                FilePath = file,
                Line = line,
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
        }, solutionOption, fileOption, lineOption, outputOption, formatOption, verbosityOption);

        dependency.AddCommand(method);
        if (!root.Subcommands.Any(c => c.Name.Equals("dependency", StringComparison.Ordinal)))
        {
            root.AddCommand(dependency);
        }
    }
}
