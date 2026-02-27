namespace RefactorCli.Abstractions;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(CatalogReport report, string outputDir, CancellationToken ct);
}
