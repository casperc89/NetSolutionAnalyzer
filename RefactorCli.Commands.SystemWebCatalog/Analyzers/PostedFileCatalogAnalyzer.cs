using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class PostedFileCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0500",
        Title = "Legacy posted file APIs",
        Category = "Request",
        Severity = "Warning",
        WhatItDetects = "HttpPostedFileBase, Request.Files, and InputStream usage.",
        WhyItMatters = "File upload migration moves to IFormFile and often needs request-size/config changes."
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
                if (symbol is not INamedTypeSymbol typeSymbol || !IsHttpPostedFileBase(typeSymbol))
                {
                    continue;
                }

                AddFinding(acc, document, typeSyntax.GetLocation(), "System.Web.HttpPostedFileBase", typeSyntax.ToString().Trim());
            }

            foreach (var memberAccess in syntaxRoot.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var symbol = GetSymbol(semanticModel, memberAccess, ct);
                if (symbol is not IPropertySymbol property)
                {
                    continue;
                }

                if (IsRequestFilesProperty(property))
                {
                    AddFinding(acc, document, memberAccess.GetLocation(), "System.Web.HttpRequest.Files", memberAccess.ToString().Trim());
                    continue;
                }

                if (IsPostedFileInputStreamProperty(property))
                {
                    AddFinding(acc, document, memberAccess.GetLocation(), "System.Web.HttpPostedFileBase.InputStream", memberAccess.ToString().Trim());
                }
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
            message: $"Legacy file upload API detected: {symbol}",
            filePath: document.FilePath ?? document.Name,
            line: line,
            column: column,
            symbol: symbol,
            snippet: snippet);
    }

    private static bool IsHttpPostedFileBase(INamedTypeSymbol symbol)
    {
        return symbol.Name.Equals("HttpPostedFileBase", StringComparison.Ordinal)
               && symbol.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool IsRequestFilesProperty(IPropertySymbol symbol)
    {
        return symbol.Name.Equals("Files", StringComparison.Ordinal)
               && symbol.ContainingType.Name.Equals("HttpRequest", StringComparison.Ordinal)
               && symbol.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool IsPostedFileInputStreamProperty(IPropertySymbol symbol)
    {
        return symbol.Name.Equals("InputStream", StringComparison.Ordinal)
               && symbol.ContainingType.Name.Equals("HttpPostedFileBase", StringComparison.Ordinal)
               && symbol.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }
}
