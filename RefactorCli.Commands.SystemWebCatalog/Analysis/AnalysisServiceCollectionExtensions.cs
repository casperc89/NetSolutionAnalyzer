using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;
using RefactorCli.Abstractions.SystemWebCatalog;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddRoslynAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<IRoslynSolutionLoader, MSBuildRoslynSolutionLoader>();
        services.AddSingleton<ICatalogEngine, CatalogEngine>();
        services.AddSingleton<ISystemWebCatalogService, SystemWebCatalogService>();

        services.AddSingleton<ICatalogAnalyzer, UsingSystemWebCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, SemanticSystemWebSymbolCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, SystemWebBaseTypeCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, ConfigSystemWebCatalogAnalyzer>();
        services.AddSingleton<ICatalogAnalyzer, ViewSystemWebCatalogAnalyzer>();

        return services;
    }
}
