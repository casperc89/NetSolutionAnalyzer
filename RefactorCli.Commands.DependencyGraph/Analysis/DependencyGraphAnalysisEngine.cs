using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCli.Commands.DependencyGraph.Contracts;

namespace RefactorCli.Commands.DependencyGraph.Analysis;

public sealed class DependencyGraphAnalysisEngine
{
    public async Task<DependencyGraphReport> AnalyzeAsync(Solution solution, string solutionPath, CancellationToken ct)
    {
        var projects = solution.Projects
            .OrderBy(ProjectKey, StringComparer.Ordinal)
            .ToList();

        var projectMap = projects.ToDictionary(p => p.Id);
        var dependencyMap = projects.ToDictionary(
            p => p.Id,
            p => p.ProjectReferences
                .Select(pr => pr.ProjectId)
                .Where(projectMap.ContainsKey)
                .Distinct()
                .OrderBy(ProjectKeyFromId, StringComparer.Ordinal)
                .ToList());

        var postOrder = BuildPostOrder(projects.Select(p => p.Id), dependencyMap, projectMap);
        var cycles = FindCycles(projects.Select(p => p.Id).ToList(), dependencyMap, projectMap);

        var projectNodes = new List<ProjectDependencyNode>(projects.Count);
        foreach (var project in projects)
        {
            var transitiveDependencies = GetTransitiveDependencies(project.Id, dependencyMap);
            var upstreamClasses = await ExtractUpstreamClassDependenciesAsync(project, transitiveDependencies, solution, projectMap, ct);

            projectNodes.Add(new ProjectDependencyNode
            {
                ProjectName = project.Name,
                ProjectPath = project.FilePath,
                DirectDependencies = dependencyMap[project.Id]
                    .Select(id => projectMap[id].Name)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList(),
                TransitiveDependencies = transitiveDependencies
                    .Select(id => projectMap[id].Name)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList(),
                UniqueTransitiveUpstreamClassCount = upstreamClasses.Count,
                UpstreamClasses = upstreamClasses
            });
        }

        var edges = projects
            .SelectMany(project => dependencyMap[project.Id].Select(dep => new ProjectDependencyEdge
            {
                ProjectName = project.Name,
                ProjectPath = project.FilePath,
                DependsOnProjectName = projectMap[dep].Name,
                DependsOnProjectPath = projectMap[dep].FilePath
            }))
            .OrderBy(e => e.ProjectName, StringComparer.Ordinal)
            .ThenBy(e => e.DependsOnProjectName, StringComparer.Ordinal)
            .ToList();

        var upgradeOrder = postOrder
            .Select((projectId, index) => new UpgradeOrderEntry
            {
                Index = index + 1,
                ProjectName = projectMap[projectId].Name,
                ProjectPath = projectMap[projectId].FilePath
            })
            .ToList();

        return new DependencyGraphReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SolutionPath = solutionPath,
            Projects = projectNodes
                .OrderBy(p => p.ProjectName, StringComparer.Ordinal)
                .ToList(),
            Edges = edges,
            UpgradeOrderLeafToRoot = upgradeOrder,
            Cycles = cycles
        };

        string ProjectKey(Project project) => project.FilePath ?? project.Name;
        string ProjectKeyFromId(ProjectId projectId) => ProjectKey(projectMap[projectId]);
    }

    private static IReadOnlyList<ProjectId> BuildPostOrder(
        IEnumerable<ProjectId> allProjects,
        IReadOnlyDictionary<ProjectId, List<ProjectId>> dependencyMap,
        IReadOnlyDictionary<ProjectId, Project> projectMap)
    {
        var visited = new HashSet<ProjectId>();
        var order = new List<ProjectId>();

        foreach (var projectId in allProjects.OrderBy(ProjectKey, StringComparer.Ordinal))
        {
            Visit(projectId);
        }

        return order;

        void Visit(ProjectId projectId)
        {
            if (!visited.Add(projectId))
            {
                return;
            }

            foreach (var dependencyId in dependencyMap[projectId].OrderBy(ProjectKey, StringComparer.Ordinal))
            {
                Visit(dependencyId);
            }

            order.Add(projectId);
        }

        string ProjectKey(ProjectId id) => projectMap[id].FilePath ?? projectMap[id].Name;
    }

    private static IReadOnlyList<CycleReport> FindCycles(
        IReadOnlyList<ProjectId> projectIds,
        IReadOnlyDictionary<ProjectId, List<ProjectId>> dependencyMap,
        IReadOnlyDictionary<ProjectId, Project> projectMap)
    {
        var index = 0;
        var stack = new Stack<ProjectId>();
        var onStack = new HashSet<ProjectId>();
        var indexes = new Dictionary<ProjectId, int>();
        var lowLinks = new Dictionary<ProjectId, int>();
        var cycles = new List<CycleReport>();

        foreach (var projectId in projectIds.OrderBy(id => projectMap[id].FilePath ?? projectMap[id].Name, StringComparer.Ordinal))
        {
            if (!indexes.ContainsKey(projectId))
            {
                StrongConnect(projectId);
            }
        }

        return cycles;

        void StrongConnect(ProjectId projectId)
        {
            indexes[projectId] = index;
            lowLinks[projectId] = index;
            index++;

            stack.Push(projectId);
            onStack.Add(projectId);

            foreach (var dependencyId in dependencyMap[projectId].OrderBy(id => projectMap[id].FilePath ?? projectMap[id].Name, StringComparer.Ordinal))
            {
                if (!indexes.ContainsKey(dependencyId))
                {
                    StrongConnect(dependencyId);
                    lowLinks[projectId] = Math.Min(lowLinks[projectId], lowLinks[dependencyId]);
                }
                else if (onStack.Contains(dependencyId))
                {
                    lowLinks[projectId] = Math.Min(lowLinks[projectId], indexes[dependencyId]);
                }
            }

            if (lowLinks[projectId] != indexes[projectId])
            {
                return;
            }

            var component = new List<ProjectId>();
            ProjectId current;
            do
            {
                current = stack.Pop();
                onStack.Remove(current);
                component.Add(current);
            }
            while (current != projectId);

            var hasSelfEdge = dependencyMap[projectId].Contains(projectId);
            if (component.Count <= 1 && !hasSelfEdge)
            {
                return;
            }

            cycles.Add(new CycleReport
            {
                ProjectNames = component
                    .Select(id => projectMap[id].Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList()
            });
        }
    }

    private static HashSet<ProjectId> GetTransitiveDependencies(
        ProjectId projectId,
        IReadOnlyDictionary<ProjectId, List<ProjectId>> dependencyMap)
    {
        var visited = new HashSet<ProjectId>();
        var stack = new Stack<ProjectId>(dependencyMap[projectId]);

        while (stack.Count > 0)
        {
            var next = stack.Pop();
            if (!visited.Add(next))
            {
                continue;
            }

            foreach (var dependency in dependencyMap[next])
            {
                if (!visited.Contains(dependency))
                {
                    stack.Push(dependency);
                }
            }
        }

        return visited;
    }

    private static async Task<IReadOnlyList<UpstreamClassDependency>> ExtractUpstreamClassDependenciesAsync(
        Project project,
        HashSet<ProjectId> upstreamProjects,
        Solution solution,
        IReadOnlyDictionary<ProjectId, Project> projectMap,
        CancellationToken ct)
    {
        var classCounts = new Dictionary<(string ClassName, ProjectId ProjectId), int>();

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

            foreach (var nameSyntax in syntaxRoot.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(nameSyntax, ct);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                var classSymbol = TryGetClassSymbol(symbol);
                if (classSymbol is null)
                {
                    continue;
                }

                var declaringProjectId = TryGetDeclaringProjectId(classSymbol, solution);
                if (declaringProjectId is null || !upstreamProjects.Contains(declaringProjectId))
                {
                    continue;
                }

                var className = TrimGlobalPrefix(classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                var key = (className, declaringProjectId);
                classCounts[key] = classCounts.TryGetValue(key, out var existing) ? existing + 1 : 1;
            }
        }

        return classCounts
            .Select(kvp => new UpstreamClassDependency
            {
                ClassName = kvp.Key.ClassName,
                DeclaringProjectName = projectMap[kvp.Key.ProjectId].Name,
                DeclaringProjectPath = projectMap[kvp.Key.ProjectId].FilePath,
                ReferenceCount = kvp.Value
            })
            .OrderBy(x => x.DeclaringProjectName, StringComparer.Ordinal)
            .ThenBy(x => x.ClassName, StringComparer.Ordinal)
            .ToList();
    }

    private static INamedTypeSymbol? TryGetClassSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        if (symbol is IAliasSymbol alias)
        {
            return TryGetClassSymbol(alias.Target);
        }

        INamedTypeSymbol? type = symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol @event => @event.ContainingType,
            _ => null
        };

        if (type is null)
        {
            return null;
        }

        return type.TypeKind == TypeKind.Class ? type.OriginalDefinition : null;
    }

    private static ProjectId? TryGetDeclaringProjectId(INamedTypeSymbol classSymbol, Solution solution)
    {
        foreach (var location in classSymbol.Locations)
        {
            if (!location.IsInSource)
            {
                continue;
            }

            var document = solution.GetDocument(location.SourceTree);
            if (document?.Project.Id is ProjectId projectId)
            {
                return projectId;
            }
        }

        return null;
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }
}
