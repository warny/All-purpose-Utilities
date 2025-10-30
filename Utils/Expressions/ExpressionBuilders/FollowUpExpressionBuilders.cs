using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions.ExpressionBuilders;

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> to handle the plus operator (+),
/// supporting both numeric addition and string concatenation.
/// </summary>
public class PlusOperatorBuilder() : IFollowUpExpressionBuilder
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
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);

        // If either operand is of string type, do string concatenation
        if (currentExpression.Type == typeof(string) || right.Type == typeof(string))
        {
            return BuildStringConcatenation(currentExpression, right);
        }
        else
        {
            // Otherwise, handle numeric addition
            (currentExpression, right) = parser.Options.AdjustNumberType(currentExpression, right);
            return Expression.Add(currentExpression, right);
        }
    }

    /// <summary>
    /// Builds a string concatenation <see cref="Expression"/> by merging
    /// any existing Concat calls on the left or right, then calls <c>string.Concat</c>.
    /// </summary>
    /// <param name="currentExpression">The left expression (potentially a Concat call).</param>
    /// <param name="right">The right expression (potentially a Concat call).</param>
    /// <returns>An <see cref="Expression"/> representing the concatenation of the two operands.</returns>
    private static Expression BuildStringConcatenation(Expression currentExpression, Expression right)
    {
        var concat = typeof(string).GetRuntimeMethod("Concat", [typeof(string[])]);

        Expression[] lefts;
        Expression[] rights;

        // Unpack array init from left side if Concat call
        if (currentExpression is MethodCallExpression methodCallLeft && methodCallLeft.Method == concat)
        {
            lefts = [.. ((NewArrayExpression)methodCallLeft.Arguments[0]).Expressions];
        }
        else
        {
            lefts = [currentExpression];
        }

        // Unpack array init from right side if Concat call
        if (right is MethodCallExpression methodCallRight && methodCallRight.Method == concat)
        {
            rights = [.. ((NewArrayExpression)methodCallRight.Arguments[0]).Expressions];
        }
        else
        {
            rights = [right];
        }

        // Merge adjacent string constants
        if (lefts[^1] is ConstantExpression constLeft && rights[0] is ConstantExpression constRight)
        {
            lefts[^1] = Expression.Constant(
                (string)constLeft.Value + (string)constRight.Value,
                typeof(string)
            );
            rights = rights[1..];
        }

        var newArguments = lefts.Concat(rights).ToArray();
        return newArguments.Length > 1
            ? Expression.Call(concat, Expression.NewArrayInit(typeof(string), newArguments))
            : newArguments[0];
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for binary operators,
/// allowing a custom operator function and optional numeric type adjustment.
/// </summary>
public class OperatorBuilder : IFollowUpExpressionBuilder
{
    /// <summary>
    /// Initializes a new instance of <see cref="OperatorBuilder"/> with a specified operator function
    /// and a flag indicating whether to adjust numeric types.
    /// </summary>
    /// <param name="buildOperator">A function that builds the resulting <see cref="Expression"/> from two operands.</param>
    /// <param name="adjustNumberType">
    /// If <see langword="true"/>, calls <c>parser.Options.AdjustNumberType</c> on the operands.
    /// </param>
    public OperatorBuilder(Func<Expression, Expression, Expression> buildOperator, bool adjustNumberType)
    {
        BuildOperator = buildOperator;
        AdjustNumberType = adjustNumberType;
    }

    private Func<Expression, Expression, Expression> BuildOperator { get; }
    private bool AdjustNumberType { get; }

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
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        if (AdjustNumberType)
        {
            (currentExpression, right) = parser.Options.AdjustNumberType(currentExpression, right);
        }
        return BuildOperator(currentExpression, right);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> to handle a member access operation
/// (e.g., appending ".MemberName" after an existing expression).
/// </summary>
public class MemberBuilder : IFollowUpExpressionBuilder
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
        string strMember = context.Tokenizer.ReadToken();
        return parser.GetExpression(context, currentExpression, strMember);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for the null-conditional operator (?.),
/// returning either <see langword="null"/> or the expression member access.
/// </summary>
public class NullOrMemberBuilder : IFollowUpExpressionBuilder
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
        context.PushContext();
        string strMember = context.Tokenizer.ReadToken();

        // Create a variable to store the current expression's value
        var variable = Expression.Variable(currentExpression.Type, $"<value{context.Depth}>");
        context.AddVariable(variable);

        // Build the member access for e.g. object?.Member
        var readExpression = parser.GetExpression(context, variable, strMember);

        // Determine target type (class -> same type, struct -> nullable)
        var targetType = readExpression.Type.IsClass
            ? readExpression.Type
            : typeof(Nullable<>).MakeGenericType(readExpression.Type);

        // Return null if the object is null, otherwise the read expression
        var expression = Expression.Block(
            context.StackVariables,
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

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for a type-check operator, e.g. "expression is Type".
/// </summary>
public class TypeMatchBuilder : IFollowUpExpressionBuilder
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
        Type t = parser.ReadType(context, null);
        return Expression.TypeIs(currentExpression, t);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for an additive assign operation (e.g. "+="),
/// supporting string concatenation when the left operand is a string.
/// </summary>
public class AddAssignationBuilder : IFollowUpExpressionBuilder
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
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        if (currentExpression.Type == typeof(string))
        {
            return BuildStringConcatenation(currentExpression, right);
        }

        right = parser.Options.AdjustNumberType(currentExpression.Type, right);
        return Expression.AddAssign(currentExpression, right);
    }

    /// <summary>
    /// Creates an expression that appends the contents of <paramref name="right"/> to <paramref name="left"/>
    /// by calling <c>string.Concat</c>, then assigns it back to <paramref name="left"/>.
    /// </summary>
    private static Expression BuildStringConcatenation(Expression left, Expression right)
    {
        var concat = typeof(string).GetRuntimeMethod("Concat", [typeof(string[])]);

        IEnumerable<Expression> rights;

        // If the right side is already a Concat call, we can combine arrays
        if (right is MethodCallExpression methodCallRight && methodCallRight.Method == concat)
        {
            rights = ((NewArrayExpression)methodCallRight.Arguments[0]).Expressions;
        }
        else
        {
            rights = [right];
        }

        // Use array init to combine left and the right's elements
        return Expression.Assign(
            left,
            Expression.Call(
                concat,
                Expression.NewArrayInit(typeof(string), [left, .. rights])
            )
        );
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for simple assignment operators (e.g. "="),
/// allowing a custom assignment <c>BinaryExpression</c> factory.
/// </summary>
public class AssignationBuilder : IFollowUpExpressionBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignationBuilder"/> class with the specified operator function.
    /// </summary>
    /// <param name="buildOperator">
    /// A function that produces a <see cref="BinaryExpression"/> representing the assignment (e.g. <c>Expression.Assign</c>).
    /// </param>
    public AssignationBuilder(Func<Expression, Expression, BinaryExpression> buildOperator)
    {
        BuildOperator = buildOperator;
    }

    private Func<Expression, Expression, BinaryExpression> BuildOperator { get; }

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
        Expression right = parser.ReadExpression(context, nextLevel, markers, out isClosedWrap);
        right = parser.Options.AdjustNumberType(currentExpression.Type, right);
        return BuildOperator(currentExpression, right);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for post-increment or post-decrement operators,
/// e.g. i++ or i--. The supplied operator function is responsible for creating the appropriate unary expression.
/// </summary>
public class PostOperationBuilder : IFollowUpExpressionBuilder
{
    /// <summary>
    /// Initializes a new instance of <see cref="PostOperationBuilder"/> with the specified unary operator function.
    /// </summary>
    /// <param name="buildOperator">
    /// A function that takes an <see cref="Expression"/> and returns a <see cref="UnaryExpression"/>
    /// (e.g., <c>Expression.PostIncrementAssign</c>).
    /// </param>
    public PostOperationBuilder(Func<Expression, UnaryExpression> buildOperator)
    {
        BuildOperator = buildOperator;
    }

    private Func<Expression, UnaryExpression> BuildOperator { get; }

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
        return BuildOperator(currentExpression);
    }
}

/// <summary>
/// Implements <see cref="IFollowUpExpressionBuilder"/> for type-cast operations such as <c>(Type)expression</c>.
/// It uses <c>Expression.TypeAs</c> to create a safe cast expression.
/// </summary>
public class TypeCastBuilder : IFollowUpExpressionBuilder
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
        Type t = parser.ReadType(context, null);
        return Expression.TypeAs(currentExpression, t);
    }
}
