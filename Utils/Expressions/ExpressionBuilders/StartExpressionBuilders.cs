using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Reflection;
using Utils.String;

namespace Utils.Expressions.ExpressionBuilders
{
    /// <summary>
    /// Builds an expression representing a numeric constant.
    /// Handles base prefixes (e.g., "0x" for hex), numeric suffixes (e.g., "f" for float),
    /// and direct numeric literals, returning the corresponding <see cref="ConstantExpression"/>.
    /// </summary>
    public class NumberConstantBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            // Check for base prefix (e.g., 0x..., 0b..., etc.)
            if (val.Length > 2 && val[0] == '0' && char.IsLetter(val[1]))
            {
                if (!parser.Builder.IntegerPrefixes.TryGetValue(val[..2], out int @base))
                {
                    throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);
                }
                return Expression.Constant(Convert.ToUInt64(val[2..], @base));
            }

            // If the last character is a digit
            char lastChar = val[^1];
            if (char.IsDigit(lastChar))
            {
                // If there's a decimal point, parse as double
                if (val.Contains('.'))
                {
                    return Expression.Constant(double.Parse(val));
                }

                // Otherwise, parse as long and then downcast if possible
                var constVal = long.Parse(val);
                if (constVal > int.MaxValue || constVal < int.MinValue)
                {
                    return Expression.Constant(constVal);
                }
                if (constVal > short.MaxValue || constVal < short.MinValue)
                {
                    return Expression.Constant((int)constVal);
                }
                if (constVal > byte.MaxValue || constVal < byte.MinValue)
                {
                    return Expression.Constant((short)constVal);
                }
                return Expression.Constant((byte)constVal);
            }

            // Check numeric suffix (e.g., "f" => float, "d" => double, etc.)
            if (parser.Options.NumberSuffixes.TryGetValue(lastChar.ToString(), out var converter))
            {
                return Expression.Constant(converter(val[..^1]));
            }

            // If no known numeric pattern matches, return null
            return null;
        }
    }

    /// <summary>
    /// Builds an expression representing <see langword="null"/>.
    /// </summary>
    public class NullBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
            => Expression.Constant(null);
    }

    /// <summary>
    /// Builds an expression representing <see langword="true"/>.
    /// </summary>
    public class TrueBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
            => Expression.Constant(true);
    }

    /// <summary>
    /// Builds an expression representing <see langword="false"/>.
    /// </summary>
    public class FalseBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
            => Expression.Constant(false);
    }

    /// <summary>
    /// Builds an expression for <c>sizeof(T)</c>, retrieving the size of the specified type
    /// via <see cref="System.Runtime.InteropServices.Marshal.SizeOf(Type)"/>.
    /// </summary>
    public class SizeOfBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            context.Tokenizer.ReadSymbol("(");
            var type = parser.ReadType(context, null);
            context.Tokenizer.ReadSymbol(")");
            return Expression.Constant(System.Runtime.InteropServices.Marshal.SizeOf(type));
        }
    }

    /// <summary>
    /// Builds an expression for <c>typeof(T)</c>, returning a constant <see cref="Type"/> object.
    /// </summary>
    public class TypeofBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            context.Tokenizer.ReadSymbol("(");
            Type type = parser.ReadType(context, null);
            context.Tokenizer.ReadSymbol(")");
            return Expression.Constant(type, typeof(Type));
        }
    }

    /// <summary>
    /// Builds a <c>new</c> expression for either an object, array, or collection/member initialization.
    /// </summary>
    public class NewBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["=>", "{", "}", "[", "]", "<", ">"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            Expression currentExpression;

            // First, parse the type name after "new"
            Type type = parser.ReadType(context, context.Tokenizer.ReadToken());

            // Check for an array parameter or a constructor parameter
            var arrayParam = parser.ReadExpressions(context, new Parenthesis("[", "]", ","));
            var listParam = parser.ReadExpressions(context, new Parenthesis("(", ")", ","), true, true);

            // If constructor usage
            if (listParam != null)
            {
                var constructors = parser.Resolver.GetConstructors(type);
                var constructor = parser.Resolver.SelectConstructor(constructors, listParam);
                currentExpression = Expression.New(constructor?.Method, constructor?.Parameters);

                // If there's a brace, we might have member or collection initialization
                if (context.Tokenizer.PeekToken() != "{")
                    return currentExpression;

                context.Tokenizer.ReadToken(); // consume '{'

                // Check if next token indicates member or collection init
                context.Tokenizer.PushPosition();
                string str = context.Tokenizer.ReadToken();
                if (str == "}")
                {
                    // no init block
                    return currentExpression;
                }
                bool isMemberInit = context.Tokenizer.ReadToken() == "=";
                context.Tokenizer.PopPosition();

                // Member initialization
                if (isMemberInit)
                {
                    var listMemberBinding = new List<MemberBinding>();
                    string memberName;
                    while ((memberName = context.Tokenizer.ReadToken()) != "}")
                    {
                        context.Tokenizer.ReadSymbol("=");

                        var memberInfo = parser.Resolver.GetInstancePropertyOrField(type, memberName);
                        var bindingExpression = parser.ReadExpression(context, 0, markers, out isClosedWrap);
                        var memberBinding = Expression.Bind(memberInfo, bindingExpression);
                        listMemberBinding.Add(memberBinding);

                        // Comma or curly brace
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
                    var listExpression = new List<Expression>();
                    while (true)
                    {
                        listExpression.Add(parser.ReadExpression(context, 0, markers, out isClosedWrap));

                        // Comma or curly brace
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
            else if (arrayParam != null)
            {
                // This is an array creation
                var initValues = parser.ReadExpressions(context, new Parenthesis("{", "}", ","), true);
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

        /// <summary>
        /// Builds an expression by reading the next expression from the parser at the given priority.
        /// Useful for sub-scenarios within "new" statements.
        /// </summary>
        public class ReadNextExpressionBuilder : IStartExpressionBuilder
        {
            /// <inheritdoc/>
            public Expression Build(
                ExpressionParserCore parser,
                ParserContext context,
                string val,
                int priorityLevel,
                Parenthesis markers,
                ref bool isClosedWrap)
            {
                return parser.ReadExpression(context, priorityLevel, markers, out isClosedWrap);
            }
        }

        /// <summary>
        /// Implements unary minus builder for negative numeric constants or unary expressions.
        /// </summary>
        public class UnaryMinusBuilder : IStartExpressionBuilder
        {
            /// <inheritdoc/>
            public Expression Build(
                ExpressionParserCore parser,
                ParserContext context,
                string val,
                int priorityLevel,
                Parenthesis markers,
                ref bool isClosedWrap)
            {
                // Read the next expression at the correct priority
                var expr = parser.ReadExpression(context, parser.Options.GetOperatorLevel(val, true), markers, out _);

                // If it's a numeric constant, just negate directly
                if (expr is ConstantExpression ce && parser.Options.NumberTypeLevel.ContainsKey(expr.Type))
                {
                    object newValue = ce.Value switch
                    {
                        double d => -d,
                        float s => -s,
                        decimal d => -d,
                        long l => -l,
                        uint i => -(long)i,
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

                // Otherwise, use Expression.Negate
                return Expression.Negate(expr);
            }
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for unary operators,
    /// building a unary expression using the provided <see cref="OperatorBuilder"/> function.
    /// </summary>
    public class UnaryOperandBuilder : IStartExpressionBuilder
    {
        /// <summary>
        /// Initializes a new <see cref="UnaryOperandBuilder"/> with the specified unary operator function.
        /// </summary>
        /// <param name="buildOperator">A function producing the unary expression.</param>
        public UnaryOperandBuilder(Func<Expression, UnaryExpression> buildOperator)
        {
            OperatorBuilder = buildOperator;
        }

        /// <summary>
        /// The function that constructs the unary expression from an operand <see cref="Expression"/>.
        /// </summary>
        public Func<Expression, UnaryExpression> OperatorBuilder { get; }

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var operand = parser.ReadExpression(context, parser.Options.GetOperatorLevel(val, true), markers, out isClosedWrap);
            return OperatorBuilder(operand);
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for handling parentheses or bracket expressions,
    /// such as grouped sub-expressions, type casts, or lambda parameter lists.
    /// </summary>
    public class ParenthesisBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <summary>
        /// Initializes a new <see cref="ParenthesisBuilder"/> with tokens for closing and separating expressions.
        /// </summary>
        /// <param name="closeParenthesis">The symbol representing the closing bracket or parenthesis.</param>
        /// <param name="separator">A symbol (e.g., ",") used to separate expressions within this group.</param>
        public ParenthesisBuilder(string closeParenthesis, string separator)
        {
            CloseParenthesis = closeParenthesis;
            Separator = separator;
        }

        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => [CloseParenthesis, Separator, "<", ">", "(", ")"];

        /// <summary>
        /// Gets the symbol representing the closing bracket or parenthesis.
        /// </summary>
        public string CloseParenthesis { get; }

        /// <summary>
        /// Gets the symbol used to separate expressions within this parenthesis group.
        /// </summary>
        public string Separator { get; }

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var parenthesis = new Parenthesis(val, CloseParenthesis, Separator);

            // Peek inside the parenthesis string
            context.Tokenizer.PushPosition();
            string str = ParserExtensions.GetBracketString(context, parenthesis, true);

            // If we see "=>" after the parenthesis, interpret as lambda
            if (context.Tokenizer.ReadSymbol("=>", false))
            {
                context.Tokenizer.DiscardPosition();
                return LambdaBuilder(parser, context, str);
            }

            // Otherwise, try to parse a type => cast
            Type type = parser.GetType(context, str);

            if (type != null)
            {
                // It's a type cast
                context.Tokenizer.DiscardPosition();
                return Expression.Convert(
                    parser.ReadExpression(context, parser.Options.GetOperatorLevel("convert()", true), parenthesis, out isClosedWrap),
                    type
                );
            }
            else
            {
                // Just treat it as a sub-expression group
                context.Tokenizer.PopPosition();
                var result = parser.ReadExpression(context, 0, parenthesis, out _);
                context.Tokenizer.ReadSymbol(parenthesis.End);
                return result;
            }
        }

        /// <summary>
        /// Builds a lambda expression if the content in parentheses is followed by &quot;=&gt;&quot;,
        /// indicating a lambda definition with explicitly typed parameters.
        /// </summary>
        /// <param name="parser">The main <see cref="ExpressionParserCore"/> instance.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="lambdaPrefix">The string content representing lambda parameters inside parentheses.</param>
        /// <returns>A <see cref="LambdaExpression"/> representing the parsed lambda.</returns>
        public Expression LambdaBuilder(ExpressionParserCore parser, ParserContext context, string lambdaPrefix)
        {
            Parenthesis[] markers = [("<", ">"), ("(", ")")];

            // Split the parameter definitions, e.g. "int x, float y"
            string[] paramsDefinitions = lambdaPrefix
                .SplitCommaSeparatedList(',', markers)
                .Select(p => p.Trim())
                .ToArray();

            var parameters = new ParameterExpression[paramsDefinitions.Length];

            context.PushContext(true);

            // For each parameter definition "Type name"
            for (int i = 0; i < paramsDefinitions.Length; i++)
            {
                if (paramsDefinitions[i] is null)
                    throw new ParseUnknownException(lambdaPrefix, context.Tokenizer.Position.Index);

                int lastSeparatorIndex = paramsDefinitions[i].LastIndexOf(' ');
                if (lastSeparatorIndex == -1)
                {
                    throw new ParseUnknownException(lambdaPrefix, context.Tokenizer.Position.Index);
                }

                var parameterTypeName = paramsDefinitions[i][..lastSeparatorIndex].Trim();
                parameterTypeName = parameterTypeName.IsNullOrEmpty() ? null : parameterTypeName;
                var parameterName = paramsDefinitions[i][lastSeparatorIndex..].Trim();

                var parameterType = parser.GetType(context, parameterTypeName);
                context.AddVariable(Expression.Parameter(parameterType, parameterName));
            }

            // parse the lambda body
            context.PushContext(false);
            var innerExpression = parser.ReadExpression(context, 0, null, out _);
            context.PopContext();

            // build a lambda from the stacked variables
            var lambdaExpression = Expression.Lambda(innerExpression, context.StackVariables);
            context.PopContext();
            return lambdaExpression;
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for a block expression, e.g. statements within { ... }.
    /// Each expression read inside becomes part of the block, using a <see cref="BlockExpressionBuilder"/>.
    /// </summary>
    public class BlockBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <summary>
        /// Initializes a new <see cref="BlockBuilder"/> specifying the closing bracket and a separator.
        /// </summary>
        /// <param name="closeParenthesis">The symbol for closing the block (e.g. "}").</param>
        /// <param name="separator">A symbol to separate statements within the block (e.g. ";").</param>
        public BlockBuilder(string closeParenthesis, string separator)
        {
            CloseParenthesis = closeParenthesis;
            Separator = separator;
        }

        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => [CloseParenthesis, Separator];

        /// <summary>
        /// Gets the closing symbol for the block (e.g. "}").
        /// </summary>
        public string CloseParenthesis { get; }

        /// <summary>
        /// Gets the symbol used to separate statements within the block (e.g. ";").
        /// </summary>
        public string Separator { get; }

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var blockMarkers = new Parenthesis(val, CloseParenthesis, Separator);

            context.PushContext();
            var expressions = parser.ReadExpressions(context, blockMarkers, false);
            var variables = context.StackVariables;

            var builder = new BlockExpressionBuilder(variables, expressions);
            context.PopContext();

            return builder.CreateBlock();
        }
    }

    /// <summary>
    /// A default unary builder that determines whether the token is a name
    /// (resolving a parameter, constant, or type) or a string/char literal,
    /// and creates the appropriate <see cref="Expression"/>.
    /// </summary>
    public class DefaultUnaryBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var firstChar = val[0];

            // If token starts with a quote, treat it as a string or char literal
            if (firstChar == '"' || firstChar == '\'' || firstChar == '@' || firstChar == '$')
            {
                return ReadStringOrChar(parser, context, val, firstChar);
            }

            // Otherwise, attempt to parse it as a name
            if (val.IsName())
            {
                return ReadName(parser, context, val);
            }

            throw new ParseUnknownException(val, context.Tokenizer.Position.Index);
        }

        /// <summary>
        /// Reads a string or char literal from <paramref name="val"/>.
        /// </summary>
        private static Expression ReadStringOrChar(ExpressionParserCore parser, ParserContext context, string val, char firstChar)
        {
            return firstChar switch
            {
                '\"' or '@' => Expression.Constant(context.Tokenizer.DefineString),
                '\'' => Expression.Constant(context.Tokenizer.DefineString[0]),
                '$' => BuildInterpolatedString(parser, context, context.Tokenizer.DefineString),
                _ => throw new ParseUnknownException(val, context.Tokenizer.Position.Index),
            };
        }

        /// <summary>
        /// Interprets <paramref name="val"/> as a variable, constant, type, or member access,
        /// returning the appropriate <see cref="Expression"/>.
        /// </summary>
        private static Expression ReadName(ExpressionParserCore parser, ParserContext context, string val)
        {
            // If it matches a declared variable or a known constant
            if (context.TryFindVariable(val, out var parameter))
                return parameter;
            if (parser.Resolver.TryGetConstant(val, out var constant))
                return constant;

            // If there's a default instance, try to get a member from it
            if (context.DefaultInstanceParam != null)
            {
                var expression = parser.GetExpression(context, context.DefaultInstanceParam, val);
                if (expression != null) return expression;
            }

            // If there's a default static type, try to get a member from it
            if (context.DefaultStaticType != null)
            {
                var expression = parser.GetExpression(context, context.DefaultStaticType, val);
                if (expression != null) return expression;
            }

            // Otherwise, interpret as a type
            var type = parser.ReadType(context, val);
            var token = context.Tokenizer.PeekToken();

            // If there's a dot, read the member
            if (token == ".")
            {
                context.Tokenizer.ReadSymbol(".");
                string strMember = context.Tokenizer.ReadToken();
                var expression = parser.GetExpression(context, type, strMember);
                if (expression != null) return expression;
                throw new ParseUnknownException(strMember, context.Tokenizer.Position.Index);
            }

            // If the next token is a name, declare a new variable
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

        private static Expression BuildInterpolatedString(ExpressionParserCore parser, ParserContext context, string format)
        {
            ArgumentNullException.ThrowIfNull(parser);

            var parsed = new InterpolatedStringParser(format);
            var pieces = new List<Expression>();

            foreach (var part in parsed)
            {
                switch (part)
                {
                    case LiteralPart lit:
                        pieces.Add(Expression.Constant(lit.Text));
                        break;
                    case FormattedPart fp:
                        var expr = ExpressionParser.ParseExpression(
                                fp.ExpressionText,
                                [.. context.Parameters],
                                context.DefaultStaticType,
                                context.FirstArgumentIsDefaultInstance);
                        pieces.Add(
                                Expression.Call(
                                        Expression.Convert(expr, typeof(object)),
                                        typeof(object).GetMethod("ToString")!
                                )
                        );
                        break;
                }
            }

            return Expression.Call(
                    typeof(string).GetMethod("Concat", [typeof(string[])])!,
                    Expression.NewArrayInit(typeof(string), pieces)
            );
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for "if" statements, optionally
    /// handling an "else" branch.
    /// </summary>
    public class IfBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IfBuilder"/> class, specifying the "else" keyword.
        /// </summary>
        /// <param name="elseKeyword">
        /// The symbol representing the else portion of the conditional (e.g., "else").
        /// </param>
        public IfBuilder(string elseKeyword)
        {
            ElseKeyword = elseKeyword;
        }

        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => [ElseKeyword];

        /// <summary>
        /// Gets the keyword used to identify the "else" branch.
        /// </summary>
        public string ElseKeyword { get; }

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            // Parse the condition in parentheses
            context.Tokenizer.ReadSymbol("(");
            var testExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", null), out _);
            context.Tokenizer.ReadSymbol(")");

            // Parse the "if true" body
            var ifTrueExpression = parser.ReadExpression(context, 0, null, out _);

            // Attempt to read a semicolon if the expression isn't a block
            context.Tokenizer.PushPosition();
            if (ifTrueExpression is not BlockExpression)
                context.Tokenizer.ReadSymbol(";");
            var nextToken = context.Tokenizer.ReadToken();

            // If there's an else branch, read it
            if (nextToken == ElseKeyword)
            {
                context.Tokenizer.DiscardPosition();
                var ifFalseExpression = parser.ReadExpression(context, 0, null, out _);
                return Expression.IfThenElse(testExpression, ifTrueExpression, ifFalseExpression);
            }

            // Otherwise, revert the read and finalize with no else branch
            context.Tokenizer.PopPosition();
            return Expression.IfThen(testExpression, ifTrueExpression);
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for the "break" statement,
    /// returning an <see cref="Expression.Break(LabelTarget)"/> to the nearest loop label.
    /// </summary>
    public class BreakBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var target = context.BreakLabel;
            if (target == null)
                throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);

            return Expression.Break(target);
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for the "continue" statement,
    /// returning an <see cref="Expression.Break(LabelTarget)"/> that effectively simulates a continue
    /// (jumping to the loop's continue label).
    /// </summary>
    public class ContinueBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var target = context.ContinueLabel;
            if (target == null)
                throw new ParseWrongSymbolException("", val, context.Tokenizer.Position.Index);

            // Use Expression.Break with the continue label
            return Expression.Break(target);
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for the "return" statement.
    /// The builder simply reads the following expression and returns it,
    /// ignoring early-exit semantics.
    /// </summary>
    public class ReturnBuilder : IStartExpressionBuilder
    {
        /// <inheritdoc/>
        public Expression Build(
                ExpressionParserCore parser,
                ParserContext context,
                string val,
                int priorityLevel,
                Parenthesis markers,
                ref bool isClosedWrap)
        {
            var expr = parser.ReadExpression(context, 0, null, out _);
            context.Tokenizer.ReadSymbol(";");
            return expr ?? Expression.Empty();
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for a "while" loop construct,
    /// building an <see cref="Expression.Loop(Expression)"/>. The loop terminates if the condition is false.
    /// </summary>
    public class WhileBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")", ";"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var continueLabel = Expression.Label();
            var breakLabel = Expression.Label();
            context.PushContext(continueLabel, breakLabel);

            // Parse the condition inside parentheses
            context.Tokenizer.ReadSymbol("(");
            var testExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", null), out _);
            context.Tokenizer.ReadSymbol(")");

            // Parse the loop body
            var loopExpression = parser.ReadExpression(context, 0, null, out _);

            var result = Expression.Block(
                context.StackVariables,
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(
                            Expression.Not(testExpression),
                            Expression.Break(breakLabel)
                        ),
                        loopExpression
                    ),
                    breakLabel,
                    continueLabel
                )
            );

            context.PopContext();
            return result;
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for a "for" loop construct,
    /// building an <see cref="Expression.Loop(Expression)"/> with initializers, condition, and increments.
    /// </summary>
    public class ForBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")", ";"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var continueLabel = Expression.Label();
            var breakLabel = Expression.Label();
            context.PushContext(continueLabel, breakLabel);

            // Parse expressions inside for(...) separated by ";"
            var forExpression = parser.ReadExpressions(context, new Parenthesis("(", ")", ";"), true, true);

            var initializer = forExpression[0];
            var test = forExpression[1];
            var loop = forExpression[2..]; // increments
            var loopExpression = parser.ReadExpression(context, 0, null, out _);

            var loopBody = loop
                    .Prepend(loopExpression)
                    .Prepend(Expression.IfThen(Expression.Not(test), Expression.Break(breakLabel)));

            var result = Expression.Block(
                    context.StackVariables,
                    initializer,
                    Expression.Loop(
                            Expression.Block(loopBody),
                            breakLabel,
                            continueLabel
                    )
            );

            context.PopContext();
            return result;
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for a "foreach" loop,
    /// building either an <see cref="BlockExpression"/> that enumerates an array
    /// or uses <see cref="IEnumerator"/> for other enumerables.
    /// </summary>
    public class ForEachBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")", "in"];

        /// <inheritdoc/>
        public Expression Build(
            ExpressionParserCore parser,
            ParserContext context,
            string val,
            int priorityLevel,
            Parenthesis markers,
            ref bool isClosedWrap)
        {
            var continueLabel = Expression.Label();
            var breakLabel = Expression.Label();
            context.PushContext(continueLabel, breakLabel);

            // Parse "(type variable in collection)"
            context.Tokenizer.ReadSymbol("(");
            var forExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", "in"), out _);
            context.Tokenizer.ReadSymbol("in");
            var enumerableExpression = parser.ReadExpression(context, 0, new Parenthesis("(", ")", "in"), out _);
            context.Tokenizer.ReadSymbol(")");

            // Parse the loop body
            var loopExpression = parser.ReadExpression(context, 0, null, out _);

            Expression result;
            if (enumerableExpression.Type.IsArray && enumerableExpression.Type.GetArrayRank() == 1)
            {
                // Array-based foreach
                result = CreateExpressionWithArray(context, continueLabel, breakLabel, forExpression, enumerableExpression, loopExpression);
            }
            else
            {
                // IEnumerable-based foreach
                result = CreateExpressionWithEnumerator(context, continueLabel, breakLabel, forExpression, enumerableExpression, loopExpression);
            }

            context.PopContext();
            return result;
        }

        /// <summary>
        /// Creates a foreach loop for a single-dimensional array, enumerating indices via <c>GetLowerBound</c> and <c>GetUpperBound</c>.
        /// </summary>
        private static Expression CreateExpressionWithArray(
            ParserContext context,
            LabelTarget continueLabel,
            LabelTarget breakLabel,
            Expression forExpression,
            Expression enumerableExpression,
            Expression loopExpression)
        {
            var getLowerBoundMethod = typeof(Array).GetMethod("GetLowerBound");
            var getUpperBoundMethod = typeof(Array).GetMethod("GetUpperBound");

            var indexVariable = Expression.Variable(typeof(int), "index");
            var upperBoundVariable = Expression.Variable(typeof(int), "upperBound");

            var initializer1 = Expression.Assign(indexVariable, Expression.Call(enumerableExpression, getLowerBoundMethod, Expression.Constant(0)));
            var initializer2 = Expression.Assign(upperBoundVariable, Expression.Call(enumerableExpression, getUpperBoundMethod, Expression.Constant(0)));

            var loop = Expression.PostIncrementAssign(indexVariable);
            var test = Expression.GreaterThan(indexVariable, upperBoundVariable);

            return Expression.Block(
                    context.StackVariables.Append(indexVariable).Append(upperBoundVariable),
                    initializer1,
                    initializer2,
                    Expression.Loop(
                            Expression.Block(
                                    Expression.IfThen(test, Expression.Break(breakLabel)),
                                    Expression.Assign(forExpression, Expression.ArrayIndex(enumerableExpression, indexVariable)),
                                    loopExpression,
                                    loop
                            ),
                            breakLabel,
                            continueLabel
                    )
            );
        }

        /// <summary>
        /// Creates a foreach loop for an <see cref="IEnumerable"/> or generic <see cref="IEnumerable{T}"/>,
        /// building an enumerator and calling <c>MoveNext()</c> until completion.
        /// </summary>
        private static Expression CreateExpressionWithEnumerator(
            ParserContext context,
            LabelTarget continueLabel,
            LabelTarget breakLabel,
            Expression forExpression,
            Expression enumerableExpression,
            Expression loopExpression)
        {
            // Try to detect IEnumerable<T> for typed enumerator
            var enumerableGenericType = typeof(IEnumerable<>);
            var enumerableType = typeof(IEnumerable);

            MethodInfo getEnumerator = null;
            PropertyInfo current = null;
            Expression assignment = null;
            ParameterExpression enumeratorVariable = null;

            // For typed enumerables (IEnumerable<T>)
            var typedEnumerableGenericType = enumerableExpression.Type.GetInterfaces()
                .Prepend(enumerableExpression.Type)
                .FirstOrDefault(i =>
                    i.IsInterface
                    && i.IsGenericType
                    && i.GetGenericTypeDefinition() == enumerableGenericType
                    && forExpression.Type.IsAssignableFromEx(i.GetGenericArguments()[0]));

            if (typedEnumerableGenericType != null)
            {
                getEnumerator = typedEnumerableGenericType.GetMethod("GetEnumerator");
                current = getEnumerator.ReturnType.GetProperty("Current");
                enumeratorVariable = Expression.Variable(
                        typeof(IEnumerator<>).MakeGenericType(typedEnumerableGenericType.GetGenericArguments()[0]),
                        "enumerator");
                assignment = Expression.Assign(forExpression, Expression.Property(enumeratorVariable, current));
            }
            // For non-generic IEnumerable
            else if (enumerableExpression.Type.GetInterfaces()
                    .Prepend(enumerableExpression.Type)
                    .Any(i => i.IsInterface && i == enumerableType))
            {
                getEnumerator = enumerableType.GetMethod("GetEnumerator");
                current = getEnumerator.ReturnType.GetProperty("Current");
                enumeratorVariable = Expression.Variable(typeof(IEnumerator), "enumerator");
                assignment = Expression.Assign(
                    forExpression,
                    Expression.ConvertChecked(
                        Expression.Property(enumeratorVariable, current),
                        forExpression.Type
                    )
                );
            }

            if (getEnumerator is null)
                throw new ParseUnknownException(enumerableExpression.ToString(), context.Tokenizer.Position.Index);

            var moveNext = typeof(IEnumerator).GetMethod("MoveNext");
            var initializer = Expression.Assign(enumeratorVariable, Expression.Call(enumerableExpression, getEnumerator));

            return Expression.Block(
                    context.StackVariables.Append(enumeratorVariable),
                    initializer,
                    Expression.Loop(
                            Expression.Block(
                                    Expression.IfThen(
                                            Expression.Not(Expression.Call(enumeratorVariable, moveNext)),
                                            Expression.Break(breakLabel)
                                    ),
                                    assignment,
                                    loopExpression
                            ),
                            breakLabel,
                            continueLabel
                    )
            );
        }
    }

    /// <summary>
    /// Implements <see cref="IStartExpressionBuilder"/> for <c>switch</c> statements.
    /// This builder supports both expression-style switches and the classic
    /// statement form where each case ends with <c>break;</c>.
    /// </summary>
    public class SwitchBuilder : IStartExpressionBuilder, IAdditionalTokens
    {
        /// <inheritdoc/>
        public IEnumerable<string> AdditionalTokens => ["(", ")", "{", "}", "case", "default", ":", ";", "break"];

        /// <inheritdoc/>
        public Expression Build(
                ExpressionParserCore parser,
                ParserContext context,
                string val,
                int priorityLevel,
                Parenthesis markers,
                ref bool isClosedWrap)
        {
            context.Tokenizer.ReadSymbol("(");
            var switchValue = parser.ReadExpression(context, 0, new Parenthesis("(", ")", null), out _);
            context.Tokenizer.ReadSymbol(")");
            context.Tokenizer.ReadSymbol("{");

            List<SwitchCase> cases = new();
            Expression defaultBody = Expression.Empty();

            while (true)
            {
                string token = context.Tokenizer.ReadToken();
                if (token == "case")
                {
                    var testValue = parser.ReadExpression(context, 0, null, out _);
                    context.Tokenizer.ReadSymbol(":");
                    var body = parser.ReadExpression(context, 0, null, out _);
                    if (body.Type != switchValue.Type)
                        body = Expression.Convert(body, switchValue.Type);
                    context.Tokenizer.ReadSymbol(";");
                    var next = context.Tokenizer.PeekToken();
                    if (next == "break")
                    {
                        context.Tokenizer.ReadToken();
                        context.Tokenizer.ReadSymbol(";");
                    }
                    if (testValue.Type != switchValue.Type)
                    {
                        if (testValue is ConstantExpression ce)
                        {
                            object constantValue = ce.Value;
                            if (constantValue != null)
                                constantValue = Convert.ChangeType(constantValue, switchValue.Type);
                            testValue = Expression.Constant(constantValue, switchValue.Type);
                        }
                        else
                            testValue = Expression.Convert(testValue, switchValue.Type);
                    }
                    cases.Add(Expression.SwitchCase(body, testValue));
                }
                else if (token == "default")
                {
                    context.Tokenizer.ReadSymbol(":");
                    defaultBody = parser.ReadExpression(context, 0, null, out _);
                    if (defaultBody.Type != switchValue.Type)
                        defaultBody = Expression.Convert(defaultBody, switchValue.Type);
                    context.Tokenizer.ReadSymbol(";");
                    var next = context.Tokenizer.PeekToken();
                    if (next == "break")
                    {
                        context.Tokenizer.ReadToken();
                        context.Tokenizer.ReadSymbol(";");
                    }
                }
                else if (token == "}")
                {
                    break;
                }
                else
                {
                    throw new ParseUnknownException(token, context.Tokenizer.Position.Index);
                }
            }

            var switchExpr = Expression.Switch(switchValue, defaultBody, cases.ToArray());
            return switchExpr;
        }
    }
}
