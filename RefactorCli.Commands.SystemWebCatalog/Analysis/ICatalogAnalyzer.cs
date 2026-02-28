using Microsoft.CodeAnalysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

public interface ICatalogAnalyzer
{
    CatalogRuleDescriptor Descriptor { get; }

    Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct);
}
