using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Net.DNS;

internal static class DNSExpression
{
    public static Expression BuildExpression(Expression element, string expression)
    {
        if (element is ParameterExpression pe)
        {
            return ExpressionParser.ParseExpression(expression, [pe], null, true, ["Utils.Net.DNS"]);
        }
        else
        {
            var variable = Expression.Variable(element.Type, "<default>");
            return Expression.Block([variable],
                Expression.Assign(variable, element),
                ExpressionParser.ParseExpression(expression, [variable], null, true, ["Utils.Net.DNS"])
            );
        }
    }
}
