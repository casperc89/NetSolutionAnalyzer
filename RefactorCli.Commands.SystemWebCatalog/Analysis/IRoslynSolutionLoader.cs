using Microsoft.CodeAnalysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public interface IRoslynSolutionLoader
{
    Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct);
}
