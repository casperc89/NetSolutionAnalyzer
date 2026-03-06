namespace RefactorCli.Commands.MethodDependency.Contracts;

public sealed class MethodDependencyOptions
{
    public required string SolutionPath { get; init; }

    public required string FilePath { get; init; }

    public int Line { get; init; }

    public required string OutputPath { get; init; }

    public required IReadOnlyList<string> Formats { get; init; }

    public required VerbosityLevel Verbosity { get; init; }
}

public enum VerbosityLevel
{
    Quiet,
    Normal,
    Diag
}
