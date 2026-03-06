using RefactorCli.Abstractions;
using RefactorCli.Commands.DeadCode.Configuration;
using RefactorCli.Commands.DeadCode.Contracts;
using RefactorCli.Infrastructure;

namespace RefactorCli.Commands.DeadCode.Analysis;

public sealed class DeadCodeService
{
    private readonly MSBuildRoslynSolutionLoader _solutionLoader;
    private readonly DeadCodeAnalysisEngine _engine;

    public DeadCodeService(MSBuildRoslynSolutionLoader solutionLoader, DeadCodeAnalysisEngine engine)
    {
        _solutionLoader = solutionLoader;
        _engine = engine;
    }

    public async Task<DeadCodeReport> AnalyzeAsync(DeadCodeOptions options, DeadCodeConfig config, CancellationToken ct)
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
            return await _engine.AnalyzeAsync(solution, fullSolutionPath, options, config, ct);
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
