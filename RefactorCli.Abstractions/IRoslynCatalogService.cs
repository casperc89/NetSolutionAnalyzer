namespace RefactorCli.Abstractions;

public interface ISystemWebCatalogService
{
    Task<CatalogReport> AnalyzeAsync(SystemWebCatalogOptions options, CancellationToken ct);
}
