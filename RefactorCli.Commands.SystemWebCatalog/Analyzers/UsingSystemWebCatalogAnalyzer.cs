using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class UsingSystemWebCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0001",
        Title = "System.Web namespace imports",
        Category = "Namespace",
        Severity = "Info",
        WhatItDetects = "Using directives that import System.Web or child namespaces.",
        WhyItMatters = "Identifies source files with direct compile-time dependency on System.Web namespaces."
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
                    id: Rule.Id,
                    category: Rule.Category,
                    severity: Rule.Severity,
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
