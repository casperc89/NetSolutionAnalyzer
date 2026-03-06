using RefactorCli.Abstractions;
using RefactorCli.Commands.MethodDependency.Contracts;
using RefactorCli.Infrastructure;

namespace RefactorCli.Commands.MethodDependency.Analysis;

public sealed class MethodDependencyService
{
    private readonly MSBuildRoslynSolutionLoader _solutionLoader;
    private readonly MethodDependencyAnalysisEngine _engine;

    public MethodDependencyService(MSBuildRoslynSolutionLoader solutionLoader, MethodDependencyAnalysisEngine engine)
    {
        _solutionLoader = solutionLoader;
        _engine = engine;
    }

    public async Task<MethodDependencyReport> AnalyzeAsync(MethodDependencyOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            throw new InvalidCommandOptionsException("--solution is required.");
        }

        if (string.IsNullOrWhiteSpace(options.FilePath))
        {
            throw new InvalidCommandOptionsException("--file is required.");
        }

        if (options.Line <= 0)
        {
            throw new InvalidCommandOptionsException("--line must be greater than zero.");
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

        var fullFilePath = Path.GetFullPath(options.FilePath);

        try
        {
            var solution = await _solutionLoader.LoadSolutionAsync(fullSolutionPath, ct);
            return await _engine.AnalyzeAsync(solution, fullSolutionPath, fullFilePath, options.Line, ct);
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
