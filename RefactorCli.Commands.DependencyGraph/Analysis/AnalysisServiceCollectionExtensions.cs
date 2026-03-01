using Microsoft.Extensions.DependencyInjection;

namespace RefactorCli.Commands.DependencyGraph.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddDependencyGraphAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<MSBuildRoslynSolutionLoader>();
        services.AddSingleton<DependencyGraphAnalysisEngine>();
        services.AddSingleton<DependencyGraphService>();
        return services;
    }
}
