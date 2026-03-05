using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace RefactorCli.Abstractions;

public interface ICommandModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services);

    void RegisterCommands(Command root, IServiceProvider serviceProvider);
}
