using Microsoft.CodeAnalysis;

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

    public string Id => "SW0006";

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
                    id: Id,
                    category: "View",
                    severity: "Info",
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
