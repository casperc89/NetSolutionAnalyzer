namespace RefactorCli.Commands.DependencyGraph.Contracts;

public sealed class DependencyGraphOptions
{
    public required string SolutionPath { get; init; }

    public required string OutputPath { get; init; }

    public required IReadOnlyList<string> Formats { get; init; }

    public required VerbosityLevel Verbosity { get; init; }

    public int MaxClassesPerProject { get; init; } = 50;

    public bool ExcludeTestProjects { get; init; }
}

public enum VerbosityLevel
{
    Quiet,
    Normal,
    Diag
}
