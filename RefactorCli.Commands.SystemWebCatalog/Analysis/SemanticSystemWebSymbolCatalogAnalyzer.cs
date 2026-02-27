using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class SemanticSystemWebSymbolCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly Type[] CandidateNodeTypes =
    [
        typeof(IdentifierNameSyntax),
        typeof(QualifiedNameSyntax),
        typeof(MemberAccessExpressionSyntax),
        typeof(ObjectCreationExpressionSyntax),
        typeof(InvocationExpressionSyntax),
        typeof(AttributeSyntax),
        typeof(BaseListSyntax)
    ];

    public string Id => "SW0002";

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

            var nodes = syntaxRoot.DescendantNodes().Where(n => CandidateNodeTypes.Contains(n.GetType()));
            foreach (var node in nodes)
            {
                ISymbol? symbol = null;
                var symbolInfo = semanticModel.GetSymbolInfo(node, ct);
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

                if (symbol is null && node is ExpressionSyntax expression)
                {
                    symbol = semanticModel.GetTypeInfo(expression, ct).Type;
                }

                if (!RoslynSymbolHelpers.IsSystemWebSymbol(symbol))
                {
                    continue;
                }

                var symbolName = symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (string.IsNullOrWhiteSpace(symbolName))
                {
                    continue;
                }
                var (line, column) = CatalogAccumulator.GetLineAndColumn(node.GetLocation());
                acc.Add(
                    id: Id,
                    category: "Member",
                    severity: "Info",
                    message: $"Semantic reference to {symbolName}",
                    filePath: document.FilePath ?? document.Name,
                    line: line,
                    column: column,
                    symbol: symbolName,
                    snippet: node.ToString().Trim());
            }
        }
    }
}
