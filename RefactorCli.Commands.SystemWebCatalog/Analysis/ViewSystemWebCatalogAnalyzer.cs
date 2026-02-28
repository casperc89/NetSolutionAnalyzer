using Microsoft.CodeAnalysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class ViewSystemWebCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly string[] Patterns =
    [
        "System.Web",
        "HttpContext",
        "Request.",
        "Response.",
        "Server."
    ];

    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0005",
        Title = "Razor view System.Web heuristics",
        Category = "View",
        Severity = "Info",
        WhatItDetects = "Known text patterns in .cshtml files that suggest System.Web-bound view/runtime usage.",
        WhyItMatters = "View-level dependencies help estimate Razor migration scope beyond compiled C# symbols."
    };

    public CatalogRuleDescriptor Descriptor => Rule;

    public Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
    {
        var projectDir = GetProjectDirectory(project);
        if (projectDir is null)
        {
            return Task.CompletedTask;
        }

        foreach (var path in Directory.EnumerateFiles(projectDir, "*.cshtml", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            foreach (var pattern in Patterns)
            {
                if (!content.Contains(pattern, StringComparison.Ordinal))
                {
                    continue;
                }

                acc.Add(
                    id: Rule.Id,
                    category: Rule.Category,
                    severity: Rule.Severity,
                    message: $"View contains heuristic System.Web usage: '{pattern}'",
                    filePath: path,
                    symbol: pattern);
            }
        }

        return Task.CompletedTask;
    }

    private static string? GetProjectDirectory(Project project)
    {
        var filePath = project.FilePath;
        return filePath is null ? null : Path.GetDirectoryName(filePath);
    }
}
