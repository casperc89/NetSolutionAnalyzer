using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Commands.SystemWebCatalog.Analyzers;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddRoslynAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<MSBuildRoslynSolutionLoader>();
        services.AddSingleton<CatalogEngine>();
        services.AddSingleton<SystemWebCatalogService>();

        services.AddSingleton<ICatalogAnalyzer, UsingSystemWebCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, SemanticSystemWebSymbolCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, SystemWebBaseTypeCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, ConfigSystemWebCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, ViewSystemWebCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, HttpContextCurrentCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, HttpApplicationLifecycleCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, ServerMapPathCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, SessionUsageCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, PostedFileCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, HeadersCookiesCatalogAnalyzer>();

        return services;
    }
}
