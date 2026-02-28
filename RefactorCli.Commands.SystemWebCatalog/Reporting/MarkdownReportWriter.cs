using System.Text;
using RefactorCli.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Reporting;

public sealed class MarkdownReportWriter : IReportWriter
{
    private readonly IFileSystem _fileSystem;

    public MarkdownReportWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string Format => "md";

    public async Task<string> WriteAsync(CatalogReport report, string outputDir, CancellationToken ct)
    {
        _fileSystem.EnsureDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "systemweb-catalog.md");
        var markdown = BuildMarkdown(report);
        await _fileSystem.WriteAllTextAsync(outputPath, markdown, ct);
        return outputPath;
    }

    private static string BuildMarkdown(CatalogReport report)
    {
        var allFindings = report.Projects.SelectMany(p => p.Findings).ToList();
        var findingsByRule = allFindings
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var describedRules = report.Rules
            .Select(rule => (
                Rule: rule.Id,
                Count: findingsByRule.TryGetValue(rule.Id, out var ruleFindings) ? ruleFindings.Count : 0,
                Category: rule.Category,
                Severity: rule.Severity,
                WhatItDetects: rule.WhatItDetects,
                WhyItMatters: rule.WhyItMatters))
            .ToList();

        var fallbackRules = findingsByRule
            .Where(x => report.Rules.All(r => !r.Id.Equals(x.Key, StringComparison.Ordinal)))
            .Select(x =>
            {
                var first = x.Value[0];
                return (
                    Rule: x.Key,
                    Count: x.Value.Count,
                    Category: first.Category,
                    Severity: first.Severity,
                    WhatItDetects: $"See finding message patterns, for example: {first.Message}",
                    WhyItMatters: "This rule appeared in findings but no analyzer descriptor was available.");
            });

        var byRule = describedRules
            .Concat(fallbackRules)
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Rule, StringComparer.Ordinal)
            .ToList();

        var hotspots = allFindings
            .GroupBy(f => f.FilePath)
            .Select(g => (File: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.File, StringComparer.Ordinal)
            .Take(15);

        var symbols = allFindings
            .Where(f => !string.IsNullOrWhiteSpace(f.Symbol))
            .GroupBy(f => f.Symbol!)
            .Select(g => (Symbol: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Symbol, StringComparer.Ordinal)
            .Take(20);

        var builder = new StringBuilder();
        builder.AppendLine("# System.Web Catalog Report");
        builder.AppendLine();
        builder.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:O}");
        builder.AppendLine($"- Solution: `{report.SolutionPath}`");
        builder.AppendLine($"- Projects analyzed: {report.Projects.Count}");
        builder.AppendLine($"- Total findings: {allFindings.Count}");
        builder.AppendLine();
        builder.AppendLine("## Rule Explanations");
        builder.AppendLine();
        builder.AppendLine("| Rule | What it detects | Why it matters | Category | Severity | Findings |");
        builder.AppendLine("|---|---|---|---|---|---:|");
        foreach (var rule in byRule)
        {
            builder.AppendLine($"| {rule.Rule} | {rule.WhatItDetects} | {rule.WhyItMatters} | {rule.Category} | {rule.Severity} | {rule.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Findings by Rule");
        builder.AppendLine();
        builder.AppendLine("| Rule | What it detects | Count |");
        builder.AppendLine("|---|---|---:|");
        foreach (var rule in byRule)
        {
            builder.AppendLine($"| {rule.Rule} | {rule.WhatItDetects} | {rule.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Findings by Project");
        builder.AppendLine();
        builder.AppendLine("| Project | Findings | Documents Analyzed |");
        builder.AppendLine("|---|---:|---:|");
        foreach (var project in report.Projects.OrderBy(p => p.ProjectName, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {project.ProjectName} | {project.Findings.Count} | {project.DocumentsAnalyzed} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Symbols");
        builder.AppendLine();
        foreach (var symbol in symbols)
        {
            builder.AppendLine($"- `{symbol.Symbol}` ({symbol.Count})");
        }

        builder.AppendLine();
        builder.AppendLine("## File Hotspots");
        builder.AppendLine();
        foreach (var hotspot in hotspots)
        {
            builder.AppendLine($"- `{hotspot.File}` ({hotspot.Count})");
        }

        return builder.ToString();
    }
}
