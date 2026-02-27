using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class UsingSystemWebCatalogAnalyzer : ICatalogAnalyzer
{
    public string Id => "SW0001";

    public async Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct)
    {
        foreach (var document in project.Documents)
        {
            if (document.SourceCodeKind != SourceCodeKind.Regular)
            {
                continue;
            }

            var syntaxRoot = await document.GetSyntaxRootAsync(ct);
            if (syntaxRoot is null)
            {
                continue;
            }

            foreach (var usingDirective in syntaxRoot.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                var ns = usingDirective.Name?.ToString();
                if (string.IsNullOrWhiteSpace(ns))
                {
                    continue;
                }

                if (!ns.Equals("System.Web", StringComparison.Ordinal) &&
                    !ns.StartsWith("System.Web.", StringComparison.Ordinal))
                {
                    continue;
                }

                var (line, column) = CatalogAccumulator.GetLineAndColumn(usingDirective.GetLocation());
                acc.Add(
                    id: Id,
                    category: "Namespace",
                    severity: "Info",
                    message: $"Using directive imports {ns}",
                    filePath: document.FilePath ?? document.Name,
                    line: line,
                    column: column,
                    symbol: ns,
                    snippet: usingDirective.ToString().Trim());
            }
        }
    }
}
