using RefactorCli.Abstractions;
using RefactorCli.Commands.DependencyGraph.Contracts;
using RefactorCli.Infrastructure;

namespace RefactorCli.Commands.DependencyGraph.Analysis;

public sealed class DependencyGraphService
{
    private readonly MSBuildRoslynSolutionLoader _solutionLoader;
    private readonly DependencyGraphAnalysisEngine _engine;

    public DependencyGraphService(MSBuildRoslynSolutionLoader solutionLoader, DependencyGraphAnalysisEngine engine)
    {
        _solutionLoader = solutionLoader;
        _engine = engine;
    }

    public async Task<DependencyGraphReport> AnalyzeAsync(DependencyGraphOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            throw new InvalidCommandOptionsException("--solution is required.");
        }

        var fullSolutionPath = Path.GetFullPath(options.SolutionPath);
        if (!File.Exists(fullSolutionPath))
        {
            throw new InvalidCommandOptionsException($"Solution not found: {fullSolutionPath}");
        }

        if (!fullSolutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidCommandOptionsException($"Expected a .sln file: {fullSolutionPath}");
        }

        try
        {
            var solution = await _solutionLoader.LoadSolutionAsync(fullSolutionPath, ct);
            return await _engine.AnalyzeAsync(solution, fullSolutionPath, options.ExcludeTestProjects, ct);
        }
        catch (InvalidCommandOptionsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SolutionLoadException($"Failed to load or analyze solution '{fullSolutionPath}': {ex.Message}");
        }
    }
}
