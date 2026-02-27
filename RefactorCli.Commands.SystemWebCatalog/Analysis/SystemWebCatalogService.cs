using RefactorCli.Abstractions;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class SystemWebCatalogService
{
    private readonly MSBuildRoslynSolutionLoader _solutionLoader;
    private readonly CatalogEngine _engine;
    private readonly IFileSystem _fileSystem;

    public SystemWebCatalogService(
        MSBuildRoslynSolutionLoader solutionLoader,
        CatalogEngine engine,
        IFileSystem fileSystem)
    {
        _solutionLoader = solutionLoader;
        _engine = engine;
        _fileSystem = fileSystem;
    }

    public async Task<CatalogReport> AnalyzeAsync(SystemWebCatalogOptions options, CancellationToken ct)
    {
        var solutionPath = _fileSystem.GetFullPath(options.SolutionPath);
        if (!_fileSystem.FileExists(solutionPath))
        {
            throw new InvalidCommandOptionsException($"Solution not found: {solutionPath}");
        }

        var solution = await _solutionLoader.LoadSolutionAsync(solutionPath, ct);
        if (!solution.Projects.Any())
        {
            throw new SolutionLoadException($"No projects were loaded from solution: {solutionPath}");
        }

        return await _engine.AnalyzeAsync(solution, solutionPath, ct);
    }
}
