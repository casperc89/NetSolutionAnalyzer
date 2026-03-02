using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCli.Commands.SystemWebCatalog.Analyzers;

internal static class SessionAccessAnalyzerHelpers
{
    internal const string DynamicSessionKey = "<dynamic>";

    public static bool IsSessionIndexerAccess(
        ElementAccessExpressionSyntax elementAccess,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string symbol)
    {
        var expressionType = ResolveExpressionType(elementAccess.Expression, semanticModel, ct);
        if (!IsSessionLikeType(expressionType))
        {
            symbol = string.Empty;
            return false;
        }

        var typeName = TrimGlobalPrefix(expressionType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        symbol = $"{typeName}.this[]";
        return true;
    }

    public static bool IsSessionRead(ElementAccessExpressionSyntax elementAccess)
    {
        if (elementAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == elementAccess)
        {
            return !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
        }

        return true;
    }

    public static bool IsSessionWrite(ElementAccessExpressionSyntax elementAccess)
    {
        return elementAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == elementAccess;
    }

    public static string GetSessionKey(ElementAccessExpressionSyntax elementAccess, SemanticModel semanticModel, CancellationToken ct)
    {
        var keyExpression = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (keyExpression is null)
        {
            return DynamicSessionKey;
        }

        var constant = semanticModel.GetConstantValue(keyExpression, ct);
        if (constant.HasValue && constant.Value is string literalKey)
        {
            return literalKey;
        }

        return DynamicSessionKey;
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

    internal static bool IsSessionLikeType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        var ns = type.ContainingNamespace?.ToDisplayString();
        if (type.Name.Equals("HttpSessionState", StringComparison.Ordinal) &&
            ns is not null &&
            (ns.Equals("System.Web", StringComparison.Ordinal) ||
             ns.Equals("System.Web.SessionState", StringComparison.Ordinal)))
        {
            return true;
        }

        if (type.Name.Equals("HttpSessionStateBase", StringComparison.Ordinal) &&
            ns is not null &&
            ns.Equals("System.Web", StringComparison.Ordinal))
        {
            return true;
        }

        return type.Name.Equals("SessionStateItemCollection", StringComparison.Ordinal) &&
               ns is not null &&
               ns.Equals("System.Web.SessionState", StringComparison.Ordinal);
    }

    private static string TrimGlobalPrefix(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value["global::".Length..]
            : value;
    }
}
