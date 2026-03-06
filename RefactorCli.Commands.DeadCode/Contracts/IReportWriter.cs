namespace RefactorCli.Commands.DeadCode.Contracts;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(DeadCodeReport report, string outputDir, CancellationToken ct);
}
