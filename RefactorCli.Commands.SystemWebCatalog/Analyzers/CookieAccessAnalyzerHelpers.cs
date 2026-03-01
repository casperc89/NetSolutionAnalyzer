using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

internal static class CookieAccessAnalyzerHelpers
{
    internal const string DynamicCookieKey = "<dynamic>";

    public static bool IsCookieIndexerAccess(
        ElementAccessExpressionSyntax elementAccess,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string symbol)
    {
        var expressionType = ResolveExpressionType(elementAccess.Expression, semanticModel, ct);
        if (!IsCookieCollectionType(expressionType))
        {
            symbol = string.Empty;
            return false;
        }

        var typeName = TrimGlobalPrefix(expressionType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        symbol = $"{typeName}.this[]";
        return true;
    }

    public static bool IsCookieRead(ElementAccessExpressionSyntax elementAccess)
    {
        if (elementAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == elementAccess)
        {
            return !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
        }

        return true;
    }

    public static bool IsCookieWrite(ElementAccessExpressionSyntax elementAccess)
    {
        return elementAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == elementAccess;
    }

    public static string GetCookieKey(ElementAccessExpressionSyntax elementAccess, SemanticModel semanticModel, CancellationToken ct)
    {
        var keyExpression = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (keyExpression is null)
        {
            return DynamicCookieKey;
        }

        var constant = semanticModel.GetConstantValue(keyExpression, ct);
        if (constant.HasValue && constant.Value is string key)
        {
            return key;
        }

        return DynamicCookieKey;
    }

    private static ITypeSymbol? ResolveExpressionType(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
    {
        var symbol = GetSymbol(semanticModel, expression, ct);
        switch (symbol)
        {
            case ILocalSymbol local:
                return local.Type;
            case IParameterSymbol parameter:
                return parameter.Type;
            case IFieldSymbol field:
                return field.Type;
            case IPropertySymbol property:
                return property.Type;
            case IMethodSymbol method:
                return method.ReturnType;
        }

        return semanticModel.GetTypeInfo(expression, ct).Type;
    }

    private static ISymbol? GetSymbol(SemanticModel semanticModel, SyntaxNode node, CancellationToken ct)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node, ct);
        return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
    }

    private static bool IsCookieCollectionType(ITypeSymbol? type)
    {
        return type is not null &&
               type.Name.Equals("HttpCookieCollection", StringComparison.Ordinal) &&
               type.ContainingNamespace?.ToDisplayString().Equals("System.Web", StringComparison.Ordinal) == true;
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }
}
