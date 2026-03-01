using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DependencyGraph.Analysis;
using RefactorCli.Commands.DependencyGraph.Contracts;

namespace RefactorCli.Commands.DependencyGraph;

public sealed class DependencyGraphCommandHandler : ICommandHandler<DependencyGraphOptions>
{
    private readonly DependencyGraphService _graphService;
    private readonly IEnumerable<IReportWriter> _writers;
    private readonly IAppConsole _console;
    private readonly ILogger<DependencyGraphCommandHandler> _logger;

    public DependencyGraphCommandHandler(
        DependencyGraphService graphService,
        IEnumerable<IReportWriter> writers,
        IAppConsole console,
        ILogger<DependencyGraphCommandHandler> logger)
    {
        _graphService = graphService;
        _writers = writers;
        _console = console;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(DependencyGraphOptions options, CancellationToken ct)
    {
        try
        {
            _console.Info($"Loading solution: {options.SolutionPath}");
            var report = await _graphService.AnalyzeAsync(options, ct);

            var selectedFormats = options.Formats
                .Select(f => f.Trim().ToLowerInvariant())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            var reportPaths = new List<string>();
            foreach (var writer in _writers.OrderBy(w => w.Format, StringComparer.Ordinal))
            {
                if (!selectedFormats.Contains(writer.Format))
                {
                    continue;
                }

                var path = await writer.WriteAsync(report, options.OutputPath, options.MaxClassesPerProject, ct);
                reportPaths.Add(path);
            }

            _console.Info($"Projects analyzed: {report.Projects.Count}");
            _console.Info($"Dependency edges: {report.Edges.Count}");
            if (report.Cycles.Count > 0)
            {
                _console.Error($"Cycles detected: {report.Cycles.Count}");
            }

            var top = report.Projects
                .OrderByDescending(p => p.UniqueTransitiveUpstreamClassCount)
                .ThenBy(p => p.ProjectName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (top is not null)
            {
                _console.Info($"Top coupling hotspot: {top.ProjectName} ({top.UniqueTransitiveUpstreamClassCount} upstream classes)");
            }

            foreach (var reportPath in reportPaths)
            {
                _console.Info($"Report: {reportPath}");
            }

            return ExitCodes.Success;
        }
        catch (InvalidCommandOptionsException ex)
        {
            _console.Error(ex.Message);
            return ExitCodes.InvalidArguments;
        }
        catch (SolutionLoadException ex)
        {
            _console.Error(ex.Message);
            return ExitCodes.SolutionLoadFailed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure while running dependency graph");
            _console.Error(ex.Message);
            return ExitCodes.UnexpectedError;
        }
    }
}
