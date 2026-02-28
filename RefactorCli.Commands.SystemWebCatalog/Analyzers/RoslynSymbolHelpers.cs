using Microsoft.CodeAnalysis;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

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

    public static bool IsSourceDeclaredSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

        return symbol.Locations.Any(location => location.IsInSource);
    }

    public static bool IsNoiseSymbolKind(ISymbol? symbol)
    {
        return symbol is ILocalSymbol
            or IParameterSymbol
            or IRangeVariableSymbol
            or ILabelSymbol
            or ITypeParameterSymbol;
    }
}
