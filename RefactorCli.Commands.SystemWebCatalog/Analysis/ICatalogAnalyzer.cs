using Microsoft.CodeAnalysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public interface ICatalogAnalyzer
{
    string Id { get; }

    Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct);
}
