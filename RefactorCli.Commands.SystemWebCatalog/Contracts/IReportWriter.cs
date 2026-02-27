namespace RefactorCli.Commands.SystemWebCatalog.Contracts;

public interface IReportWriter
{
    string Format { get; }

    Task<string> WriteAsync(CatalogReport report, string outputDir, CancellationToken ct);
}
