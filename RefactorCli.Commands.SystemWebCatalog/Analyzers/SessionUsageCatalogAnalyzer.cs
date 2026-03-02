using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class SessionUsageCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0400",
        Title = "Session usage patterns",
        Category = "State",
        Severity = "Warning",
        WhatItDetects = "Session[...], HttpContext.Current.Session, Controller.Session, and SessionStateItemCollection usage.",
        WhyItMatters = "ASP.NET Core session is opt-in and constrained by serialization/distribution decisions."
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
                var symbol = GetSymbol(semanticModel, memberAccess, ct);
                if (symbol is IPropertySymbol property && IsSessionProperty(property))
                {
                    var symbolName = HasHttpContextCurrentInChain(memberAccess, semanticModel, ct)
                        ? "System.Web.HttpContext.Current.Session"
                        : GetSessionPropertySymbol(property);
                    AddFinding(acc, document, memberAccess.GetLocation(), symbolName, memberAccess.ToString().Trim());
                }
            }

            foreach (var identifier in syntaxRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Parent is MemberAccessExpressionSyntax parentMemberAccess &&
                    parentMemberAccess.Name == identifier)
                {
                    continue;
                }

                var symbol = GetSymbol(semanticModel, identifier, ct);
                if (symbol is not IPropertySymbol property || !IsSessionProperty(property))
                {
                    continue;
                }

                var symbolName = GetSessionPropertySymbol(property);
                AddFinding(acc, document, identifier.GetLocation(), symbolName, identifier.ToString().Trim());
            }

            foreach (var elementAccess in syntaxRoot.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
            {
                if (SessionAccessAnalyzerHelpers.IsSessionIndexerAccess(elementAccess, semanticModel, ct, out var symbolName))
                {
                    AddFinding(
                        acc,
                        document,
                        elementAccess.GetLocation(),
                        symbolName,
                        elementAccess.ToString().Trim());
                }
            }

            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = GetSymbol(semanticModel, invocation, ct);
                if (symbol is not IMethodSymbol method || !method.IsExtensionMethod)
                {
                    continue;
                }

                var reducedReceiver = method.ReducedFrom?.Parameters.FirstOrDefault()?.Type;
                var declaredReceiver = method.Parameters.FirstOrDefault()?.Type;
                if (!SessionAccessAnalyzerHelpers.IsSessionLikeType(reducedReceiver) &&
                    !SessionAccessAnalyzerHelpers.IsSessionLikeType(declaredReceiver))
                {
                    continue;
                }

                AddFinding(
                    acc,
                    document,
                    invocation.GetLocation(),
                    $"{method.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{method.Name}(...)",
                    invocation.ToString().Trim());
            }

            foreach (var typeSyntax in syntaxRoot.DescendantNodes().OfType<TypeSyntax>())
            {
                var symbol = GetSymbol(semanticModel, typeSyntax, ct);
                if (symbol is not INamedTypeSymbol typeSymbol || !IsSessionStateItemCollection(typeSymbol))
                {
                    continue;
                }

                AddFinding(
                    acc,
                    document,
                    typeSyntax.GetLocation(),
                    "System.Web.SessionState.SessionStateItemCollection",
                    typeSyntax.ToString().Trim());
            }
        }
    }

    private static void AddFinding(CatalogAccumulator acc, Document document, Location location, string symbol, string snippet)
    {
        var (line, column) = CatalogAccumulator.GetLineAndColumn(location);
        acc.Add(
            id: Rule.Id,
            category: Rule.Category,
            severity: Rule.Severity,
            message: $"Session usage detected: {symbol}",
            filePath: document.FilePath ?? document.Name,
            line: line,
            column: column,
            symbol: symbol,
            snippet: snippet);
    }

    private static ISymbol? GetSymbol(SemanticModel semanticModel, SyntaxNode node, CancellationToken ct)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node, ct);
        return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
    }

    private static bool IsSessionProperty(IPropertySymbol property)
    {
        return property.Name.Equals("Session", StringComparison.Ordinal) &&
               SessionAccessAnalyzerHelpers.IsSessionLikeType(property.Type);
    }

    private static bool IsSessionStateItemCollection(INamedTypeSymbol type)
    {
        return type.Name.Equals("SessionStateItemCollection", StringComparison.Ordinal) &&
               type.ContainingNamespace?.ToDisplayString().Equals("System.Web.SessionState", StringComparison.Ordinal) == true;
    }

    private static bool HasHttpContextCurrentInChain(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel, CancellationToken ct)
    {
        if (memberAccess.Expression is not MemberAccessExpressionSyntax nestedMemberAccess)
        {
            return false;
        }

        var symbol = GetSymbol(semanticModel, nestedMemberAccess, ct);
        return symbol is IPropertySymbol property &&
               property.Name.Equals("Current", StringComparison.Ordinal) &&
               property.ContainingType.Name.Equals("HttpContext", StringComparison.Ordinal) &&
               property.ContainingType.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }

    private static string GetSessionPropertySymbol(IPropertySymbol property)
    {
        var containingType = property.OriginalDefinition.ContainingType;
        var containingName = TrimGlobalPrefix(containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        return $"{containingName}.Session";
    }
}
