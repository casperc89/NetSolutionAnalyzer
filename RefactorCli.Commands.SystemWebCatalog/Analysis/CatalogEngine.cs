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

    public async Task<CatalogReport> AnalyzeAsync(
        Solution solution,
        string solutionPath,
        IReadOnlyList<string> includedRules,
        CancellationToken ct)
        => await AnalyzeAsync(solution, solutionPath, includedRules, excludeTestProjects: false, ct);

    public async Task<CatalogReport> AnalyzeAsync(
        Solution solution,
        string solutionPath,
        IReadOnlyList<string> includedRules,
        bool excludeTestProjects,
        CancellationToken ct)
    {
        var selectedRules = includedRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => rule.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<ICatalogAnalyzer> activeAnalyzers;
        if (selectedRules.Count == 0)
        {
            activeAnalyzers = _analyzers;
        }
        else
        {
            var analyzerIds = _analyzers
                .Select(a => a.Descriptor.Id)
                .ToHashSet(StringComparer.Ordinal);

            var unknownRules = selectedRules
                .Where(rule => !analyzerIds.Contains(rule))
                .OrderBy(rule => rule, StringComparer.Ordinal)
                .ToList();

            if (unknownRules.Count > 0)
            {
                var availableRules = string.Join(", ", analyzerIds.OrderBy(rule => rule, StringComparer.Ordinal));
                throw new InvalidCommandOptionsException(
                    $"Unknown rule ID(s): {string.Join(", ", unknownRules)}. Available rule IDs: {availableRules}.");
            }

            activeAnalyzers = _analyzers
                .Where(analyzer => selectedRules.Contains(analyzer.Descriptor.Id))
                .OrderBy(analyzer => analyzer.Descriptor.Id, StringComparer.Ordinal)
                .ToList();
        }

        var projectReports = new List<ProjectReport>();

        var projects = solution.Projects
            .Where(project => !excludeTestProjects || !project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Name, StringComparer.Ordinal);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("Analyzing project: {ProjectName}", project.Name);

            var acc = new CatalogAccumulator();

            foreach (var analyzer in activeAnalyzers)
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
            Rules = activeAnalyzers
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
