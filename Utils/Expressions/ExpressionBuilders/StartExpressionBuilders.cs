using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Expressions.ExpressionBuilders;

public class NumberConstantBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        if (val.Length > 2 && val[0] == 0 && char.IsLetter(val[1]))
        {
            if (!parser.Builder.IntegerPrefixes.TryGetValue(val[..2], out int @base)) throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);
            return (Expression.Constant(Convert.ToUInt64(val[2..], @base)));
        }

        // Parse numeric value
        char lastChar = val[^1];
        if (char.IsDigit(lastChar))
        {
            if (val.Contains('.'))
            {
                return Expression.Constant(double.Parse(val));
            }

            var constVal = long.Parse(val);
            if (constVal > int.MaxValue || constVal < int.MinValue) return Expression.Constant(constVal);
            if (constVal > short.MaxValue || constVal < short.MinValue) return Expression.Constant((int)constVal);
            if (constVal > byte.MaxValue || constVal < byte.MinValue) return Expression.Constant((short)constVal);
            return Expression.Constant((byte)constVal);
        }

        if (parser.Options.NumberSuffixes.TryGetValue(lastChar.ToString(), out var converter))
        {
            return Expression.Constant(converter(val[..^1]));
        }

        return null;
    }
}

public class NullBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap) => Expression.Constant(null);
}

public class TrueBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap) => Expression.Constant(true);
}

public class FalseBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap) => Expression.Constant(false);
}

public class SizeOfBuilder : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => ["(", ")"];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        context.Tokenizer.ReadSymbol("(");
        Type type = parser.ReadType(context, null);
        context.Tokenizer.ReadSymbol(")");
        return Expression.Constant(System.Runtime.InteropServices.Marshal.SizeOf(type));
    }
}

public class TypeofBuilder : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => ["(", ")"];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        // string str = GetBracketString(false);
        context.Tokenizer.ReadSymbol("(");
        Type type = parser.ReadType(context, null);
        context.Tokenizer.ReadSymbol(")");

        return Expression.Constant(type, typeof(Type));
    }
}

public class NewBuilder : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => ["=>", "{", "}", "[", "]", "<", ">"];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        Expression currentExpression;
        // Get the type
        Type type = parser.ReadType(context, context.Tokenizer.ReadToken());

        // Check if it's an array
        var arrayParam = parser.ReadExpressions(context, new Parenthesis("[", "]", ","));
        var listParam = parser.ReadExpressions(context, new Parenthesis("(", ")", ","));

        if (listParam != null)
        {
            // Get the constructor
            var constructors = parser.Resolver.GetConstructors(type);
            var constructor = parser.Resolver.SelectConstructor(constructors, listParam);
            currentExpression = Expression.New(constructor?.Method, constructor?.Parameters);

            if (context.Tokenizer.PeekToken() != "{") return currentExpression;

            // Member initialization or collection initialization
            context.Tokenizer.ReadToken();

            // Determine whether it's member initialization or collection initialization
            context.Tokenizer.PushPosition();
            string str = context.Tokenizer.ReadToken();
            if (str == "}") return currentExpression;
            bool isMemberInit = context.Tokenizer.ReadToken() == "=";
            context.Tokenizer.PopPosition();

            // Member initialization
            if (isMemberInit)
            {
                List<MemberBinding> listMemberBinding = new List<MemberBinding>();
                string memberName;
                while ((memberName = context.Tokenizer.ReadToken()) != "}")
                {
                    context.Tokenizer.ReadSymbol("=");

                    MemberInfo memberInfo = parser.Resolver.GetInstancePropertyOrField(type, memberName);
                    MemberBinding memberBinding = Expression.Bind(memberInfo, parser.ReadExpression(context, 0, markers, out isClosedWrap));
                    listMemberBinding.Add(memberBinding);

                    // Comma
                    string comma = context.Tokenizer.ReadToken();
                    if (comma == "}")
                    {
                        break;
                    }
                    ParseException.Assert(comma, ",", context.Tokenizer.Position.Index);
                }
                return Expression.MemberInit((NewExpression)currentExpression, listMemberBinding);
            }
            // Collection initialization
            else
            {
                List<Expression> listExpression = [];
                while (true)
                {
                    listExpression.Add(parser.ReadExpression(context, 0, markers, out isClosedWrap));

                    // Comma
                    string comma = context.Tokenizer.ReadToken();
                    if (comma == "}")
                    {
                        break;
                    }
                    ParseException.Assert(comma, ",", context.Tokenizer.Position.Index);
                }
                return Expression.ListInit((NewExpression)currentExpression, listExpression);
            }
        }
        else if (arrayParam!= null)
        {
            // Read array initialization inside {}
            var initValues = parser.ReadExpressions(context, new Parenthesis("{", "}", ","));
            if (initValues != null)
            {
                currentExpression = Expression.NewArrayInit(type, initValues);
            }
            else
            {
                currentExpression = Expression.NewArrayBounds(type, arrayParam);
            }
        }
        else
        {
            throw new ParseUnknownException(context.Tokenizer.PeekToken(), context.Tokenizer.Position.Index);
        }

        return currentExpression;
    }

    public class ReadNextExpressionBuilder : IStartExpressionBuilder
    {
        public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
            => parser.ReadExpression(context, priorityLevel, markers, out isClosedWrap);
    }

    public class UnaryMinusBuilder : IStartExpressionBuilder
    {
        public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap) {
            var Expression = parser.ReadExpression(context, parser.Options.GetOperatorLevel(val, true), markers, out _);

            if (Expression is ConstantExpression constantExpression && parser.Options.NumberTypeLevel.ContainsKey(Expression.Type))
            {
                object newValue = constantExpression.Value switch
                {
                    double d => -d,
                    float s => -s,
                    decimal d => -d,
                    long l => -l,
                    uint i => -i,
                    int i => -i,
                    short s => -s,
                    ushort s => -s,
                    sbyte s => -s,
                    BigInteger bi => -bi,
                    byte bi => -bi,
                    _ => throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index)
                };
                return Expression.Constant(newValue);
            }

            return Expression.Negate(Expression);
        }
    }
}

public class UnaryOperandBuilder(Func<Expression, UnaryExpression> buildOperator) : IStartExpressionBuilder
{
    public Func<Expression, UnaryExpression> OperatorBuilder => buildOperator;

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
        => OperatorBuilder(parser.ReadExpression(context, parser.Options.GetOperatorLevel(val, true), markers, out isClosedWrap));
}

public class ParenthesisBuilder(string closeParenthesis, string separator) : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => [closeParenthesis, separator];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var parenthesis = new Parenthesis(val, closeParenthesis, separator);

        context.Tokenizer.PushPosition();
        string str = ParserExtensions.GetBracketString(context, parenthesis, true);
        Type type = parser.GetType(context, str);

        // Found a type, treat it as a type conversion
        if (type != null)
        {
            context.Tokenizer.DiscardPosition();
            return Expression.Convert(parser.ReadExpression(context, parser.Options.GetOperatorLevel("convert()", true), parenthesis, out isClosedWrap), type);
        }
        // Type not found, use it for prioritization only
        else
        {
            context.Tokenizer.PopPosition();
            var result = parser.ReadExpression(context, 0, parenthesis, out _);
            context.Tokenizer.ReadSymbol(parenthesis.End);
            // Allocate a new isClosedWrap variable
            return result;
        }
    }
}

public class BlockBuilder(string closeParenthesis, string separator) : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => [closeParenthesis, separator];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var blockMarkers = new Parenthesis(val, closeParenthesis, separator);

        context.PushContext();
        var expressions = parser.ReadExpressions(context, blockMarkers, false);
        var variables = context.StackVariables;
        context.PopContext();
        return Expression.Block(variables, expressions);
    }
}

public class DefaultUnaryBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var firstChar = val[0];

        // The first character is a letter, underscore, or dollar sign
        if (val.IsName())
        {
            return ReadName(parser, context, val);
        }
        // The first character is not a letter, underscore, or dollar sign
        else
        {
            return ReadStringOrChar(context, val, firstChar);
        }
    }

    private static Expression ReadStringOrChar(ParserContext context, string val, char firstChar)
    {
        return firstChar switch
        {
            '\"' or '@' => Expression.Constant(context.Tokenizer.DefineString),
            '\'' => Expression.Constant(context.Tokenizer.DefineString[0]),
            _ => throw new ParseUnknownException(val, context.Tokenizer.Position.Index),
        };
    }

    private static Expression ReadName(ExpressionParserCore parser, ParserContext context, string val)
    {
        // Parameter or class or default instance property
        if (context.TryFindVariable(val, out ParameterExpression parameter)) return parameter;
        if (parser.Resolver.TryGetConstant(val, out ConstantExpression constant)) return constant;

        // Default instance method call
        if (context.DefaultInstanceParam != null)
        {
            var expression = parser.GetExpression(context, context.DefaultInstanceParam, val);
            if (expression != null) return expression;
        }

        // default static method call
        if (context.DefaultInstanceType != null)
        {
            var expression = parser.GetExpression(context, context.DefaultInstanceType, val);
            if (expression != null) return expression;
        }

        // Class
        Type type = parser.ReadType(context, val);

        var token = context.Tokenizer.PeekToken();

        if (token == ".")
        {
            context.Tokenizer.ReadSymbol(".");
            string strMember = context.Tokenizer.ReadToken();
            {
                var expression = parser.GetExpression(context, type, strMember);
                if (expression != null) return expression;
            }
            throw new ParseUnknownException(strMember, context.Tokenizer.Position.Index);
        }

        if (token.IsName())
        {
            context.Tokenizer.ReadToken();
            var newVariable = Expression.Variable(type, token);
            if (!context.AddVariable(newVariable))
            {
                throw new ParseDuplicateParameterNameException(token, context.Tokenizer.Position.Index);
            }
            return newVariable;
        }
        throw new ParseWrongSymbolException("", token, context.Tokenizer.Position.Index);
    }
}

public class IfBuilder(string elseKeyword) : IStartExpressionBuilder, IAdditionalTokens
{
    public IEnumerable<string> AdditionalTokens => [elseKeyword];

    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        context.Tokenizer.ReadSymbol("(");
        var testExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", null), out _);
        context.Tokenizer.ReadSymbol(")");
        var ifTrueExpression = parser.ReadExpression(context, 0, null, out _);
        context.Tokenizer.PushPosition();
        if (ifTrueExpression is not BlockExpression) context.Tokenizer.ReadSymbol(";");
        var nextToken = context.Tokenizer.ReadToken();
        if (nextToken == elseKeyword)
        {
            context.Tokenizer.DiscardPosition();
            var ifFalseExpression = parser.ReadExpression(context, 0, null, out _);
            return Expression.IfThenElse(testExpression, ifTrueExpression, ifFalseExpression);
        }
        context.Tokenizer.PopPosition();
        return Expression.IfThen(testExpression, ifTrueExpression);
    }
}

public class BreakBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var target = context.BreakLabel;
        if (target == null) throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);
        return Expression.Break(target);
    }
}

public class ContinueBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var target = context.ContinueLabel;
        if (target == null) throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);
        return Expression.Break(target);
    }
}

public class WhileBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var continueLabel = Expression.Label();
        var breakLabel = Expression.Label();
        context.PushContext(continueLabel, breakLabel);
        context.Tokenizer.ReadSymbol("(");
        var testExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", null), out _);
        context.Tokenizer.ReadSymbol(")");
        var loopExpression = parser.ReadExpression(context, 0, null, out _);

        var result = Expression.Block(
            context.StackVariables,
            Expression.Loop(
                Expression.Block(
                    Expression.IfThen(Expression.Not(testExpression), Expression.Break(breakLabel)),
                    loopExpression
                ), 
                breakLabel, continueLabel
            )
        );

        context.PopContext();        
        return result;
    }
}

public class ForBuilder : IStartExpressionBuilder
{
    public Expression Build(ExpressionParserCore parser, ParserContext context, string val, int priorityLevel, Parenthesis markers, ref bool isClosedWrap)
    {
        var continueLabel = Expression.Label();
        var breakLabel = Expression.Label();
        context.PushContext(continueLabel, breakLabel);
        var forExpression = parser.ReadExpressions(context, new Parenthesis("(", ")", ";"), true, true);
        var initializer = forExpression[0];
        var test = forExpression[1];
        var loop = forExpression[2..];

        var loopExpression = parser.ReadExpression(context, 0, null, out _);

        var result = Expression.Block(
            context.StackVariables,
            [
                initializer,
                Expression.Loop(
                    Expression.Block(
                        new Expression[] {
                            Expression.IfThen(Expression.Not(test), Expression.Break(breakLabel)),
                            loopExpression
                        }.Union(loop)
                    ),
                    breakLabel, continueLabel
                )
            ]
        );

        context.PopContext();
        return result;
    }
}
