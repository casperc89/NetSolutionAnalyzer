using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public sealed class HttpApplicationLifecycleCatalogAnalyzer : ICatalogAnalyzer
{
    private static readonly HashSet<string> LifecycleMethods = new(StringComparer.Ordinal)
    {
        "Application_BeginRequest",
        "Application_EndRequest",
        "Application_Error",
        "Session_Start",
        "Application_AuthenticateRequest"
    };

    private static readonly CatalogRuleDescriptor Rule = new()
    {
        Id = "SW0101",
        Title = "HttpApplication lifecycle and request event handlers",
        Category = "Pipeline",
        Severity = "Warning",
        WhatItDetects = "Classes deriving from HttpApplication and request lifecycle methods such as Application_BeginRequest.",
        WhyItMatters = "Global.asax and HttpApplication event logic usually needs redesign as ASP.NET Core middleware."
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
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
                if (classSymbol is null || !DerivesFromHttpApplication(classSymbol))
                {
                    continue;
                }

                var (classLine, classColumn) = CatalogAccumulator.GetLineAndColumn(classDecl.Identifier.GetLocation());
                acc.Add(
                    id: Rule.Id,
                    category: Rule.Category,
                    severity: Rule.Severity,
                    message: $"Type derives from System.Web.HttpApplication ({classSymbol.Name})",
                    filePath: document.FilePath ?? document.Name,
                    line: classLine,
                    column: classColumn,
                    symbol: "System.Web.HttpApplication",
                    snippet: classDecl.Identifier.Text);

                foreach (var methodDecl in classDecl.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!LifecycleMethods.Contains(methodDecl.Identifier.ValueText))
                    {
                        continue;
                    }

                    var (line, column) = CatalogAccumulator.GetLineAndColumn(methodDecl.Identifier.GetLocation());
                    acc.Add(
                        id: Rule.Id,
                        category: Rule.Category,
                        severity: Rule.Severity,
                        message: $"HttpApplication lifecycle handler '{methodDecl.Identifier.ValueText}'",
                        filePath: document.FilePath ?? document.Name,
                        line: line,
                        column: column,
                        symbol: methodDecl.Identifier.ValueText,
                        snippet: methodDecl.ToString().Trim());
                }
            }
        }
    }

    private static bool DerivesFromHttpApplication(INamedTypeSymbol symbol)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name.Equals("HttpApplication", StringComparison.Ordinal) &&
                current.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}
