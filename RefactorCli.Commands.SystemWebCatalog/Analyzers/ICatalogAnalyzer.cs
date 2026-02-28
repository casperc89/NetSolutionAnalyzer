using Microsoft.CodeAnalysis;
using RefactorCli.Commands.SystemWebCatalog.Contracts;
using RefactorCli.Commands.SystemWebCatalog.Analysis;
using RefactorCli.Commands.SystemWebCatalog.Analysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

public interface ICatalogAnalyzer
{
    CatalogRuleDescriptor Descriptor { get; }

    Task AnalyzeAsync(Project project, CatalogAccumulator acc, CancellationToken ct);
}
