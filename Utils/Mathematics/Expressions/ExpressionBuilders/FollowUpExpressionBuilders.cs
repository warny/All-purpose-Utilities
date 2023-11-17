using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions.ExpressionBuilders;

public class PlusOperatorBuilder() : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);

        // If either operand is of string type
        if (currentExpression.Type == typeof(string) || right.Type == typeof(string))
        {
            return BuildStringConcatenation(currentExpression, right);
        }
        else
        {
            (currentExpression, right) = parser.Options.AdjustNumberType(currentExpression, right);
            return Expression.Add(currentExpression, right);
        }
    }

    private static Expression BuildStringConcatenation(Expression currentExpression, Expression right)
    {
        var concat = typeof(string).GetRuntimeMethod("Concat", new Type[] { typeof(string[]) });

        IEnumerable<Expression> lefts;
        IEnumerable<Expression> rights;

        if (currentExpression is MethodCallExpression methodCallLeft && methodCallLeft.Method == concat)
        {
            lefts = ((NewArrayExpression)methodCallLeft.Arguments[0]).Expressions;
        } else {
            lefts = [currentExpression]; 
        }

        if (right is MethodCallExpression methodCallRight && methodCallRight.Method == concat)
        {
            rights = ((NewArrayExpression)methodCallRight.Arguments[0]).Expressions;
        }
        else
        {
            rights = [right];
        }

        // Call the string.Concat method
        return Expression.Call(
            concat,
            Expression.NewArrayInit(typeof(string), Enumerable.Concat(lefts, rights))
        );
    }
}


public class OperatorBuilder(Func<Expression, Expression, Expression> buildOperator, bool adjustNumberType) : IFollowUpExpressionBuilder
{
    private Func<Expression, Expression, Expression> BuildOperator => buildOperator;
    private bool AdjustNumberType => adjustNumberType;

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        if(AdjustNumberType) (currentExpression, right) = parser.Options.AdjustNumberType(currentExpression, right);
        return BuildOperator(currentExpression, right);
    }
}

public class MemberBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        string strMember = context.Tokenizer.ReadToken();
        return parser.GetExpression(context, currentExpression, strMember);
    }

}

public class NullOrMemberBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        context.PushContext();
        string strMember = context.Tokenizer.ReadToken();
        var variable = Expression.Variable(currentExpression.Type, $"<value{context.Depth}>");
        context.AddVariable(variable);
        var readExpression = parser.GetExpression(context, variable, strMember);

        var targetType
            = readExpression.Type.IsClass
            ? readExpression.Type
            : typeof(Nullable<>).MakeGenericType(readExpression.Type);

        var expression =
            Expression.Block(context.StackVariables,
                Expression.Condition(
                    Expression.Equal(
                        Expression.Assign(variable, currentExpression),
                        Expression.Constant(null, typeof(object))
                    ),
                    Expression.Convert(Expression.Constant(null), targetType),
                    Expression.Convert(readExpression, targetType),
                    targetType
                )
            );
        context.PopContext();
        return expression;
    }

}

public class TypeMatchBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Type t = parser.ReadType(context, null);
        return Expression.TypeIs(currentExpression, t);
    }
}

public class AddAssignationBuilder() : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        if (currentExpression.Type == typeof(string))
        {
            return BuildStringConcatenation(currentExpression, right);
        }

        right = parser.Options.AdjustNumberType(currentExpression.Type, right);
        return Expression.AddAssign(currentExpression, right);
    }

    private static Expression BuildStringConcatenation(Expression left, Expression right)
    {
        var concat = typeof(string).GetRuntimeMethod("Concat", new Type[] { typeof(string[]) });

        IEnumerable<Expression> rights;

        if (right is MethodCallExpression methodCallRight && methodCallRight.Method == concat)
        {
            rights = ((NewArrayExpression)methodCallRight.Arguments[0]).Expressions;
        }
        else
        {
            rights = [right];
        }

        // Call the string.Concat method
        return Expression.Assign(left, 
            Expression.Call(
                concat,
                Expression.NewArrayInit(typeof(string), Enumerable.Concat([left], rights))
            )
        );
    }

}


public class AssignationBuilder(Func<Expression, Expression, BinaryExpression> buildOperator) : IFollowUpExpressionBuilder
{
    private Func<Expression, Expression, BinaryExpression> BuildOperator => buildOperator;

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        right = parser.Options.AdjustNumberType(currentExpression.Type, right);
        return BuildOperator(currentExpression, right);
    }
}

public class PostOperationBuilder(Func<Expression, UnaryExpression> buildOperator) : IFollowUpExpressionBuilder
{
    private Func<Expression, UnaryExpression> BuildOperator => buildOperator;

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        return BuildOperator(currentExpression);
    }
}

public class TypeCastBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        Type t = parser.ReadType(context, null);
        return Expression.TypeAs(currentExpression, t);
    }
}

public class ElseBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, WrapMarkers markers, ref bool isClosedWrap)
    {
        if (currentExpression is not ConditionalExpression ce) throw new ParseWrongSymbolException(nextVal, val, context.Tokenizer.Position.Index);
        Expression falseExpression = parser.ReadExpression(context, 0, null, out _);
        return Expression.IfThenElse(ce.Test, ce.IfTrue, falseExpression);
    }
}
