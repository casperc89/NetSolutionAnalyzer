using Microsoft.CodeAnalysis;
using RefactorCli.Abstractions;

namespace RefactorCli.Analysis.Roslyn;

public sealed class CatalogAccumulator
{
    private readonly List<Finding> _findings = [];
    private readonly HashSet<string> _dedupe = new(StringComparer.Ordinal);

    public IReadOnlyList<Finding> Findings => _findings;

    public void Add(
        string id,
        string category,
        string severity,
        string message,
        string filePath,
        int? line = null,
        int? column = null,
        string? symbol = null,
        string? snippet = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var key = $"{id}|{filePath}|{line}|{column}|{symbol}";
        if (!_dedupe.Add(key))
        {
            return;
        }

        _findings.Add(new Finding
        {
            Id = id,
            Category = category,
            Severity = severity,
            Message = message,
            FilePath = filePath,
            Line = line,
            Column = column,
            Symbol = symbol,
            Snippet = snippet,
            Properties = properties
        });
    }

    public static (int? line, int? column) GetLineAndColumn(Location? location)
    {
        if (location is null || !location.IsInSource)
        {
            return (null, null);
        }

        var span = location.GetLineSpan();
        return (span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
    }
}
