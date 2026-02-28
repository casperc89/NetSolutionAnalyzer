namespace RefactorCli.Commands.SystemWebCatalog.Contracts;

public sealed class CatalogRuleDescriptor
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string WhatItDetects { get; init; }

    public required string WhyItMatters { get; init; }
}
