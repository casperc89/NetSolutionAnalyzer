namespace RefactorCli.Commands.MethodDependency.Contracts;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(MethodDependencyReport report, string outputDir, CancellationToken ct);
}
