using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class HttpContextCurrentCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly HashSet<string> TargetAmbientMembers = new(StringComparer.Ordinal)
    {
        "Items",
        "Request",
        "Response",
        "Server",
        "User",
        "Session"
    };

    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0100",
        Title = "HttpContext.Current ambient context access",
        Category = "Context",
        Severity = "Warning",
        WhatItDetects = "System.Web.HttpContext.Current and ambient chained usage like Current.Request/Response/Server/User/Items.",
        WhyItMatters = "Ambient static context often blocks migration and should move to IHttpContextAccessor or explicit request context passing."
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

            foreach (var memberAccess in syntaxRoot.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, ct);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (symbol is not IPropertySymbol property)
                {
                    continue;
                }

                if (IsHttpContextCurrentProperty(property))
                {
                    AddFinding(acc, document, memberAccess, "System.Web.HttpContext.Current");
                    continue;
                }

                if (IsHttpContextAmbientProperty(property) && HasHttpContextCurrentInChain(memberAccess.Expression, semanticModel, ct))
                {
                    AddFinding(acc, document, memberAccess, $"System.Web.HttpContext.Current.{property.Name}");
                }
            }
        }
    }

    private static void AddFinding(CatalogAccumulator acc, Document document, MemberAccessExpressionSyntax memberAccess, string symbol)
    {
        var (line, column) = CatalogAccumulator.GetLineAndColumn(memberAccess.GetLocation());
        acc.Add(
            id: Rule.Id,
            category: Rule.Category,
            severity: Rule.Severity,
            message: $"Ambient HttpContext access detected: {symbol}",
            filePath: document.FilePath ?? document.Name,
            line: line,
            column: column,
            symbol: symbol,
            snippet: memberAccess.ToString().Trim());
    }

    private static bool IsHttpContextCurrentProperty(IPropertySymbol property)
    {
        return property.Name.Equals("Current", StringComparison.Ordinal)
               && property.ContainingType.Name.Equals("HttpContext", StringComparison.Ordinal)
               && property.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool IsHttpContextAmbientProperty(IPropertySymbol property)
    {
        return TargetAmbientMembers.Contains(property.Name)
               && property.ContainingType.Name.Equals("HttpContext", StringComparison.Ordinal)
               && property.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool HasHttpContextCurrentInChain(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, ct);
            var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            if (symbol is IPropertySymbol property && IsHttpContextCurrentProperty(property))
            {
                return true;
            }

            return HasHttpContextCurrentInChain(memberAccess.Expression, semanticModel, ct);
        }

        return false;
    }
}
