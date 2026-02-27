using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class SystemWebBaseTypeCatalogAnalyzer : ICatalogAnalyzer
{
    public string Id => "SW0003";

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

            var semanticModel = await document.GetSemanticModelAsync(ct);
            if (semanticModel is null)
            {
                continue;
            }

            foreach (var classDecl in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.BaseList is null)
                {
                    continue;
                }

                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var symbol = semanticModel.GetSymbolInfo(baseType.Type, ct).Symbol as ITypeSymbol;
                    if (!RoslynSymbolHelpers.IsSystemWebSymbol(symbol))
                    {
                        continue;
                    }

                    var fqSymbol = symbol!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var (line, column) = CatalogAccumulator.GetLineAndColumn(baseType.GetLocation());
                    acc.Add(
                        id: Id,
                        category: "Type",
                        severity: "Warning",
                        message: $"Type derives/implements {fqSymbol}",
                        filePath: document.FilePath ?? document.Name,
                        line: line,
                        column: column,
                        symbol: fqSymbol,
                        snippet: baseType.ToString().Trim());
                }
            }
        }
    }
}
