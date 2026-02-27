using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace RefactorCli.Abstractions;

public static class ServiceCollectionModuleExtensions
{
    public static IServiceCollection AddModule<TModule>(this IServiceCollection services, Command root)
        where TModule : class, ICommandModule, new()
    {
        var module = new TModule();
        module.Register(root, services);
        services.AddSingleton<ICommandModule>(module);
        return services;
    }
}
