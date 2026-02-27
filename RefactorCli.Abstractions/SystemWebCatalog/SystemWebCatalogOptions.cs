namespace RefactorCli.Abstractions.SystemWebCatalog;

public sealed class SystemWebCatalogOptions
{
    public required string SolutionPath { get; init; }

    public required string OutputPath { get; init; }

    public required IReadOnlyList<string> Formats { get; init; }

    public VerbosityLevel Verbosity { get; init; } = VerbosityLevel.Normal;
}

public enum VerbosityLevel
{
    Quiet,
    Normal,
    Diag
}
