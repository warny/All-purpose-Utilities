using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Recognizes the expression nodes the engine treats as opaque (ETC R4): any instance of a class outside the framework
/// expression object model, that is an extension node (a user subclass of <see cref="Expression"/>).
/// </summary>
/// <remarks>
/// <para>
/// The public framework node classes (<see cref="BinaryExpression"/>, <see cref="UnaryExpression"/>, …,
/// <see cref="DynamicExpression"/>) have no public or protected constructor, so <see cref="Expression"/> itself is the only
/// class user code can derive from: a node that is an instance of none of these classes is user code. The test is a chain of
/// type tests only. It never reads a member of the node, not even the virtual <see cref="Expression.NodeType"/> and
/// <see cref="Expression.Type"/> getters, which a subclass is free to override with code that counts, throws or changes.
/// </para>
/// <para>
/// Opacity policy: the engine never reads <see cref="Expression.NodeType"/>, <see cref="Expression.Type"/>,
/// <see cref="Expression.CanReduce"/>, never calls <see cref="Expression.Reduce"/>, <see cref="Expression.ReduceAndCheck"/>,
/// <c>VisitChildren</c>, <c>Accept</c>, <see cref="object.Equals(object?)"/>, <see cref="object.GetHashCode"/> or
/// <see cref="object.ToString"/> of an opaque node: it is passed through unchanged, compared and hashed by reference, keyed as
/// unsupported, and classified as observable. The framework factories that rebuild a supported parent validate their operands,
/// so rebuilding a parent of an opaque node (because another child changed) reads that node's <see cref="Expression.Type"/>
/// and <see cref="Expression.NodeType"/>, exactly as the factory that built the source tree did; that read belongs to the
/// framework, not to the engine.
/// </para>
/// </remarks>
internal static class ExpressionOpacity
{
    /// <summary>
    /// Whether <paramref name="expression"/> is an instance of a class outside the framework expression object model (an
    /// extension node), recognized by type tests only.
    /// </summary>
    /// <param name="expression">The node.</param>
    /// <returns><see langword="true"/> for an opaque node.</returns>
    /// <remarks>The most frequent classes are tested first, so a common node leaves the chain after one or two tests.</remarks>
    internal static bool IsOpaque(Expression expression)
        => expression is not (BinaryExpression or ParameterExpression or ConstantExpression or UnaryExpression
            or MethodCallExpression or LambdaExpression or MemberExpression or ConditionalExpression or InvocationExpression
            or NewExpression or NewArrayExpression or TypeBinaryExpression or DefaultExpression or BlockExpression
            or TryExpression or SwitchExpression or LoopExpression or GotoExpression or LabelExpression
            or RuntimeVariablesExpression or DebugInfoExpression or MemberInitExpression or ListInitExpression
            or IndexExpression or DynamicExpression);

    /// <summary>
    /// Describes <paramref name="expression"/> for an exception message without running its code: its node type for a
    /// framework node, its runtime class name for an opaque node.
    /// </summary>
    /// <param name="expression">The node, or <see langword="null"/>.</param>
    /// <returns>A short description.</returns>
    internal static string Describe(Expression? expression)
        => expression is null
            ? "null"
            : IsOpaque(expression) ? $"opaque {expression.GetType().Name}" : expression.NodeType.ToString();
}
