using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions.ExpressionBuilders;

public class ThrowParseException : IStartExpressionBuilder, IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        throw new ParseUnknownException(".", context.Tokenizer.Position.Index);
    }

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        throw new ParseUnknownException(nextVal, context.Tokenizer.Position.Index);
    }
}
