using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class ServerMapPathCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0104",
        Title = "Server.MapPath and HttpServerUtility usage",
        Category = "Hosting",
        Severity = "Warning",
        WhatItDetects = "Direct calls to Server.MapPath and HttpContext.Current.Server / HttpServerUtility access.",
        WhyItMatters = "Path resolution should move to IWebHostEnvironment.ContentRootPath/WebRootPath in ASP.NET Core."
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

                if (symbol is IPropertySymbol property && IsHttpContextServerProperty(property))
                {
                    var (line, column) = CatalogAccumulator.GetLineAndColumn(memberAccess.GetLocation());
                    acc.Add(
                        id: Rule.Id,
                        category: Rule.Category,
                        severity: Rule.Severity,
                        message: "Direct use of HttpContext.Current.Server",
                        filePath: document.FilePath ?? document.Name,
                        line: line,
                        column: column,
                        symbol: "System.Web.HttpContext.Server",
                        snippet: memberAccess.ToString().Trim());
                }
            }

            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation, ct);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (symbol is not IMethodSymbol method || !IsMapPathMethod(method))
                {
                    continue;
                }

                var (line, column) = CatalogAccumulator.GetLineAndColumn(invocation.GetLocation());
                acc.Add(
                    id: Rule.Id,
                    category: Rule.Category,
                    severity: Rule.Severity,
                    message: "Direct use of Server.MapPath/HttpServerUtility.MapPath",
                    filePath: document.FilePath ?? document.Name,
                    line: line,
                    column: column,
                    symbol: "System.Web.HttpServerUtility.MapPath",
                    snippet: invocation.ToString().Trim());
            }
        }
    }

    private static bool IsHttpContextServerProperty(IPropertySymbol property)
    {
        return property.Name.Equals("Server", StringComparison.Ordinal)
               && property.ContainingType.Name.Equals("HttpContext", StringComparison.Ordinal)
               && property.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static bool IsMapPathMethod(IMethodSymbol method)
    {
        if (!method.Name.Equals("MapPath", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        if (containingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return containingType.Name.Equals("HttpServerUtility", StringComparison.Ordinal);
    }
}
