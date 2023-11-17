using System.Linq.Expressions;

namespace Utils.Expressions
{
    public interface IStartExpressionBuilder
    {
        Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap);
    }

    public interface IFollowUpExpressionBuilder
    {
        Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, Parenthesis markers, ref bool isClosedWrap);
    }

}
