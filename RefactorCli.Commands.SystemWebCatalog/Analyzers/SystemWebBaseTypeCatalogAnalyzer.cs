using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class SystemWebBaseTypeCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0003",
        Title = "System.Web inheritance and interface implementation",
        Category = "Type",
        Severity = "Warning",
        WhatItDetects = "Types that derive from or implement System.Web-based framework types.",
        WhyItMatters = "Inheritance dependencies are often high-impact and usually require larger migration design changes."
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
                        id: Rule.Id,
                        category: Rule.Category,
                        severity: Rule.Severity,
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
