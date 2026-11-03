using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>The observable outcome of one evaluation of a compiled audit tree.</summary>
/// <param name="Value">The result, or <see cref="double.NaN"/> when it threw.</param>
/// <param name="Exception">The exception type, or <see langword="null"/>.</param>
/// <param name="Events">The traced effects, in order.</param>
internal sealed record AuditOutcome(double Value, Type? Exception, string[] Events)
{
    /// <inheritdoc/>
    public override string ToString() => $"{(Exception is null ? Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "throws " + Exception.Name)} [{string.Join(",", Events)}]";
}

/// <summary>Shared helpers of the ETC closure audit: evaluation, cloning, free variables, printing and counterexample reduction.</summary>
internal static class EtcAuditSupport
{
    /// <summary>The argument pairs every audit tree is evaluated on.</summary>
    public static readonly (double X, double Y)[] Inputs = [(0, 0), (1, 2), (-1.5, 0.5), (3, -2), (double.NaN, 1)];

    /// <summary>The internal <c>DebugView</c> property of expressions.</summary>
    private static readonly PropertyInfo DebugViewProperty = typeof(Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Gets the debug view of a tree, or its <c>ToString</c> when the debug view cannot be produced.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>A printable form.</returns>
    public static string Print(Expression e)
    {
        try
        {
            return (string)DebugViewProperty.GetValue(e)!;
        }
        catch (TargetInvocationException)
        {
            return e.ToString();
        }
    }

    /// <summary>Compiles a lambda, or returns <see langword="null"/> with the reason when it cannot be compiled.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="error">The compilation error.</param>
    /// <returns>The delegate.</returns>
    public static Func<double, double, double>? TryCompile(Expression<Func<double, double, double>> lambda, out string? error)
    {
        try
        {
            error = null;
            return lambda.Compile();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    /// <summary>Evaluates a compiled tree on every input.</summary>
    /// <param name="function">The delegate.</param>
    /// <returns>The outcomes.</returns>
    public static AuditOutcome[] Evaluate(Func<double, double, double> function)
    {
        var outcomes = new AuditOutcome[Inputs.Length];
        for (int i = 0; i < Inputs.Length; i++)
        {
            R4DynamicRuntime.Reset();
            try
            {
                double value = function(Inputs[i].X, Inputs[i].Y);
                outcomes[i] = new AuditOutcome(value, null, [.. R4DynamicRuntime.Events]);
            }
            catch (Exception ex)
            {
                outcomes[i] = new AuditOutcome(double.NaN, ex.GetType(), [.. R4DynamicRuntime.Events]);
            }
        }

        return outcomes;
    }

    /// <summary>The ways two outcomes may differ.</summary>
    public enum Divergence
    {
        /// <summary>No observable difference.</summary>
        None,

        /// <summary>Same effects and exceptions; the numbers differ only by rounding (reassociation) or the sign of a zero.</summary>
        Rounding,

        /// <summary>Same effects and exceptions; one result is NaN or infinite and the other is not (IEEE identity class).</summary>
        Ieee,

        /// <summary>Same effects and exceptions; the numbers differ beyond rounding.</summary>
        Value,

        /// <summary>The thrown exception differs.</summary>
        Exception,

        /// <summary>The effects (traced calls, accessors, binders) differ in count or order.</summary>
        Effects,
    }

    /// <summary>Classifies the most severe difference between two outcome lists.</summary>
    /// <param name="expected">The outcomes of the original.</param>
    /// <param name="actual">The outcomes of the transformed tree.</param>
    /// <returns>The most severe divergence.</returns>
    public static Divergence Compare(AuditOutcome[] expected, AuditOutcome[] actual) => Compare(expected, actual, regularOnly: false);

    /// <summary>
    /// Classifies the most severe difference between two outcome lists, optionally on the regular inputs only: the inputs
    /// with no zero and no <see cref="double.NaN"/> argument, where the symbolic identities of the S3 contract
    /// (<c>x - x</c>, <c>0 * x</c>, <c>0 / x</c>…) agree with IEEE evaluation unless a rule is wrong.
    /// </summary>
    /// <param name="expected">The outcomes of the original.</param>
    /// <param name="actual">The outcomes of the transformed tree.</param>
    /// <param name="regularOnly">Whether only the regular inputs are compared.</param>
    /// <returns>The most severe divergence.</returns>
    public static Divergence Compare(AuditOutcome[] expected, AuditOutcome[] actual, bool regularOnly)
    {
        Divergence worst = Divergence.None;
        for (int i = 0; i < expected.Length; i++)
        {
            if (regularOnly && (Inputs[i].X == 0 || Inputs[i].Y == 0 || double.IsNaN(Inputs[i].X) || double.IsNaN(Inputs[i].Y))) continue;
            Divergence current = Compare(expected[i], actual[i]);
            if (current > worst) worst = current;
        }

        return worst;
    }

    /// <summary>Classifies the difference between two outcomes.</summary>
    /// <param name="expected">The outcome of the original.</param>
    /// <param name="actual">The outcome of the transformed tree.</param>
    /// <returns>The divergence.</returns>
    private static Divergence Compare(AuditOutcome expected, AuditOutcome actual)
    {
        Divergence events = CompareEvents(expected.Events, actual.Events);
        if (events == Divergence.Effects) return Divergence.Effects;
        if (expected.Exception != actual.Exception) return Divergence.Exception;
        if (expected.Exception is not null) return events;

        Divergence value = CompareValues(expected.Value, actual.Value);
        return value > events ? value : events;
    }

    /// <summary>
    /// Compares two effect traces: the same effects in the same order, where an effect carrying a number (a binder report
    /// <c>name:value</c>) may differ by rounding (<see cref="Divergence.Rounding"/>) or by the IEEE class of its value
    /// (<see cref="Divergence.Ieee"/>); any other difference is <see cref="Divergence.Effects"/>.
    /// </summary>
    /// <param name="expected">The trace of the original.</param>
    /// <param name="actual">The trace of the transformed tree.</param>
    /// <returns>The divergence.</returns>
    private static Divergence CompareEvents(string[] expected, string[] actual)
    {
        if (expected.Length != actual.Length) return Divergence.Effects;
        Divergence worst = Divergence.None;
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] == actual[i]) continue;
            int a = expected[i].IndexOf(':'), b = actual[i].IndexOf(':');
            if (a < 0 || a != b || string.CompareOrdinal(expected[i], 0, actual[i], 0, a) != 0
                || !double.TryParse(expected[i][(a + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)
                || !double.TryParse(actual[i][(b + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
            {
                return Divergence.Effects;
            }

            Divergence current = CompareValues(x, y);
            if (current > worst) worst = current;
        }

        return worst;
    }

    /// <summary>Classifies the difference between two numbers.</summary>
    /// <param name="a">The expected number.</param>
    /// <param name="b">The actual number.</param>
    /// <returns>The divergence.</returns>
    private static Divergence CompareValues(double a, double b)
    {
        if (a.Equals(b)) return a == 0 && BitConverter.DoubleToInt64Bits(a) != BitConverter.DoubleToInt64Bits(b) ? Divergence.Rounding : Divergence.None;
        if (double.IsNaN(a) && double.IsNaN(b)) return Divergence.None;
        if (!double.IsFinite(a) || !double.IsFinite(b)) return Divergence.Ieee;
        if (a == b) return Divergence.Rounding;
        double scale = Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        return Math.Abs(a - b) <= 1e-9 * scale ? Divergence.Rounding : Divergence.Value;
    }

    /// <summary>Clones a tree into fresh nodes; with <paramref name="rename"/>, every declaration and label target is replaced by a fresh one.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="rename">Whether to alpha-rename binders and labels.</param>
    /// <returns>The clone.</returns>
    public static Expression Clone(Expression e, bool rename) => new Cloner(rename).Visit(e)!;

    /// <summary>Replaces every occurrence of one node instance (and of no other) by another expression, without entering extension nodes.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="target">The node to replace.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The rewritten tree.</returns>
    public static Expression ReplaceNode(Expression e, Expression target, Expression replacement) => new NodeReplacer(target, replacement).Visit(e)!;

    /// <summary>Lists the nodes of a tree in visit order, without entering extension nodes.</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The distinct nodes.</returns>
    public static List<Expression> Nodes(Expression e)
    {
        var collector = new NodeCollector();
        collector.Visit(e);
        return collector.Nodes;
    }

    /// <summary>Gets the free parameters of a tree (binder-aware), not entering extension nodes.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="enterQuotes">Whether quoted lambdas are searched.</param>
    /// <returns>The free parameters.</returns>
    public static HashSet<ParameterExpression> FreeParameters(Expression e, bool enterQuotes = true)
    {
        var collector = new FreeCollector(enterQuotes);
        collector.Visit(e);
        return collector.Free;
    }

    /// <summary>Tests whether <paramref name="parameter"/> is an assignment target anywhere in <paramref name="e"/>.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="parameter">The parameter.</param>
    /// <returns><see langword="true"/> if it is written.</returns>
    public static bool IsWritten(Expression e, ParameterExpression parameter)
        => Nodes(e).Any(node => node switch
        {
            BinaryExpression binary when IsAssignment(binary.NodeType) => ReferenceEquals(binary.Left, parameter),
            UnaryExpression unary when unary.NodeType is ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
                or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign => ReferenceEquals(unary.Operand, parameter),
            _ => false,
        });

    /// <summary>Tests whether a node type is an assignment.</summary>
    /// <param name="nodeType">The node type.</param>
    /// <returns><see langword="true"/> for <c>Assign</c> and the compound assignments.</returns>
    private static bool IsAssignment(ExpressionType nodeType) => nodeType is ExpressionType.Assign or ExpressionType.AddAssign
        or ExpressionType.SubtractAssign or ExpressionType.MultiplyAssign or ExpressionType.DivideAssign;

    /// <summary>
    /// Reduces a counterexample: repeatedly replaces a <see cref="double"/> node by <c>0</c>, <c>1</c> or one of its own
    /// <see cref="double"/> children while the reduced tree still compiles and still fails.
    /// </summary>
    /// <param name="body">The failing body.</param>
    /// <param name="fails">The failure predicate (receives a body).</param>
    /// <returns>A locally minimal failing body.</returns>
    public static Expression Shrink(Expression body, Func<Expression, bool> fails)
    {
        bool progress = true;
        int rounds = 0;
        while (progress && rounds++ < 200)
        {
            progress = false;
            foreach (Expression node in Nodes(body))
            {
                if (node.Type != typeof(double) || node is ParameterExpression || ExpressionOpacity.IsOpaque(node) || ReferenceEquals(node, body) && node is ConstantExpression) continue;

                foreach (Expression candidate in Candidates(node))
                {
                    Expression reduced;
                    try
                    {
                        reduced = ReplaceNode(body, node, candidate);
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        continue;
                    }

                    if (ReferenceEquals(reduced, body) || TryCompile(EtcAuditGenerator.Wrap(reduced), out _) is null) continue;

                    bool stillFails;
                    try
                    {
                        stillFails = fails(reduced);
                    }
                    catch
                    {
                        stillFails = false;
                    }

                    if (stillFails)
                    {
                        body = reduced;
                        progress = true;
                        break;
                    }
                }

                if (progress) break;
            }
        }

        return body;
    }

    /// <summary>The replacement candidates of a node during reduction.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Simpler expressions of the same type.</returns>
    private static IEnumerable<Expression> Candidates(Expression node)
    {
        if (node is not ConstantExpression)
        {
            yield return Expression.Constant(0.0);
            yield return Expression.Constant(1.0);
        }

        foreach (Expression child in DirectChildren(node))
        {
            if (child.Type == typeof(double)) yield return child;
        }
    }

    /// <summary>The direct expression children of a node, in a fixed order.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The children.</returns>
    private static IEnumerable<Expression> DirectChildren(Expression node) => node switch
    {
        BinaryExpression binary => [binary.Left, binary.Right],
        UnaryExpression { Operand: not null } unary => [unary.Operand],
        MethodCallExpression call => call.Arguments,
        ConditionalExpression conditional => [conditional.IfTrue, conditional.IfFalse],
        InvocationExpression invocation => invocation.Arguments.Concat(invocation.Expression is LambdaExpression lambda ? [lambda.Body] : []),
        BlockExpression block => [block.Result],
        DynamicExpression dynamic => dynamic.Arguments,
        TryExpression @try => [@try.Body, .. @try.Handlers.Select(handler => handler.Body)],
        _ => [],
    };

    /// <summary>Clones nodes, optionally alpha-renaming declarations and label targets.</summary>
    /// <param name="rename">Whether to rename.</param>
    private sealed class Cloner(bool rename) : ExpressionVisitor
    {
        /// <summary>The renamed declarations.</summary>
        private readonly Dictionary<ParameterExpression, Stack<ParameterExpression>> _parameters = new(ReferenceEqualityComparer.Instance);

        /// <summary>The renamed label targets.</summary>
        private readonly Dictionary<LabelTarget, LabelTarget> _labels = new(ReferenceEqualityComparer.Instance);

        /// <inheritdoc/>
        protected override Expression VisitConstant(ConstantExpression node) => Expression.Constant(node.Value, node.Type);

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
            => _parameters.TryGetValue(node, out Stack<ParameterExpression>? renamed) && renamed.Count > 0 ? renamed.Peek() : node;

        /// <inheritdoc/>
        /// <remarks>A plain <see cref="ExpressionVisitor"/> reaches a dynamic operation through this method (its <c>Accept</c> only dispatches to <c>VisitDynamic</c> for a dynamic-aware visitor), so its arguments are visited here.</remarks>
        protected override Expression VisitExtension(Expression node)
            => node is DynamicExpression dynamic ? dynamic.Update(dynamic.Arguments.Select(argument => Visit(argument)!)) : node;

        /// <inheritdoc/>
        protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
        {
            if (node is null || !rename) return node;
            if (!_labels.TryGetValue(node, out LabelTarget? renamed))
            {
                renamed = Expression.Label(node.Type, node.Name + "'");
                _labels[node] = renamed;
            }

            return renamed;
        }

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            ParameterExpression[] declarations = Declare(node.Parameters);
            try
            {
                return Expression.Lambda(node.Type, Visit(node.Body), node.Name, node.TailCall, declarations);
            }
            finally
            {
                Undeclare(node.Parameters);
            }
        }

        /// <inheritdoc/>
        protected override Expression VisitBlock(BlockExpression node)
        {
            ParameterExpression[] declarations = Declare(node.Variables);
            try
            {
                return Expression.Block(node.Type, declarations, node.Expressions.Select(statement => Visit(statement)));
            }
            finally
            {
                Undeclare(node.Variables);
            }
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            ParameterExpression[] declarations = node.Variable is null ? [] : Declare([node.Variable]);
            try
            {
                return Expression.MakeCatchBlock(node.Test, declarations.Length == 0 ? null : declarations[0], Visit(node.Body), node.Filter is null ? null : Visit(node.Filter));
            }
            finally
            {
                if (node.Variable is not null) Undeclare([node.Variable]);
            }
        }

        /// <inheritdoc/>
        protected override Expression VisitMemberInit(MemberInitExpression node)
            => Expression.MemberInit((NewExpression)Visit(node.NewExpression), node.Bindings.Select(VisitMemberBinding));

        /// <inheritdoc/>
        protected override Expression VisitListInit(ListInitExpression node)
            => Expression.ListInit((NewExpression)Visit(node.NewExpression), node.Initializers.Select(VisitElementInit));

        /// <summary>Declares fresh instances for a binder.</summary>
        /// <param name="declarations">The original declarations.</param>
        /// <returns>The declarations to use.</returns>
        private ParameterExpression[] Declare(IReadOnlyList<ParameterExpression> declarations)
        {
            var result = new ParameterExpression[declarations.Count];
            for (int i = 0; i < result.Length; i++)
            {
                ParameterExpression original = declarations[i];
                ParameterExpression fresh = rename ? Expression.Parameter(original.IsByRef ? original.Type.MakeByRefType() : original.Type, original.Name + "'") : original;
                if (!_parameters.TryGetValue(original, out Stack<ParameterExpression>? stack)) _parameters[original] = stack = new Stack<ParameterExpression>();
                stack.Push(fresh);
                result[i] = fresh;
            }

            return result;
        }

        /// <summary>Leaves a binder.</summary>
        /// <param name="declarations">The original declarations.</param>
        private void Undeclare(IReadOnlyList<ParameterExpression> declarations)
        {
            foreach (ParameterExpression original in declarations) _parameters[original].Pop();
        }
    }

    /// <summary>Replaces one node instance.</summary>
    /// <param name="target">The node to replace.</param>
    /// <param name="replacement">The replacement.</param>
    private sealed class NodeReplacer(Expression target, Expression replacement) : ExpressionVisitor
    {
        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (ReferenceEquals(node, target)) return replacement;
            return node is not null && ExpressionOpacity.IsOpaque(node) ? node : base.Visit(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitExtension(Expression node)
            => node is DynamicExpression dynamic ? dynamic.Update(dynamic.Arguments.Select(argument => Visit(argument)!)) : node;

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Expression body = Visit(node.Body)!;
            return ReferenceEquals(body, node.Body) ? node : Expression.Lambda(node.Type, body, node.Name, node.TailCall, node.Parameters);
        }
    }

    /// <summary>Collects distinct nodes in visit order.</summary>
    private sealed class NodeCollector : ExpressionVisitor
    {
        /// <summary>The visited set.</summary>
        private readonly HashSet<Expression> _seen = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets the nodes.</summary>
        public List<Expression> Nodes { get; } = [];

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;
            if (_seen.Add(node)) Nodes.Add(node);
            return ExpressionOpacity.IsOpaque(node) ? node : base.Visit(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitExtension(Expression node)
        {
            if (node is DynamicExpression dynamic)
            {
                foreach (Expression argument in dynamic.Arguments) Visit(argument);
            }

            return node;
        }
    }

    /// <summary>Collects free parameters, binder-aware.</summary>
    /// <param name="enterQuotes">Whether quoted lambdas are searched.</param>
    private sealed class FreeCollector(bool enterQuotes) : ExpressionVisitor
    {
        /// <summary>The declarations in scope with their counts.</summary>
        private readonly Dictionary<ParameterExpression, int> _declared = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets the free parameters.</summary>
        public HashSet<ParameterExpression> Free { get; } = new(ReferenceEqualityComparer.Instance);

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node) => node is not null && ExpressionOpacity.IsOpaque(node) ? node : base.Visit(node);

        /// <inheritdoc/>
        protected override Expression VisitExtension(Expression node)
        {
            if (node is DynamicExpression dynamic)
            {
                foreach (Expression argument in dynamic.Arguments) Visit(argument);
            }

            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_declared.ContainsKey(node)) Free.Add(node);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitUnary(UnaryExpression node)
            => node.NodeType == ExpressionType.Quote && !enterQuotes ? node : base.VisitUnary(node);

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Scoped(node.Parameters, () => Visit(node.Body));
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitBlock(BlockExpression node)
        {
            Scoped(node.Variables, () => Visit(node.Expressions));
            return node;
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            Scoped(node.Variable is null ? [] : [node.Variable], () =>
            {
                Visit(node.Filter);
                Visit(node.Body);
            });
            return node;
        }

        /// <summary>Visits with declarations in scope.</summary>
        /// <param name="declarations">The declarations.</param>
        /// <param name="visit">The visit.</param>
        private void Scoped(IReadOnlyList<ParameterExpression> declarations, Action visit)
        {
            foreach (ParameterExpression declaration in declarations) _declared[declaration] = _declared.TryGetValue(declaration, out int count) ? count + 1 : 1;
            try
            {
                visit();
            }
            finally
            {
                foreach (ParameterExpression declaration in declarations)
                {
                    if (--_declared[declaration] == 0) _declared.Remove(declaration);
                }
            }
        }
    }
}
