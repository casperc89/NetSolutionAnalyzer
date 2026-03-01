namespace RefactorCli.Commands.DependencyGraph.Contracts;

public sealed class DependencyGraphReport
{
    public required DateTime GeneratedAtUtc { get; init; }

    public required string SolutionPath { get; init; }

    public required IReadOnlyList<ProjectDependencyNode> Projects { get; init; }

    public required IReadOnlyList<ProjectDependencyEdge> Edges { get; init; }

    public required IReadOnlyList<UpgradeOrderEntry> UpgradeOrderLeafToRoot { get; init; }

    public required IReadOnlyList<CycleReport> Cycles { get; init; }
}

public sealed class ProjectDependencyNode
{
    public required string ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public required IReadOnlyList<string> DirectDependencies { get; init; }

    public required IReadOnlyList<string> TransitiveDependencies { get; init; }

    public int UniqueTransitiveUpstreamClassCount { get; init; }

    public required IReadOnlyList<UpstreamClassDependency> UpstreamClasses { get; init; }
}

public sealed class UpstreamClassDependency
{
    public required string ClassName { get; init; }

    public required string DeclaringProjectName { get; init; }

    public string? DeclaringProjectPath { get; init; }

    public int ReferenceCount { get; init; }
}

public sealed class ProjectDependencyEdge
{
    public required string ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public required string DependsOnProjectName { get; init; }

    public string? DependsOnProjectPath { get; init; }
}

public sealed class UpgradeOrderEntry
{
    public int Index { get; init; }

    public required string ProjectName { get; init; }

    public string? ProjectPath { get; init; }
}

public sealed class CycleReport
{
    public required IReadOnlyList<string> ProjectNames { get; init; }
}
