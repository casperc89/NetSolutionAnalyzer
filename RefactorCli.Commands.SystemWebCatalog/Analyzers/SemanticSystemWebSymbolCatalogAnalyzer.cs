using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class SemanticSystemWebSymbolCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly Type[] CandidateNodeTypes =
    [
        typeof(QualifiedNameSyntax),
        typeof(MemberAccessExpressionSyntax),
        typeof(ObjectCreationExpressionSyntax),
        typeof(InvocationExpressionSyntax),
        typeof(AttributeSyntax),
        typeof(BaseTypeSyntax)
    ];

    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0002",
        Title = "Semantic System.Web symbol references",
        Category = "Member",
        Severity = "Info",
        WhatItDetects = "Semantic references to System.Web types and members in C# code.",
        WhyItMatters = "Captures true symbol usage even when aliases or qualified names hide plain-text patterns."
    };

    public CatalogRuleDescriptor Descriptor => Rule;

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

                if (symbol is IAliasSymbol alias)
                {
                    symbol = alias.Target;
                }

                if (symbol is null && node is ExpressionSyntax expression)
                {
                    symbol = semanticModel.GetTypeInfo(expression, ct).Type;
                }

                if (!RoslynSymbolHelpers.IsSystemWebSymbol(symbol))
                {
                    continue;
                }

                if (RoslynSymbolHelpers.IsNoiseSymbolKind(symbol))
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
                    id: Rule.Id,
                    category: Rule.Category,
                    severity: Rule.Severity,
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
