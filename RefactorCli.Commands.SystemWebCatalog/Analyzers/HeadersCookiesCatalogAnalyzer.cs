using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class HeadersCookiesCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0702",
        Title = "Legacy cookie APIs",
        Category = "Http",
        Severity = "Warning",
        WhatItDetects = "Request.Cookies, Response.Cookies, and HttpCookie usage.",
        WhyItMatters = "Cookie API and defaults (for example SameSite) differ in ASP.NET Core."
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

            foreach (var typeSyntax in syntaxRoot.DescendantNodes().OfType<TypeSyntax>())
            {
                var symbol = GetSymbol(semanticModel, typeSyntax, ct);
                if (symbol is not INamedTypeSymbol typeSymbol || !IsHttpCookie(typeSymbol))
                {
                    continue;
                }

                AddFinding(acc, document, typeSyntax.GetLocation(), "System.Web.HttpCookie", typeSyntax.ToString().Trim());
            }

            foreach (var memberAccess in syntaxRoot.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var symbol = GetSymbol(semanticModel, memberAccess, ct);
                if (symbol is not IPropertySymbol property || !IsCookiesProperty(property))
                {
                    continue;
                }

                var source = property.ContainingType.Name.Equals("HttpRequest", StringComparison.Ordinal)
                    ? "System.Web.HttpRequest.Cookies"
                    : "System.Web.HttpResponse.Cookies";
                AddFinding(acc, document, memberAccess.GetLocation(), source, memberAccess.ToString().Trim());
            }
        }
    }

    private static ISymbol? GetSymbol(SemanticModel semanticModel, SyntaxNode node, CancellationToken ct)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node, ct);
        return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
    }

    private static void AddFinding(CatalogAccumulator acc, Document document, Location location, string symbol, string snippet)
    {
        var (line, column) = CatalogAccumulator.GetLineAndColumn(location);
        acc.Add(
            id: Rule.Id,
            category: Rule.Category,
            severity: Rule.Severity,
            message: $"Legacy cookies API detected: {symbol}",
            filePath: document.FilePath ?? document.Name,
            line: line,
            column: column,
            symbol: symbol,
            snippet: snippet);
    }

    private static bool IsHttpCookie(INamedTypeSymbol symbol)
    {
        return symbol.Name.Equals("HttpCookie", StringComparison.Ordinal)
               && symbol.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool IsCookiesProperty(IPropertySymbol symbol)
    {
        if (!symbol.Name.Equals("Cookies", StringComparison.Ordinal))
        {
            return false;
        }

        if (symbol.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return symbol.ContainingType.Name.Equals("HttpRequest", StringComparison.Ordinal) ||
               symbol.ContainingType.Name.Equals("HttpResponse", StringComparison.Ordinal);
    }
}
