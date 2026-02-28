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

                if (symbol is null)
                {
                    continue;
                }

                if (!RoslynSymbolHelpers.IsSystemWebSymbol(symbol))
                {
                    continue;
                }

                if (RoslynSymbolHelpers.IsNoiseSymbolKind(symbol))
                {
                    continue;
                }

                var symbolName = FormatSymbol(symbol);
                if (string.IsNullOrWhiteSpace(symbolName))
                {
                    continue;
                }

                var containingType = symbol.ContainingType is null
                    ? null
                    : TrimGlobalPrefix(symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MemberKind"] = symbol.Kind.ToString()
                };

                if (!string.IsNullOrWhiteSpace(containingType))
                {
                    properties["ContainingType"] = containingType;
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
                    snippet: node.ToString().Trim(),
                    properties: properties);
            }
        }
    }

    private static string? FormatSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        if (symbol is INamedTypeSymbol namedType)
        {
            return namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        if (symbol is IMethodSymbol method)
        {
            var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (string.IsNullOrWhiteSpace(containingType))
            {
                return method.Name;
            }

            var parameterTypes = string.Join(", ", method.Parameters
                .Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return $"{containingType}.{method.Name}({parameterTypes})";
        }

        var containing = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (string.IsNullOrWhiteSpace(containing))
        {
            return symbol.Name;
        }

        return $"{containing}.{symbol.Name}";
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }
}
