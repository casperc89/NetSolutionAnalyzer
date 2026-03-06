using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Commands.DeadCode.Contracts;

namespace RefactorCli.Commands.DeadCode.Analysis;

public sealed class DeadCodeAnalysisEngine
{
    public async Task<DeadCodeReport> AnalyzeAsync(
        Solution solution,
        string solutionPath,
        DeadCodeOptions options,
        DeadCodeConfig config,
        CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var projects = solution.Projects
            .Where(p => !options.ExcludeTestProjects || !IsTestProject(p))
            .OrderBy(p => p.FilePath ?? p.Name, StringComparer.Ordinal)
            .ToList();

        var projectById = projects.ToDictionary(p => p.Id);

        var phase = Stopwatch.StartNew();
        var candidates = await CollectCandidatesAsync(projects, ct);
        var collectCandidatesMs = phase.ElapsedMilliseconds;

        phase.Restart();
        var roots = CollectRoots(candidates, config);
        var collectRootsMs = phase.ElapsedMilliseconds;

        phase.Restart();
        var incomingReferenceCounts = await BuildIncomingReferenceCountsAsync(projects, candidates, ct);
        var buildReferenceIndexMs = phase.ElapsedMilliseconds;

        phase.Restart();
        var dynamicPatterns = await CollectDynamicPatternsByProjectAsync(projects, config.DynamicUsage.ReflectionPatterns, ct);
        var collectDynamicPatternsMs = phase.ElapsedMilliseconds;

        phase.Restart();
        var suppressionMap = config.Suppressions
            .Where(s => !string.IsNullOrWhiteSpace(s.Symbol))
            .ToDictionary(s => s.Symbol!, s => s.Reason ?? string.Empty, StringComparer.Ordinal);

        var findings = new List<DeadCodeFinding>();
        foreach (var candidate in candidates.OrderBy(c => c.DisplayName, StringComparer.Ordinal))
        {
            if (suppressionMap.TryGetValue(candidate.DisplayName, out _))
            {
                continue;
            }

            var hasIncomingRefs = incomingReferenceCounts.TryGetValue(candidate.Key, out var referenceCount) && referenceCount > 0;
            if (roots.Contains(candidate.Key) || hasIncomingRefs)
            {
                continue;
            }

            var evidence = new List<string>();
            var confidence = ClassifyConfidence(candidate, config, dynamicPatterns, evidence);
            if ((int)confidence > (int)options.MinConfidence)
            {
                continue;
            }

            evidence.Insert(0, "No static references found.");

            var location = candidate.GetSourceLocation();
            findings.Add(new DeadCodeFinding
            {
                Id = "DC0001",
                Confidence = confidence,
                Symbol = candidate.DisplayName,
                SymbolKind = candidate.Symbol.Kind.ToString(),
                ProjectName = projectById[candidate.ProjectId].Name,
                ProjectPath = projectById[candidate.ProjectId].FilePath,
                Message = BuildMessage(confidence),
                FilePath = location?.FilePath,
                Line = location?.Line,
                Column = location?.Column,
                Evidence = evidence
            });
        }

        var classifyFindingsMs = phase.ElapsedMilliseconds;
        totalStopwatch.Stop();

        return new DeadCodeReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = solutionPath,
            ProjectsAnalyzed = projects.Count,
            Findings = findings,
            Diagnostics = new DeadCodeDiagnostics
            {
                CandidateSymbols = candidates.Count,
                RootSymbols = roots.Count,
                ProjectsWithDynamicPatterns = dynamicPatterns.Count,
                Timing = new DeadCodeTiming
                {
                    CollectCandidatesMs = collectCandidatesMs,
                    CollectRootsMs = collectRootsMs,
                    BuildReferenceIndexMs = buildReferenceIndexMs,
                    CollectDynamicPatternsMs = collectDynamicPatternsMs,
                    ClassifyFindingsMs = classifyFindingsMs,
                    TotalMs = totalStopwatch.ElapsedMilliseconds
                }
            }
        };
    }

    private static string BuildMessage(DeadCodeConfidence confidence)
    {
        return confidence switch
        {
            DeadCodeConfidence.DefinitelyDead => "Unreachable symbol with no dynamic usage indicators.",
            DeadCodeConfidence.LikelyDead => "Unreachable symbol; wider visibility lowers certainty.",
            _ => "Unreachable symbol, but dynamic/runtime usage patterns were detected in the project."
        };
    }

    private static DeadCodeConfidence ClassifyConfidence(
        CandidateSymbol candidate,
        DeadCodeConfig config,
        IReadOnlyDictionary<ProjectId, IReadOnlyList<string>> dynamicPatterns,
        List<string> evidence)
    {
        if (config.DynamicUsage.MarkUnknownIfMatched &&
            dynamicPatterns.TryGetValue(candidate.ProjectId, out var matchedPatterns) &&
            matchedPatterns.Count > 0)
        {
            evidence.Add($"Dynamic patterns matched in project: {string.Join(", ", matchedPatterns)}.");
            return DeadCodeConfidence.Unknown;
        }

        if (candidate.Symbol.DeclaredAccessibility is Accessibility.Private or Accessibility.NotApplicable)
        {
            evidence.Add("Symbol accessibility is private/not-applicable.");
            return DeadCodeConfidence.DefinitelyDead;
        }

        evidence.Add($"Symbol accessibility is {candidate.Symbol.DeclaredAccessibility}.");
        return DeadCodeConfidence.LikelyDead;
    }

    private static async Task<IReadOnlyDictionary<ProjectId, IReadOnlyList<string>>> CollectDynamicPatternsByProjectAsync(
        IReadOnlyList<Project> projects,
        IReadOnlyList<string> patterns,
        CancellationToken ct)
    {
        var normalizedPatterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var matches = new Dictionary<ProjectId, IReadOnlyList<string>>();
        if (normalizedPatterns.Count == 0)
        {
            return matches;
        }

        foreach (var project in projects)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var document in project.Documents)
            {
                if (document.SourceCodeKind != SourceCodeKind.Regular)
                {
                    continue;
                }

                var text = await document.GetTextAsync(ct);
                var value = text.ToString();
                foreach (var pattern in normalizedPatterns)
                {
                    if (value.Contains(pattern, StringComparison.Ordinal))
                    {
                        found.Add(pattern);
                    }
                }
            }

            if (found.Count > 0)
            {
                matches[project.Id] = found.OrderBy(x => x, StringComparer.Ordinal).ToList();
            }
        }

        return matches;
    }

    private static HashSet<SymbolKey> CollectRoots(IReadOnlyList<CandidateSymbol> candidates, DeadCodeConfig config)
    {
        var roots = new HashSet<SymbolKey>();
        var explicitSymbols = config.Roots.Symbols.ToHashSet(StringComparer.Ordinal);
        var attributeRoots = config.Roots.Attributes.ToHashSet(StringComparer.Ordinal);
        var nonActionAttributes = config.Frameworks.AspNetMvc.NonActionAttributes.ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (candidate.Symbol is IMethodSymbol method && method.Name.Equals("Main", StringComparison.Ordinal) && method.IsStatic)
            {
                roots.Add(candidate.Key);
                continue;
            }

            if (explicitSymbols.Contains(candidate.DisplayName))
            {
                roots.Add(candidate.Key);
                continue;
            }

            if (attributeRoots.Count > 0 && HasAnyAttribute(candidate.Symbol, attributeRoots))
            {
                roots.Add(candidate.Key);
                continue;
            }

            if (candidate.Symbol is IMethodSymbol action && IsAspNetMvcActionRoot(action, config.Frameworks.AspNetMvc, nonActionAttributes))
            {
                roots.Add(candidate.Key);
            }
        }

        return roots;
    }

    private static bool IsAspNetMvcActionRoot(IMethodSymbol method, AspNetMvcConfig config, IReadOnlySet<string> nonActionAttributes)
    {
        if (!config.Enabled)
        {
            return false;
        }

        if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var matchesController = containingType.Name.EndsWith(config.ControllerSuffix, StringComparison.Ordinal) ||
                                InheritsFrom(containingType, "Microsoft.AspNetCore.Mvc.ControllerBase") ||
                                InheritsFrom(containingType, "System.Web.Mvc.Controller");

        if (!matchesController)
        {
            return false;
        }

        if (HasAnyAttribute(method, nonActionAttributes))
        {
            return false;
        }

        return true;
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, string fullMetadataName)
    {
        var current = symbol;
        while (current is not null)
        {
            var name = TrimGlobalPrefix(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (string.Equals(name, fullMetadataName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasAnyAttribute(ISymbol symbol, IReadOnlySet<string> fullNames)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeType = attribute.AttributeClass;
            if (attributeType is null)
            {
                continue;
            }

            var fullName = TrimGlobalPrefix(attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (fullNames.Contains(fullName))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<Dictionary<SymbolKey, int>> BuildIncomingReferenceCountsAsync(
        IReadOnlyList<Project> projects,
        IReadOnlyList<CandidateSymbol> candidates,
        CancellationToken ct)
    {
        var counts = candidates.ToDictionary(c => c.Key, _ => 0, SymbolKeyComparer.Instance);

        foreach (var project in projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.SourceCodeKind != SourceCodeKind.Regular)
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(ct);
                if (root is null)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(ct);
                if (semanticModel is null)
                {
                    continue;
                }

                foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    ct.ThrowIfCancellationRequested();

                    var symbolInfo = semanticModel.GetSymbolInfo(name, ct);
                    var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                    if (symbol is null)
                    {
                        continue;
                    }

                    symbol = NormalizeSymbol(symbol);
                    if (symbol is null)
                    {
                        continue;
                    }

                    var key = CandidateSymbol.CreateKey(symbol);
                    if (!counts.TryGetValue(key, out _))
                    {
                        continue;
                    }

                    if (IsDeclarationNameReference(name, symbol, ct))
                    {
                        continue;
                    }

                    counts[key]++;
                }
            }
        }

        return counts;
    }

    private static ISymbol? NormalizeSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            IAliasSymbol alias => NormalizeSymbol(alias.Target),
            IMethodSymbol { MethodKind: MethodKind.Ordinary } method => method,
            INamedTypeSymbol type => type,
            _ => null
        };
    }

    private static bool IsDeclarationNameReference(SimpleNameSyntax name, ISymbol symbol, CancellationToken ct)
    {
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            var node = declaration.GetSyntax(ct);
            if (node == name)
            {
                return true;
            }

            if (node is MemberDeclarationSyntax member && member.Span.Contains(name.SpanStart) && member.Span.Contains(name.Span.End))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IReadOnlyList<CandidateSymbol>> CollectCandidatesAsync(
        IReadOnlyList<Project> projects,
        CancellationToken ct)
    {
        var candidates = new Dictionary<SymbolKey, CandidateSymbol>(SymbolKeyComparer.Instance);

        foreach (var project in projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.SourceCodeKind != SourceCodeKind.Regular)
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(ct);
                if (root is null)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(ct);
                if (semanticModel is null)
                {
                    continue;
                }

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();
                    var symbol = semanticModel.GetDeclaredSymbol(typeDecl, ct);
                    if (symbol is null || symbol.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    var candidate = CandidateSymbol.Create(symbol, project.Id);
                    candidates.TryAdd(candidate.Key, candidate);
                }

                foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();
                    var symbol = semanticModel.GetDeclaredSymbol(methodDecl, ct);
                    if (symbol is not IMethodSymbol methodSymbol || methodSymbol.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    if (methodSymbol.MethodKind != MethodKind.Ordinary)
                    {
                        continue;
                    }

                    if (methodSymbol.IsAbstract || methodSymbol.IsOverride)
                    {
                        continue;
                    }

                    var candidate = CandidateSymbol.Create(methodSymbol, project.Id);
                    candidates.TryAdd(candidate.Key, candidate);
                }
            }
        }

        return candidates.Values.ToList();
    }

    private static bool IsTestProject(Project project)
    {
        if (project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            project.Name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (project.FilePath is { } filePath)
        {
            if (filePath.Contains(".Tests.", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains(".Test.", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith("Tests.csproj", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var metadataReference in project.MetadataReferences)
        {
            var display = metadataReference.Display;
            if (display is null)
            {
                continue;
            }

            if (display.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                display.Contains("nunit", StringComparison.OrdinalIgnoreCase) ||
                display.Contains("MSTest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct SymbolKey(string Value);

    private sealed class SymbolKeyComparer : IEqualityComparer<SymbolKey>
    {
        public static SymbolKeyComparer Instance { get; } = new();

        public bool Equals(SymbolKey x, SymbolKey y) => StringComparer.Ordinal.Equals(x.Value, y.Value);

        public int GetHashCode(SymbolKey obj) => StringComparer.Ordinal.GetHashCode(obj.Value);
    }

    private sealed class CandidateSymbol
    {
        public required SymbolKey Key { get; init; }

        public required ISymbol Symbol { get; init; }

        public required string DisplayName { get; init; }

        public required ProjectId ProjectId { get; init; }

        public static CandidateSymbol Create(ISymbol symbol, ProjectId projectId)
        {
            var displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            var key = CreateKey(symbol);
            return new CandidateSymbol
            {
                Key = key,
                Symbol = symbol,
                DisplayName = displayName,
                ProjectId = projectId
            };
        }

        public static SymbolKey CreateKey(ISymbol symbol)
        {
            var displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            return new SymbolKey($"{symbol.Kind}:{displayName}");
        }

        public SourceLocation? GetSourceLocation()
        {
            var location = Symbol.Locations.FirstOrDefault(l => l.IsInSource);
            if (location is null)
            {
                return null;
            }

            var span = location.GetLineSpan();
            return new SourceLocation(
                span.Path,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1);
        }
    }

    private readonly record struct SourceLocation(string FilePath, int Line, int Column);

    private static string TrimGlobalPrefix(string value)
    {
        const string prefix = "global::";
        return value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
    }
}
