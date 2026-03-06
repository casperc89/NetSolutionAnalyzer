using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Infrastructure;

namespace RefactorCli.Commands.MethodDependency.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddMethodDependencyAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<MSBuildRoslynSolutionLoader>();
        services.AddSingleton<MethodDependencyAnalysisEngine>();
        services.AddSingleton<MethodDependencyService>();
        return services;
    }
}
