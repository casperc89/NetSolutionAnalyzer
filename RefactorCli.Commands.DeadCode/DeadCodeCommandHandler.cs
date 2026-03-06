using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode.Analysis;
using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Commands.DeadCode.Contracts;

namespace RefactorCli.Commands.DeadCode;

public sealed class DeadCodeCommandHandler : ICommandHandler<DeadCodeOptions>
{
    private readonly DeadCodeService _service;
    private readonly DeadCodeConfigLoader _configLoader;
    private readonly IEnumerable<IReportWriter> _writers;
    private readonly IAppConsole _console;
    private readonly ILogger<DeadCodeCommandHandler> _logger;

    public DeadCodeCommandHandler(
        DeadCodeService service,
        DeadCodeConfigLoader configLoader,
        IEnumerable<IReportWriter> writers,
        IAppConsole console,
        ILogger<DeadCodeCommandHandler> logger)
    {
        _service = service;
        _configLoader = configLoader;
        _writers = writers;
        _console = console;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(DeadCodeOptions options, CancellationToken ct)
    {
        try
        {
            _console.Info($"Loading solution: {options.SolutionPath}");
            var config = await _configLoader.LoadAsync(options.ConfigPath, ct);
            var report = await _service.AnalyzeAsync(options, config, ct);

            var selectedFormats = options.Formats
                .Select(f => f.Trim().ToLowerInvariant())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            var allowedFormats = new HashSet<string>(["json", "md"], StringComparer.Ordinal);
            var invalidFormats = selectedFormats.Where(f => !allowedFormats.Contains(f)).ToList();
            if (invalidFormats.Count > 0)
            {
                throw new InvalidCommandOptionsException($"Unsupported --format value(s): {string.Join(", ", invalidFormats)}. Allowed: json|md");
            }

            var reportPaths = new List<string>();
            foreach (var writer in _writers.OrderBy(w => w.Format, StringComparer.Ordinal))
            {
                if (!selectedFormats.Contains(writer.Format))
                {
                    continue;
                }

                var path = await writer.WriteAsync(report, options.OutputPath, ct);
                reportPaths.Add(path);
            }

            _console.Info($"Projects analyzed: {report.ProjectsAnalyzed}");
            _console.Info($"Findings: {report.Findings.Count}");
            _console.Info($"Definitely dead: {report.Findings.Count(f => f.Confidence == DeadCodeConfidence.DefinitelyDead)}");
            _console.Info($"Likely dead: {report.Findings.Count(f => f.Confidence == DeadCodeConfidence.LikelyDead)}");
            _console.Info($"Unknown: {report.Findings.Count(f => f.Confidence == DeadCodeConfidence.Unknown)}");
            _console.Info($"Candidates: {report.Diagnostics.CandidateSymbols}");
            _console.Info($"Roots: {report.Diagnostics.RootSymbols}");
            _console.Info($"Projects with dynamic patterns: {report.Diagnostics.ProjectsWithDynamicPatterns}");
            _console.Info(
                $"Timing (ms): total={report.Diagnostics.Timing.TotalMs}, collectCandidates={report.Diagnostics.Timing.CollectCandidatesMs}, collectRoots={report.Diagnostics.Timing.CollectRootsMs}, buildReferenceIndex={report.Diagnostics.Timing.BuildReferenceIndexMs}, collectDynamicPatterns={report.Diagnostics.Timing.CollectDynamicPatternsMs}, classify={report.Diagnostics.Timing.ClassifyFindingsMs}");

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
            _logger.LogError(ex, "Unexpected failure while running dead code analysis");
            _console.Error(ex.Message);
            return ExitCodes.UnexpectedError;
        }
    }
}
