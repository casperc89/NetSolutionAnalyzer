using Microsoft.Extensions.DependencyInjection;
using RefactorCli.Abstractions;

namespace RefactorCli.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IAppConsole, AppConsole>();
        return services;
    }
}
