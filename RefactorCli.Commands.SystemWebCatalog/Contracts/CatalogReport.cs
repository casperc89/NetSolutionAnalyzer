namespace RefactorCli.Commands.SystemWebCatalog.Contracts;

public sealed class CatalogReport
{
    public required DateTime GeneratedAtUtc { get; init; }

    public required string SolutionPath { get; init; }

    public required IReadOnlyList<ProjectReport> Projects { get; init; }
}

public sealed class ProjectReport
{
    public required string ProjectName { get; init; }

    public string? ProjectPath { get; init; }

    public string? TargetFramework { get; init; }

    public int DocumentsAnalyzed { get; init; }

    public required IReadOnlyList<Finding> Findings { get; init; }
}

public sealed class Finding
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Symbol { get; init; }

    public required string FilePath { get; init; }

    public int? Line { get; init; }

    public int? Column { get; init; }

    public string? Snippet { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
