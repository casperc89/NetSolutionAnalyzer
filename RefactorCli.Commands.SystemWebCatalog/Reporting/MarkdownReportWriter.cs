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
            .OrderBy(x => x.Rule, StringComparer.Ordinal)
            .ToList();

        var symbols = allFindings
            .Where(f => !string.IsNullOrWhiteSpace(f.Symbol))
            .GroupBy(f => f.Symbol!)
            .Select(g => (Symbol: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Symbol, StringComparer.Ordinal)
            .Take(20);
        var sessionKeys = allFindings
            .Select(f =>
            {
                if (f.Properties is not null &&
                    f.Properties.TryGetValue("sessionKey", out var keyValue) &&
                    !string.IsNullOrWhiteSpace(keyValue))
                {
                    return keyValue;
                }

                return null;
            })
            .Where(key => key is not null)
            .Select(key => key!)
            .GroupBy(key => key)
            .Select(g => (Key: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(20)
            .ToList();
        var cookieKeys = allFindings
            .Select(f =>
            {
                if (f.Properties is not null &&
                    f.Properties.TryGetValue("cookieKey", out var keyValue) &&
                    !string.IsNullOrWhiteSpace(keyValue))
                {
                    return keyValue;
                }

                return null;
            })
            .Where(key => key is not null)
            .Select(key => key!)
            .GroupBy(key => key)
            .Select(g => (Key: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(20)
            .ToList();

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
        builder.AppendLine("## Findings by Project");
        builder.AppendLine();
        builder.AppendLine("| Project | Findings | Documents Analyzed |");
        builder.AppendLine("|---|---:|---:|");
        var orderedProjects = report.Projects.OrderBy(p => p.ProjectName, StringComparer.Ordinal).ToList();
        foreach (var project in orderedProjects)
        {
            builder.AppendLine($"| {project.ProjectName} | {project.Findings.Count} | {project.DocumentsAnalyzed} |");
        }

        builder.AppendLine();
        builder.AppendLine("### Findings by Rule per Project");
        builder.AppendLine();
        foreach (var project in orderedProjects)
        {
            if (project.Findings.Count == 0)
                continue;
            
            builder.AppendLine($"#### {project.ProjectName}");
            builder.AppendLine();

            var projectFindingsByRule = project.Findings
                .GroupBy(f => f.Id)
                .Select(g => (Rule: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Rule, StringComparer.Ordinal)
                .ToList();

            builder.AppendLine("| Rule | Count |");
            builder.AppendLine("|---|---:|");
            foreach (var findingByRule in projectFindingsByRule)
            {
                builder.AppendLine($"| {findingByRule.Rule} | {findingByRule.Count} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine("## Top Symbols");
        builder.AppendLine();
        foreach (var symbol in symbols)
        {
            builder.AppendLine($"- `{symbol.Symbol}` ({symbol.Count})");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Session Keys");
        builder.AppendLine();
        if (sessionKeys.Count == 0)
        {
            builder.AppendLine("_No session key findings captured._");
        }
        else
        {
            foreach (var key in sessionKeys)
            {
                builder.AppendLine($"- `{key.Key}` ({key.Count})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Top Cookie Keys");
        builder.AppendLine();
        if (cookieKeys.Count == 0)
        {
            builder.AppendLine("_No cookie key findings captured._");
        }
        else
        {
            foreach (var key in cookieKeys)
            {
                builder.AppendLine($"- `{key.Key}` ({key.Count})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## File Hotspots by Project");
        builder.AppendLine();
        var projectsWithHotspots = orderedProjects
            .Where(p => p.Findings.Count > 0)
            .ToList();

        foreach (var project in projectsWithHotspots)
        {
            builder.AppendLine($"### {project.ProjectName}");
            builder.AppendLine();

            var fileHotspots = project.Findings
                .GroupBy(f => f.FilePath)
                .Select(g =>
                {
                    var ruleBreakdown = g
                        .GroupBy(f => f.Id)
                        .Select(rg => (Rule: rg.Key, Count: rg.Count()))
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Rule, StringComparer.Ordinal)
                        .Select(x => $"{x.Rule} ({x.Count})");

                    var topSymbols = g
                        .Where(f => !string.IsNullOrWhiteSpace(f.Symbol))
                        .GroupBy(f => f.Symbol!)
                        .Select(sg => (Symbol: sg.Key, Count: sg.Count()))
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Symbol, StringComparer.Ordinal)
                        .Take(3)
                        .Select(x => $"{x.Symbol} ({x.Count})");

                    return (
                        File: g.Key,
                        Total: g.Count(),
                        RuleBreakdown: string.Join(", ", ruleBreakdown),
                        TopSymbols: string.Join(", ", topSymbols));
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.File, StringComparer.Ordinal)
                .Take(15)
                .ToList();

            builder.AppendLine("| File | Total | Rule Breakdown | Top Symbols |");
            builder.AppendLine("|---|---:|---|---|");
            if (fileHotspots.Count == 0)
            {
                builder.AppendLine("| _None_ | 0 | - | - |");
            }
            else
            {
                foreach (var hotspot in fileHotspots)
                {
                    var ruleBreakdown = string.IsNullOrWhiteSpace(hotspot.RuleBreakdown) ? "-" : hotspot.RuleBreakdown;
                    var topSymbolsText = string.IsNullOrWhiteSpace(hotspot.TopSymbols) ? "-" : hotspot.TopSymbols;
                    builder.AppendLine($"| `{hotspot.File}` | {hotspot.Total} | {EscapeMarkdownTableCell(ruleBreakdown)} | {EscapeMarkdownTableCell(topSymbolsText)} |");
                }
            }

            builder.AppendLine();
        }

        if (projectsWithHotspots.Count == 0)
        {
            builder.AppendLine("_No project hotspots found._");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
