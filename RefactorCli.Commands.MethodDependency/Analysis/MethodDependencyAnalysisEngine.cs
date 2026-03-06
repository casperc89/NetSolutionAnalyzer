using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Abstractions;
using RefactorCli.Commands.MethodDependency.Contracts;

namespace RefactorCli.Commands.MethodDependency.Analysis;

public sealed class MethodDependencyAnalysisEngine
{
    private static readonly SymbolDisplayFormat TypeSymbolFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public async Task<MethodDependencyReport> AnalyzeAsync(
        Solution solution,
        string solutionPath,
        string filePath,
        int line,
        CancellationToken ct)
    {
        var document = FindDocumentByPath(solution, filePath);
        if (document is null)
        {
            throw new InvalidCommandOptionsException($"File was not found in the loaded solution: {filePath}");
        }

        var rootMethod = await ResolveRootMethodAsync(document, line, ct);
        if (rootMethod is null)
        {
            throw new InvalidCommandOptionsException($"Line {line} is not inside a method declaration in file '{filePath}'.");
        }

        var nodes = new Dictionary<string, MethodNode>(StringComparer.Ordinal);
        var edges = new List<MethodDependencyEdge>();
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedCallSite>();
        var unresolvedKeys = new HashSet<string>(StringComparer.Ordinal);

        var queue = new Queue<IMethodSymbol>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        queue.Enqueue(rootMethod);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            var callerId = CreateSymbolId(current, ct);
            if (!visited.Add(callerId))
            {
                continue;
            }

            AddNode(nodes, current, solution, ct);

            var callSites = await CollectCallSitesAsync(current, solution, ct);
            foreach (var callSite in callSites)
            {
                if (callSite.Callee is null)
                {
                    var unresolvedKey = $"{callerId}|{callSite.FilePath}|{callSite.Line}|{callSite.Reason}|{callSite.Expression}";
                    if (unresolvedKeys.Add(unresolvedKey))
                    {
                        unresolved.Add(new UnresolvedCallSite
                        {
                            CallerSymbolId = callerId,
                            Reason = callSite.Reason,
                            Expression = callSite.Expression,
                            FilePath = callSite.FilePath,
                            Line = callSite.Line
                        });
                    }

                    continue;
                }

                var callee = NormalizeMethod(callSite.Callee);
                var calleeId = CreateSymbolId(callee, ct);
                AddNode(nodes, callee, solution, ct);

                var edgeKey = $"{callerId}|{calleeId}|{callSite.FilePath}|{callSite.Line}";
                if (edgeKeys.Add(edgeKey))
                {
                    edges.Add(new MethodDependencyEdge
                    {
                        CallerSymbolId = callerId,
                        CalleeSymbolId = calleeId,
                        FilePath = callSite.FilePath,
                        Line = callSite.Line
                    });
                }

                if (!visited.Contains(calleeId))
                {
                    queue.Enqueue(callee);
                }
            }
        }

        var rootMethodId = CreateSymbolId(rootMethod, ct);

        return new MethodDependencyReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = solutionPath,
            RequestedFilePath = filePath,
            RequestedLine = line,
            RootMethod = nodes[rootMethodId],
            Methods = nodes.Values
                .OrderBy(x => x.DisplayName, StringComparer.Ordinal)
                .ToList(),
            Edges = edges
                .OrderBy(x => x.CallerSymbolId, StringComparer.Ordinal)
                .ThenBy(x => x.CalleeSymbolId, StringComparer.Ordinal)
                .ThenBy(x => x.FilePath, StringComparer.Ordinal)
                .ThenBy(x => x.Line)
                .ToList(),
            UnresolvedCallSites = unresolved
                .OrderBy(x => x.FilePath, StringComparer.Ordinal)
                .ThenBy(x => x.Line)
                .ThenBy(x => x.Reason, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static Document? FindDocumentByPath(Solution solution, string filePath)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return solution.Projects
            .OrderBy(p => p.FilePath ?? p.Name, StringComparer.Ordinal)
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.FilePath is not null && string.Equals(Path.GetFullPath(d.FilePath), filePath, comparer));
    }

    private static async Task<IMethodSymbol?> ResolveRootMethodAsync(Document document, int line, CancellationToken ct)
    {
        var text = await document.GetTextAsync(ct);
        if (line < 1 || line > text.Lines.Count)
        {
            throw new InvalidCommandOptionsException($"--line must be between 1 and {text.Lines.Count} for file '{document.FilePath}'.");
        }

        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null)
        {
            return null;
        }

        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel is null)
        {
            return null;
        }

        var position = text.Lines[line - 1].Start;
        var token = root.FindToken(position);

        var declaration = token.Parent?
            .AncestorsAndSelf()
            .FirstOrDefault(IsMethodLikeDeclaration);

        if (declaration is null)
        {
            return null;
        }

        return declaration switch
        {
            BaseMethodDeclarationSyntax baseMethod => semanticModel.GetDeclaredSymbol(baseMethod, ct) as IMethodSymbol,
            LocalFunctionStatementSyntax localFunction => semanticModel.GetDeclaredSymbol(localFunction, ct) as IMethodSymbol,
            AccessorDeclarationSyntax accessor => semanticModel.GetDeclaredSymbol(accessor, ct) as IMethodSymbol,
            _ => null
        };
    }

    private static bool IsMethodLikeDeclaration(SyntaxNode node)
    {
        return node is BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax;
    }

    private static async Task<IReadOnlyList<CallSite>> CollectCallSitesAsync(IMethodSymbol method, Solution solution, CancellationToken ct)
    {
        var callSites = new List<CallSite>();

        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            ct.ThrowIfCancellationRequested();

            var declaration = await syntaxReference.GetSyntaxAsync(ct);
            var tree = declaration.SyntaxTree;
            var document = solution.GetDocument(tree);
            if (document is null)
            {
                continue;
            }

            var semanticModel = await document.GetSemanticModelAsync(ct);
            if (semanticModel is null)
            {
                continue;
            }

            foreach (var executableRoot in GetExecutableRoots(declaration))
            {
                foreach (var invocation in executableRoot.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    callSites.Add(CreateCallSiteFromSymbolInfo(
                        semanticModel.GetSymbolInfo(invocation, ct),
                        document.FilePath,
                        invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        invocation.ToString(),
                        "Unable to resolve invocation symbol."));
                }

                foreach (var objectCreation in executableRoot.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>())
                {
                    callSites.Add(CreateCallSiteFromSymbolInfo(
                        semanticModel.GetSymbolInfo(objectCreation, ct),
                        document.FilePath,
                        objectCreation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        objectCreation.ToString(),
                        "Unable to resolve object creation constructor symbol."));
                }

                foreach (var objectCreation in executableRoot.DescendantNodesAndSelf().OfType<ImplicitObjectCreationExpressionSyntax>())
                {
                    callSites.Add(CreateCallSiteFromSymbolInfo(
                        semanticModel.GetSymbolInfo(objectCreation, ct),
                        document.FilePath,
                        objectCreation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        objectCreation.ToString(),
                        "Unable to resolve implicit object creation constructor symbol."));
                }
            }

            if (declaration is ConstructorDeclarationSyntax constructor && constructor.Initializer is not null)
            {
                callSites.Add(CreateCallSiteFromSymbolInfo(
                    semanticModel.GetSymbolInfo(constructor.Initializer, ct),
                    document.FilePath,
                    constructor.Initializer.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    constructor.Initializer.ToString(),
                    "Unable to resolve constructor initializer symbol."));
            }
        }

        return callSites;
    }

    private static IEnumerable<SyntaxNode> GetExecutableRoots(SyntaxNode declaration)
    {
        switch (declaration)
        {
            case BaseMethodDeclarationSyntax baseMethod:
                if (baseMethod.Body is not null)
                {
                    yield return baseMethod.Body;
                }

                if (baseMethod.ExpressionBody is not null)
                {
                    yield return baseMethod.ExpressionBody.Expression;
                }

                break;

            case LocalFunctionStatementSyntax localFunction:
                if (localFunction.Body is not null)
                {
                    yield return localFunction.Body;
                }

                if (localFunction.ExpressionBody is not null)
                {
                    yield return localFunction.ExpressionBody.Expression;
                }

                break;

            case AccessorDeclarationSyntax accessor:
                if (accessor.Body is not null)
                {
                    yield return accessor.Body;
                }

                if (accessor.ExpressionBody is not null)
                {
                    yield return accessor.ExpressionBody.Expression;
                }

                break;
        }
    }

    private static CallSite CreateCallSiteFromSymbolInfo(
        SymbolInfo symbolInfo,
        string? filePath,
        int line,
        string expression,
        string unresolvedReason)
    {
        var method = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        return new CallSite(
            method,
            unresolvedReason,
            expression,
            filePath,
            line);
    }

    private static IMethodSymbol NormalizeMethod(IMethodSymbol method)
    {
        var target = method.ReducedFrom ?? method;
        return (IMethodSymbol)target.OriginalDefinition;
    }

    private static void AddNode(
        IDictionary<string, MethodNode> nodes,
        IMethodSymbol method,
        Solution solution,
        CancellationToken ct)
    {
        var symbolId = CreateSymbolId(method, ct);
        if (nodes.ContainsKey(symbolId))
        {
            return;
        }

        var location = method.Locations.FirstOrDefault(l => l.IsInSource);
        var document = location is null ? null : solution.GetDocument(location.SourceTree);

        nodes[symbolId] = new MethodNode
        {
            SymbolId = symbolId,
            DisplayName = GetMethodDisplayName(method),
            ProjectName = document?.Project.Name,
            ProjectPath = document?.Project.FilePath,
            FilePath = location?.SourceTree?.FilePath,
            Line = location is null ? null : location.GetLineSpan().StartLinePosition.Line + 1,
            IsExternal = location is null
        };
    }

    private static string CreateSymbolId(IMethodSymbol symbol, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var containingAssembly = symbol.ContainingAssembly?.Identity?.Name ?? "unknown";
        var signature = GetMethodDisplayName(symbol);
        return $"{containingAssembly}|{signature}";
    }

    private static string GetMethodDisplayName(IMethodSymbol symbol)
    {
        var containingType = symbol.ContainingType is null
            ? null
            : TrimGlobalPrefix(symbol.ContainingType.ToDisplayString(TypeSymbolFormat));

        var parameters = string.Join(", ", symbol.Parameters
            .Select(p => TrimGlobalPrefix(p.Type.ToDisplayString(TypeSymbolFormat))));

        return containingType is null
            ? $"{symbol.Name}({parameters})"
            : $"{containingType}.{symbol.Name}({parameters})";
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }

    private sealed record CallSite(
        IMethodSymbol? Callee,
        string Reason,
        string? Expression,
        string? FilePath,
        int? Line);
}
