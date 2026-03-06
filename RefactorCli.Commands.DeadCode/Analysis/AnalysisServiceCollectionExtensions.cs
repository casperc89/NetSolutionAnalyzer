using Microsoft.Extensions.DependencyInjection;

namespace RefactorCli.Commands.DeadCode.Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddDeadCodeAnalysis(this IServiceCollection services)
    {
        services.AddTransient<DeadCodeService>();
        services.AddTransient<DeadCodeAnalysisEngine>();
        return services;
    }
}
