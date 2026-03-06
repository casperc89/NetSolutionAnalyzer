using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Commands.MethodDependency.Analysis;
using RefactorCli.Commands.MethodDependency.Contracts;

namespace RefactorCli.Commands.MethodDependency;

public sealed class MethodDependencyCommandHandler : ICommandHandler<MethodDependencyOptions>
{
    private readonly MethodDependencyService _service;
    private readonly IEnumerable<IReportWriter> _writers;
    private readonly IAppConsole _console;
    private readonly ILogger<MethodDependencyCommandHandler> _logger;

    public MethodDependencyCommandHandler(
        MethodDependencyService service,
        IEnumerable<IReportWriter> writers,
        IAppConsole console,
        ILogger<MethodDependencyCommandHandler> logger)
    {
        _service = service;
        _writers = writers;
        _console = console;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(MethodDependencyOptions options, CancellationToken ct)
    {
        try
        {
            _console.Info($"Loading solution: {options.SolutionPath}");
            var report = await _service.AnalyzeAsync(options, ct);

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

                var path = await writer.WriteAsync(report, options.OutputPath, ct);
                reportPaths.Add(path);
            }

            _console.Info($"Root method: {report.RootMethod.DisplayName}");
            _console.Info($"Methods discovered: {report.Methods.Count}");
            _console.Info($"Call edges: {report.Edges.Count}");

            if (report.UnresolvedCallSites.Count > 0)
            {
                _console.Error($"Unresolved call sites: {report.UnresolvedCallSites.Count}");
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
            _logger.LogError(ex, "Unexpected failure while running method dependency analysis");
            _console.Error(ex.Message);
            return ExitCodes.UnexpectedError;
        }
    }
}
