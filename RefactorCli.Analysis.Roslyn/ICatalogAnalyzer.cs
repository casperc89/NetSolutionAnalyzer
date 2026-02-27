using Microsoft.CodeAnalysis;

namespace RefactorCli.Analysis.Roslyn;

public interface ICatalogAnalyzer
{
    string Id { get; }

    Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct);
}
