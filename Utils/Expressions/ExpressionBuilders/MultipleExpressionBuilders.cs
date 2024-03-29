﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions.ExpressionBuilders;

public class BracketBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        Parenthesis brackets = new("[", "]", ",");
        // Indexer access
        if (currentExpression.Type.IsArray)
        {
            var result = Expression.ArrayIndex(currentExpression, parser.Options.AdjustType(parser.ReadExpression(context, 0, brackets, out _), typeof(int)));
            string token = context.Tokenizer.ReadToken();
            if (token != brackets.End) throw new ParseUnknownException(token, context.Tokenizer.Position.Index);
            return result;
        }

        DefaultMemberAttribute[] atts = currentExpression.GetType().GetTypeInfo().GetCustomAttributes<DefaultMemberAttribute>().ToArray();
        DefaultMemberAttribute indexerNameAtt = atts.SingleOrDefault();
        if (indexerNameAtt == null) return currentExpression;

        string indexerName = indexerNameAtt.MemberName;

        PropertyInfo propertyInfo = currentExpression.Type.GetRuntimeProperty(indexerName);
        MethodInfo methodInfo = propertyInfo.GetMethod;

        // Get parameters
        var listParam = parser.ReadExpressions(context, brackets);

        return Expression.Call(currentExpression, methodInfo, listParam);
    }
}

public class RightParenthesisBuilder : IFollowUpExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        // Indexer access
        if (typeof(Delegate).IsAssignableFrom(currentExpression.Type))
        {
            var method = currentExpression.Type.GetMethod("Invoke");
            var listArguments = parser.ReadExpressions(context, new Parenthesis("(", ")", ";"), false);
            var methodAndParameters = parser.Resolver.SelectMethod([method], currentExpression, null, listArguments);

            if (methodAndParameters is not null)
            {
                return Expression.Call(currentExpression, methodAndParameters?.Method, methodAndParameters?.Parameters);
            }

            throw new ParseWrongSymbolException(nextVal, val, context.Tokenizer.Position.Index);
        }
        else
        {
            parser.Builder.FallbackBinaryOrTernaryBuilder.Build(parser, context, currentExpression, val, nextVal, priorityLevel, ref nextLevel, markers, ref isClosedWrap);
            return null;
        }
    }
}

public class CloseBuilder : IFollowUpExpressionBuilder
{
    public CloseBuilder(string wrapStart)
    {
        this.WrapStart = wrapStart;
    }

    private string WrapStart { get; }

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        if (WrapStart != this.WrapStart)
        {
            throw new ParseUnmatchException(WrapStart, nextVal, context.Tokenizer.Position.Index);
        }
        isClosedWrap = true;
        return currentExpression;
    }
}

public class ConditionalBuilder : IFollowUpExpressionBuilder, IAdditionalTokens
{
    public ConditionalBuilder(string choiceSymbol)
    {
        this.ChoiceSymbol = choiceSymbol;
    }

    public IEnumerable<string> AdditionalTokens => [ChoiceSymbol];

    public string ChoiceSymbol { get; }

    public Expression Build(ExpressionParserCore parser, ParserContext context, Expression currentExpression, string val, string nextVal, int priorityLevel, ref int nextLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        Expression first = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        context.Tokenizer.ReadSymbol(ChoiceSymbol);
        Expression second = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        return Expression.Condition(currentExpression, first, second);
    }
}
