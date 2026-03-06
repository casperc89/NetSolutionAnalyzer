namespace RefactorCli.Commands.DeadCode.Contracts;

public sealed class DeadCodeReport
{
    public required DateTime GeneratedAtUtc { get; init; }

    public required string SolutionPath { get; init; }

    public int ProjectsAnalyzed { get; init; }

    public required IReadOnlyList<DeadCodeFinding> Findings { get; init; }
}

public sealed class DeadCodeFinding
{
    public required string Id { get; init; }

    public required DeadCodeConfidence Confidence { get; init; }

    public required string Symbol { get; init; }

    public required string SymbolKind { get; init; }

    public required string ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public required string Message { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }

    public int? Column { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }
}
