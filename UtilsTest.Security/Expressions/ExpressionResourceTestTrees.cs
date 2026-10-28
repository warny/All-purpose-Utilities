using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Utils.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>Tree builders and helpers shared by the ETC R1a-Security resource tests.</summary>
internal static class ExpressionResourceTestTrees
{
    /// <summary>The free parameter of every tree.</summary>
    public static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary><see cref="Math.Abs(double)"/>.</summary>
    public static readonly MethodInfo AbsMethod = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;

    /// <summary><see cref="Opaque(double)"/>, a method the simplifier has no rule for.</summary>
    public static readonly System.Reflection.MethodInfo OpaqueMethod = typeof(ExpressionResourceTestTrees).GetMethod(nameof(Opaque))!;

    /// <summary>An identity function the simplifier knows nothing about, so a chain of its calls survives simplification.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Opaque(double value) => value;

    /// <summary>Builds <c>Opaque(Opaque(...(p)))</c> with <paramref name="levels"/> calls: depth and node count are <c>levels + 1</c>, and simplification keeps it.</summary>
    /// <param name="levels">The number of calls.</param>
    /// <returns>The chain.</returns>
    public static Expression CallChain(int levels) => Chain(levels, static e => Expression.Call(OpaqueMethod, e));

    /// <summary>Builds <c>-(-(...(p)))</c> with <paramref name="levels"/> negations: depth and node count are <c>levels + 1</c>.</summary>
    /// <param name="levels">The number of negations.</param>
    /// <returns>The chain.</returns>
    public static Expression NegateChain(int levels) => Chain(levels, static e => Expression.Negate(e));

    /// <summary>Wraps <see cref="P"/> <paramref name="levels"/> times with <paramref name="wrap"/>, iteratively.</summary>
    /// <param name="levels">The number of wrappers.</param>
    /// <param name="wrap">The wrapper factory.</param>
    /// <returns>The chain.</returns>
    public static Expression Chain(int levels, Func<Expression, Expression> wrap)
    {
        Expression e = P;
        for (int i = 0; i < levels; i++) e = wrap(e);
        return e;
    }

    /// <summary>
    /// Builds a DAG of <paramref name="levels"/> additions whose two operands are the same node: <c>levels + 1</c>
    /// distinct nodes and <c>2^(levels + 1) - 1</c> logical nodes.
    /// </summary>
    /// <param name="levels">The number of additions.</param>
    /// <returns>The DAG root.</returns>
    public static Expression SharedDag(int levels)
    {
        Expression e = P;
        for (int i = 0; i < levels; i++) e = Expression.Add(e, e);
        return e;
    }

    /// <summary>Builds the left-associated sum <c>x0 + x1 + ... </c> of <paramref name="terms"/> distinct parameters.</summary>
    /// <param name="terms">The number of terms.</param>
    /// <returns>The sum and its parameters.</returns>
    public static (Expression Sum, ParameterExpression[] Parameters) LeftSum(int terms)
    {
        ParameterExpression[] x = Enumerable.Range(0, terms).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression sum = x[0];
        for (int i = 1; i < terms; i++) sum = Expression.Add(sum, x[i]);
        return (sum, x);
    }

    /// <summary>
    /// Builds the S5 "g" family (equal coefficients) <c>3*x0 - x1*3 - 3*x2 - ...</c>, whose simplification work grows
    /// exponentially with <paramref name="terms"/> while depth and size stay tiny (R0 §7.4).
    /// </summary>
    /// <param name="terms">The number of terms.</param>
    /// <returns>The chain.</returns>
    public static Expression EqualCoefficientChain(int terms)
    {
        ParameterExpression[] x = Enumerable.Range(0, terms).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression acc = Expression.Multiply(Expression.Constant(3.0), x[0]);
        for (int i = 1; i < terms; i++)
        {
            acc = Expression.Subtract(acc, i % 2 == 0 ? Expression.Multiply(Expression.Constant(3.0), x[i]) : Expression.Multiply(x[i], Expression.Constant(3.0)));
        }

        return acc;
    }

    /// <summary>Limits with the given depth and node limits and the default budgets.</summary>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The node limit.</param>
    /// <returns>The limits.</returns>
    public static ExpressionResourceLimits Shape(int maxDepth = int.MaxValue, long maxNodes = long.MaxValue)
        => ExpressionResourceLimits.Default with { MaxDepth = maxDepth, MaxNodes = maxNodes };

    /// <summary>Runs <paramref name="action"/> on a dedicated thread with an explicit stack size and rethrows its exception.</summary>
    /// <param name="action">The action.</param>
    /// <param name="stackSize">The thread stack size in bytes.</param>
    /// <param name="timeout">The maximum duration.</param>
    public static void RunOnThread(Action action, int stackSize, TimeSpan timeout)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, stackSize);
        thread.Start();
        if (!thread.Join(timeout)) throw new TimeoutException("The worker thread did not finish in time.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>A generic transformer that recurses into every child through <c>PrepareExpression</c> and rebuilds nothing new.</summary>
    internal sealed class RecursiveTransformer : ExpressionTransformer
    {
        /// <summary>Initializes the transformer with default limits.</summary>
        public RecursiveTransformer()
        {
        }

        /// <summary>Initializes the transformer with explicit limits.</summary>
        /// <param name="limits">The limits.</param>
        public RecursiveTransformer(ExpressionResourceLimits limits)
            : base(limits)
        {
        }

        /// <summary>Gets the limits of this instance.</summary>
        public ExpressionResourceLimits Limits => ResourceLimits;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <summary>Calls the protected substitution entry point.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }
}
