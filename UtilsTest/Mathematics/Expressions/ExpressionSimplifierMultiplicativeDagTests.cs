using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Legitimate multiplicative DAGs (mission "multiplicative DAG resource bounds", <c>docs/expressions/SimplifierFlatten.md</c>
/// §24): a product shared within the allowance of the factorable test simplifies exactly as its tree copy, under every policy,
/// and a product whose shared nodes multiply its occurrences beyond it keeps the values of the source, a stable result and
/// the result of its tree copy whenever the levels reach it.
/// </summary>
[TestClass]
public class ExpressionSimplifierMultiplicativeDagTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression Q = Expression.Parameter(typeof(double), "q");

    /// <summary>The parameters every sample binds, in order.</summary>
    private static readonly ParameterExpression[] Parameters = [X, Y, A, B, Q];

    /// <summary>The samples: moderate values, so that the powers of the deepest products stay finite.</summary>
    private static readonly double[][] Samples =
    [
        [1.01, 0.99, 2.0, -3.0, 4.0],
        [-1.002, 1.003, 0.5, 0.25, -2.0],
        [0.999, -1.001, -1.5, 7.0, 3.0],
    ];

    /// <summary>The policies the results must not depend on.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Builds <c>s = x * y</c>, then <c>s = s * s</c> <paramref name="levels"/> times, shared or as a tree.</summary>
    /// <param name="levels">The number of squarings.</param>
    /// <param name="shared">Whether both operands of each squaring are the same instance.</param>
    /// <returns>The product.</returns>
    private static Expression Product(int levels, bool shared)
    {
        Expression Tree(int level) => level == 0 ? Expression.Multiply(X, Y) : Expression.Multiply(Tree(level - 1), Tree(level - 1));
        if (!shared) return Tree(levels);
        Expression result = Expression.Multiply(X, Y);
        for (int level = 0; level < levels; level++) result = Expression.Multiply(result, result);
        return result;
    }

    /// <summary>The families of the mission, built over one product.</summary>
    /// <param name="product">The shared or copied product.</param>
    /// <param name="copy">A second instance of the product (the same instance for a DAG).</param>
    /// <returns>The named trees.</returns>
    private static IEnumerable<(string Name, Expression Tree)> Families(Expression product, Expression copy)
    {
        yield return ("product", product);
        yield return ("refused sum", Expression.Add(Expression.Divide(A, Q), product));
        yield return ("M4 suffix", Expression.Add(Expression.Divide(A, Q), Expression.Add(product, Expression.Multiply(Expression.Constant(2.0), A))));
        yield return ("two products", Expression.Add(Expression.Multiply(product, A), Expression.Multiply(copy, B)));
        yield return ("binary pair", Expression.Add(Expression.Add(Expression.Divide(A, Q), Expression.Multiply(product, A)), Expression.Multiply(copy, B)));
        yield return ("no common factor", Expression.Add(Expression.Add(Expression.Divide(A, Q), product), Expression.Multiply(A, B)));
    }

    /// <summary>Evaluates <paramref name="e"/> on a sample.</summary>
    /// <param name="e">The expression over <see cref="Parameters"/>.</param>
    /// <param name="sample">The values.</param>
    /// <returns>The value.</returns>
    private static double Evaluate(Expression e, double[] sample)
        => (double)Expression.Lambda(e, Parameters).Compile().DynamicInvoke(sample.Cast<object>().ToArray())!;

    /// <summary>Asserts that two values agree up to the reassociation latitude of the symbolic contract.</summary>
    /// <param name="expected">The value of the source.</param>
    /// <param name="actual">The value of the result.</param>
    /// <param name="message">The context.</param>
    private static void AssertClose(double expected, double actual, string message)
        => Assert.IsTrue(Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{message}: {expected} then {actual}");

    /// <summary>
    /// Within the plain walk of the factorable test (at most 64 product occurrences per term), every family simplifies a
    /// shared product exactly as its tree copy, under every policy, with the values of the source and a stable result.
    /// </summary>
    [TestMethod]
    public void SmallSharedProducts_SimplifyAsTheirTreeCopies()
    {
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            for (int levels = 0; levels <= 4; levels++)
            {
                Expression dag = Product(levels, shared: true);
                var shared = Families(dag, dag).ToArray();
                var copied = Families(Product(levels, shared: false), Product(levels, shared: false)).ToArray();
                for (int index = 0; index < shared.Length; index++)
                {
                    string context = $"{shared[index].Name}, {levels} levels";
                    Expression fromDag = simplifier.Simplify(shared[index].Tree);
                    Expression fromTree = simplifier.Simplify(copied[index].Tree);
                    Assert.AreEqual(ExpressionTraceFormatter.Format(fromTree), ExpressionTraceFormatter.Format(fromDag), context);
                    foreach (double[] sample in Samples) AssertClose(Evaluate(shared[index].Tree, sample), Evaluate(fromDag, sample), context);
                }
            }
        }
    }

    /// <summary>
    /// Beyond the allowance, the factorable family refuses the shared product and the levels simplify it: the result keeps the
    /// values of the source, its second pass is the result itself, and it is the result of the tree copy.
    /// </summary>
    [TestMethod]
    public void ExplosiveSharedProducts_KeepTheirValuesAndResults()
    {
        var simplifier = new ExpressionSimplifier();
        for (int levels = 5; levels <= 8; levels++)
        {
            Expression dag = Product(levels, shared: true);
            var shared = Families(dag, dag).ToArray();
            var copied = Families(Product(levels, shared: false), Product(levels, shared: false)).ToArray();
            for (int index = 0; index < shared.Length; index++)
            {
                string context = $"{shared[index].Name}, {levels} levels";
                Expression result = simplifier.Simplify(shared[index].Tree);
                foreach (double[] sample in Samples) AssertClose(Evaluate(shared[index].Tree, sample), Evaluate(result, sample), context);
                Assert.AreEqual(ExpressionTraceFormatter.Format(result), ExpressionTraceFormatter.Format(simplifier.Simplify(result)), context + " (second pass)");
                Assert.AreEqual(ExpressionTraceFormatter.Format(simplifier.Simplify(copied[index].Tree)), ExpressionTraceFormatter.Format(result), context + " (tree copy)");
            }
        }
    }

    /// <summary>
    /// The integral types keep their products (no power rule): a shared product over <see cref="long"/> and <see cref="int"/>
    /// gives the result of its tree copy and wraps like the source.
    /// </summary>
    [TestMethod]
    public void SharedIntegralProducts_SimplifyAsTheirTreeCopies()
    {
        foreach (Type type in new[] { typeof(int), typeof(long) })
        {
            ParameterExpression i = Expression.Parameter(type, "i"), j = Expression.Parameter(type, "j"), k = Expression.Parameter(type, "k");
            Expression Tree(int level) => level == 0 ? Expression.Multiply(i, j) : Expression.Multiply(Tree(level - 1), Tree(level - 1));
            for (int levels = 0; levels <= 3; levels++)
            {
                Expression dag = Expression.Multiply(i, j);
                for (int level = 0; level < levels; level++) dag = Expression.Multiply(dag, dag);
                Expression sharedSum = Expression.Add(Expression.Multiply(dag, k), Expression.Multiply(dag, i));
                Expression copiedSum = Expression.Add(Expression.Multiply(Tree(levels), k), Expression.Multiply(Tree(levels), i));
                Expression result = new ExpressionSimplifier().Simplify(sharedSum);
                Assert.AreEqual(ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(copiedSum)), ExpressionTraceFormatter.Format(result), $"{type.Name}, {levels} levels");

                object[] values = type == typeof(int) ? [7, -3, 11] : [7L, -3L, 11L];
                object? Run(Expression e) => Expression.Lambda(e, i, j, k).Compile().DynamicInvoke(values);
                Assert.AreEqual(Run(sharedSum), Run(result), $"{type.Name}, {levels} levels (value)");
            }
        }
    }
}
