namespace RefactorCli.Commands.DeadCode.Contracts;

public sealed class DeadCodeOptions
{
    public required string SolutionPath { get; init; }

    public required string OutputPath { get; init; }

    public required IReadOnlyList<string> Formats { get; init; }

    public bool ExcludeTestProjects { get; init; } = true;

    public DeadCodeConfidence MinConfidence { get; init; } = DeadCodeConfidence.DefinitelyDead;

    public string? ConfigPath { get; init; }
}

public enum DeadCodeConfidence
{
    DefinitelyDead = 0,
    LikelyDead = 1,
    Unknown = 2
}
