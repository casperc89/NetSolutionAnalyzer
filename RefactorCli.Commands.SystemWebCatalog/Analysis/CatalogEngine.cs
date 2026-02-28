using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RefactorCli.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Analyzers;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class CatalogEngine
{
    private readonly IReadOnlyList<ICatalogAnalyzer> _analyzers;
    private readonly ILogger<CatalogEngine> _logger;

    public CatalogEngine(IEnumerable<ICatalogAnalyzer> analyzers, ILogger<CatalogEngine> logger)
    {
        _analyzers = analyzers.OrderBy(a => a.Descriptor.Id, StringComparer.Ordinal).ToList();
        _logger = logger;
    }

    public async Task<CatalogReport> AnalyzeAsync(Solution solution, string solutionPath, CancellationToken ct)
    {
        var projectReports = new List<ProjectReport>();

        foreach (var project in solution.Projects.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("Analyzing project: {ProjectName}", project.Name);

            var acc = new CatalogAccumulator();

            foreach (var analyzer in _analyzers)
            {
                try
                {
                    await analyzer.AnalyzeAsync(project, acc, ct);
                }
                catch
                {
                    // Keep producing partial results even when one analyzer/document fails.
                }
            }

            var orderedFindings = acc.Findings
                .OrderBy(f => f.Id, StringComparer.Ordinal)
                .ThenBy(f => f.FilePath, StringComparer.Ordinal)
                .ThenBy(f => f.Line ?? int.MaxValue)
                .ThenBy(f => f.Column ?? int.MaxValue)
                .ThenBy(f => f.Symbol, StringComparer.Ordinal)
                .ThenBy(f => f.Message, StringComparer.Ordinal)
                .ToList();

            projectReports.Add(new ProjectReport
            {
                ProjectName = project.Name,
                ProjectPath = project.FilePath,
                TargetFramework = project.ParseOptions?.PreprocessorSymbolNames
                    .FirstOrDefault(p => p.StartsWith("NET", StringComparison.Ordinal)),
                DocumentsAnalyzed = project.Documents.Count(d => d.SourceCodeKind == SourceCodeKind.Regular),
                Findings = orderedFindings
            });
        }

        return new CatalogReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = solutionPath,
            Rules = _analyzers
                .Select(a => a.Descriptor)
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .ToList(),
            Projects = projectReports
                .OrderBy(p => p.ProjectName, StringComparer.Ordinal)
                .ThenBy(p => p.ProjectPath, StringComparer.Ordinal)
                .ToList()
        };
    }
}
