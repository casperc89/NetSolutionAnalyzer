using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace RefactorCli.Abstractions;

public interface ICommandModule
{
    string Name { get; }

    void Register(Command root, IServiceCollection services);
}
