using Microsoft.CodeAnalysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analysis;

internal static class RoslynSymbolHelpers
{
    public static bool IsSystemWebSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

        var ns = symbol.ContainingNamespace?.ToDisplayString();
        return ns is not null && (ns.Equals("System.Web", StringComparison.Ordinal) || ns.StartsWith("System.Web.", StringComparison.Ordinal));
    }
}
