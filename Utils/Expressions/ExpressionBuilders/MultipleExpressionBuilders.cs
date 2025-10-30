using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions.ExpressionBuilders;

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for bracket-based operations,
/// which may represent either array indexing or default member (indexer) access.
/// </summary>
public class BracketBuilder : IFollowUpExpressionBuilder
{
    /// <inheritdoc/>
    public Expression Build(
        ExpressionParserCore parser,
        ParserContext context,
        Expression currentExpression,
        string val,
        string nextVal,
        int priorityLevel,
        ref int nextLevel,
        Parenthesis markers,
        ref bool isClosedWrap)
    {
        var brackets = new Parenthesis("[", "]", ",");

        // If the target is an array, parse a single index expression.
        if (currentExpression.Type.IsArray)
        {
            var indexExpr = parser.ReadExpression(context, 0, brackets, out _);
            var adjustedIndex = parser.Options.AdjustType(indexExpr, typeof(int));
            var result = Expression.ArrayIndex(currentExpression, adjustedIndex);

            // Ensure the bracket is closed
            string token = context.Tokenizer.ReadToken();
            if (token != brackets.End)
                throw new ParseUnknownException(token, context.Tokenizer.Position.Index);

            return result;
        }

        // Otherwise, handle default member-based (indexer) access if available
        var atts = currentExpression
            .GetType()
            .GetTypeInfo()
            .GetCustomAttributes<DefaultMemberAttribute>()
            .ToArray();

        var indexerNameAtt = atts.SingleOrDefault();
        if (indexerNameAtt == null) return currentExpression; // No indexer available

        string indexerName = indexerNameAtt.MemberName;
        var propertyInfo = currentExpression.Type.GetRuntimeProperty(indexerName);
        var methodInfo = propertyInfo.GetMethod;

        // Parse expressions for the indexer parameters
        var listParam = parser.ReadExpressions(context, brackets);

        return Expression.Call(currentExpression, methodInfo, listParam);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for a closing parenthesis builder.
/// Depending on the context, this may represent function invocation or fallback logic.
/// </summary>
public class RightParenthesisBuilder : IFollowUpExpressionBuilder
{
    /// <inheritdoc/>
    public Expression Build(
        ExpressionParserCore parser,
        ParserContext context,
        Expression currentExpression,
        string val,
        string nextVal,
        int priorityLevel,
        ref int nextLevel,
        Parenthesis markers,
        ref bool isClosedWrap)
    {
        // If the current expression is a delegate, parse invocation
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
            // Otherwise, delegate to fallback builder
            parser.Builder.FallbackBinaryOrTernaryBuilder.Build(
                parser,
                context,
                currentExpression,
                val,
                nextVal,
                priorityLevel,
                ref nextLevel,
                markers,
                ref isClosedWrap
            );
            return null;
        }
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> that checks for matching wrap start
/// and sets the <see name="T:isClosedWrap"/> flag, concluding the bracket/parenthesis context.
/// </summary>
public class CloseBuilder : IFollowUpExpressionBuilder
{
    /// <summary>
    /// Initializes a new instance of <see cref="CloseBuilder"/> with the specified wrap start token.
    /// </summary>
    /// <param name="wrapStart">A token indicating which opening symbol must match.</param>
    public CloseBuilder(string wrapStart)
    {
        WrapStart = wrapStart;
    }

    private string WrapStart { get; }

    /// <inheritdoc/>
    public Expression Build(
        ExpressionParserCore parser,
        ParserContext context,
        Expression currentExpression,
        string val,
        string nextVal,
        int priorityLevel,
        ref int nextLevel,
        Parenthesis markers,
        ref bool isClosedWrap)
    {
        if (WrapStart != this.WrapStart)
        {
            throw new ParseUnmatchException(WrapStart, nextVal, context.Tokenizer.Position.Index);
        }
        isClosedWrap = true;
        return currentExpression;
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> and <see cref="IAdditionalTokens"/> for
/// a ternary-like operation, e.g., "condition ? expr : expr". The symbol recognized for the second
/// separator is customizable via <see cref="ChoiceSymbol"/>.
/// </summary>
public class ConditionalBuilder : IFollowUpExpressionBuilder, IAdditionalTokens
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConditionalBuilder"/> specifying the token used as the second separator (e.g. ":").
    /// </summary>
    /// <param name="choiceSymbol">The symbol representing the second branch delimiter (typically ":").</param>
    public ConditionalBuilder(string choiceSymbol)
    {
        ChoiceSymbol = choiceSymbol;
    }

    /// <summary>
    /// Gets an enumeration containing the custom choice symbol recognized by this builder.
    /// </summary>
    public IEnumerable<string> AdditionalTokens => [ChoiceSymbol];

    /// <summary>
    /// Gets the custom symbol that separates the true/false parts of the conditional expression.
    /// </summary>
    public string ChoiceSymbol { get; }

    /// <inheritdoc/>
    public Expression Build(
        ExpressionParserCore parser,
        ParserContext context,
        Expression currentExpression,
        string val,
        string nextVal,
        int priorityLevel,
        ref int nextLevel,
        Parenthesis markers,
        ref bool isClosedWrap)
    {
        // Read the true expression
        Expression first = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);

        // Expect the choiceSymbol
        context.Tokenizer.ReadSymbol(ChoiceSymbol);

        // Read the false expression
        Expression second = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);

        // Build a conditional expression: condition ? first : second
        return Expression.Condition(currentExpression, first, second);
    }
}
