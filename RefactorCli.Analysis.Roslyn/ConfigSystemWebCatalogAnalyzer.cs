using System.Xml.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace RefactorCli.Analysis.Roslyn;

public sealed class ConfigSystemWebCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly string[] TargetElements =
    [
        "system.web",
        "httpModules",
        "httpHandlers",
        "authentication",
        "membership",
        "roleManager",
        "compilation"
    ];

    public string Id => "SW0004";

    public Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
    {
        var projectDir = GetProjectDirectory(project);
        if (projectDir is null)
        {
            return Task.CompletedTask;
        }

        var configs = Directory.EnumerateFiles(projectDir, "*.config", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Equals("web.config", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Equals("app.config", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".config", StringComparison.OrdinalIgnoreCase));

        foreach (var configPath in configs)
        {
            ct.ThrowIfCancellationRequested();

            XDocument? doc = null;
            try
            {
                doc = XDocument.Load(configPath, LoadOptions.SetLineInfo);
            }
            catch
            {
                continue;
            }

            if (doc.Root is null)
            {
                continue;
            }

            foreach (var element in doc.Descendants())
            {
                var name = element.Name.LocalName;
                if (!TargetElements.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineInfo = (IXmlLineInfo)element;
                acc.Add(
                    id: Id,
                    category: "Config",
                    severity: "Info",
                    message: $"Config element <{name}> suggests classic System.Web configuration",
                    filePath: configPath,
                    line: lineInfo.HasLineInfo() ? lineInfo.LineNumber : null,
                    column: lineInfo.HasLineInfo() ? lineInfo.LinePosition : null,
                    symbol: $"<{name}>");
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
