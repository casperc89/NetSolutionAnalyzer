using RefactorCli.Abstractions;
using RefactorCli.Abstractions.SystemWebCatalog;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public sealed class SystemWebCatalogService : ISystemWebCatalogService
{
    private readonly IRoslynSolutionLoader _solutionLoader;
    private readonly ICatalogEngine _engine;
    private readonly IFileSystem _fileSystem;

    public SystemWebCatalogService(
        IRoslynSolutionLoader solutionLoader,
        ICatalogEngine engine,
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
