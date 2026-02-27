namespace RefactorCli.Abstractions.SystemWebCatalog;

public interface ISystemWebCatalogService
{
    Task<CatalogReport> AnalyzeAsync(SystemWebCatalogOptions options, CancellationToken ct);
}
