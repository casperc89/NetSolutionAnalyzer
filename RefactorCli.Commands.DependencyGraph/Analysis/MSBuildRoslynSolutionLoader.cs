using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace RefactorCli.Commands.DependencyGraph.Analysis;

public sealed class MSBuildRoslynSolutionLoader
{
    private static readonly object RegistrationLock = new();
    private static bool _isRegistered;
    private readonly ILogger<MSBuildRoslynSolutionLoader> _logger;

    public MSBuildRoslynSolutionLoader(ILogger<MSBuildRoslynSolutionLoader> logger)
    {
        _logger = logger;
    }

    public async Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct)
    {
        EnsureMSBuildRegistered();

        var workspace = MSBuildWorkspace.Create();
#pragma warning disable CS0618
        workspace.WorkspaceFailed += (_, args) =>
        {
            _logger.LogWarning("Workspace warning: {Message}", args.Diagnostic.Message);
        };
#pragma warning restore CS0618

        return await workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct);
    }

    private static void EnsureMSBuildRegistered()
    {
        if (_isRegistered)
        {
            return;
        }

        lock (RegistrationLock)
        {
            if (_isRegistered)
            {
                return;
            }

            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            _isRegistered = true;
        }
    }
}
