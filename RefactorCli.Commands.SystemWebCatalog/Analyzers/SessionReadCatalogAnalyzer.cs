using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class SessionReadCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0401",
        Title = "Session key reads",
        Category = "State",
        Severity = "Warning",
        WhatItDetects = "Reads from Session[key] on System.Web session APIs and captures the key when statically known.",
        WhyItMatters = "Read patterns expose initialization dependencies and help identify missing or dead session state during migration."
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

            foreach (var elementAccess in syntaxRoot.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
            {
                if (!SessionAccessAnalyzerHelpers.IsSessionRead(elementAccess) ||
                    !SessionAccessAnalyzerHelpers.IsSessionIndexerAccess(elementAccess, semanticModel, ct, out var symbol))
                {
                    continue;
                }

                var key = SessionAccessAnalyzerHelpers.GetSessionKey(elementAccess, semanticModel, ct);
                AddFinding(acc, document, elementAccess.GetLocation(), symbol, key, elementAccess.ToString().Trim());
            }
        }
    }

    private static void AddFinding(
        CatalogAccumulator acc,
        Document document,
        Location location,
        string symbol,
        string key,
        string snippet)
    {
        var (line, column) = CatalogAccumulator.GetLineAndColumn(location);
        var keyLabel = key == SessionAccessAnalyzerHelpers.DynamicSessionKey ? "<dynamic>" : $"'{key}'";
        acc.Add(
            id: Rule.Id,
            category: Rule.Category,
            severity: Rule.Severity,
            message: $"Session key read detected: {keyLabel}",
            filePath: document.FilePath ?? document.Name,
            line: line,
            column: column,
            symbol: symbol,
            snippet: snippet,
            properties: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sessionKey"] = key,
                ["sessionAccess"] = "read"
            });
    }
}
