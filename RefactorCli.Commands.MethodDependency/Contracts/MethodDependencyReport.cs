namespace RefactorCli.Commands.MethodDependency.Contracts;

public sealed class MethodDependencyReport
{
    public required DateTime GeneratedAtUtc { get; init; }

    public required string SolutionPath { get; init; }

    public required string RequestedFilePath { get; init; }

    public int RequestedLine { get; init; }

    public required MethodNode RootMethod { get; init; }

    public required IReadOnlyList<MethodNode> Methods { get; init; }

    public required IReadOnlyList<MethodDependencyEdge> Edges { get; init; }

    public required IReadOnlyList<UnresolvedCallSite> UnresolvedCallSites { get; init; }
}

public sealed class MethodNode
{
    public required string SymbolId { get; init; }

    public required string DisplayName { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }

    public bool IsExternal { get; init; }
}

public sealed class MethodDependencyEdge
{
    public required string CallerSymbolId { get; init; }

    public required string CalleeSymbolId { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }
}

public sealed class UnresolvedCallSite
{
    public required string CallerSymbolId { get; init; }

    public required string Reason { get; init; }

    public string? Expression { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }
}
