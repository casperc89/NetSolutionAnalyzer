using Microsoft.CodeAnalysis;

namespace RefactorCli.Analysis.Roslyn;

public interface IRoslynSolutionLoader
{
    Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct);
}
