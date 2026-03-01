namespace RefactorCli.Commands.DependencyGraph.Contracts;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(DependencyGraphReport report, string outputDir, int maxClassesPerProject, CancellationToken ct);
}
