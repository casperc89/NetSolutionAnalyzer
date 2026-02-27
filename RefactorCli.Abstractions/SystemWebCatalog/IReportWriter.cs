namespace RefactorCli.Abstractions.SystemWebCatalog;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(CatalogReport report, string outputDir, CancellationToken ct);
}
