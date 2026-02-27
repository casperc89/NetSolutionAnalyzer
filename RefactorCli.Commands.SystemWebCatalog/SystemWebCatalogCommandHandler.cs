using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Abstractions.SystemWebCatalog;

namespace RefactorCli.Commands.SystemWebCatalog;

public sealed class SystemWebCatalogCommandHandler : ICommandHandler<SystemWebCatalogOptions>
{
    private readonly ISystemWebCatalogService _catalogService;
    private readonly IEnumerable<IReportWriter> _writers;
    private readonly IAppConsole _console;
    private readonly ILogger<SystemWebCatalogCommandHandler> _logger;

    public SystemWebCatalogCommandHandler(
        ISystemWebCatalogService catalogService,
        IEnumerable<IReportWriter> writers,
        IAppConsole console,
        ILogger<SystemWebCatalogCommandHandler> logger)
    {
        _catalogService = catalogService;
        _writers = writers;
        _console = console;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(SystemWebCatalogOptions options, CancellationToken ct)
    {
        try
        {
            _console.Info($"Loading solution: {options.SolutionPath}");
            var report = await _catalogService.AnalyzeAsync(options, ct);

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

            var total = report.Projects.Sum(p => p.Findings.Count);
            var top = report.Projects
                .OrderByDescending(p => p.Findings.Count)
                .ThenBy(p => p.ProjectName, StringComparer.Ordinal)
                .FirstOrDefault();

            _console.Info($"Analysis complete. Total findings: {total}");
            if (top is not null)
            {
                _console.Info($"Project with most findings: {top.ProjectName} ({top.Findings.Count})");
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
            _logger.LogError(ex, "Unexpected failure while running systemweb catalog");
            _console.Error(ex.Message);
            return ExitCodes.UnexpectedError;
        }
    }
}
