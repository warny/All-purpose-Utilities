using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the binary pair finishing of refused chains (mission B, <c>docs/expressions/SimplifierFlatten.md</c> §23):
/// its pre-test never reads an extension node nor a custom reflection object, a hostile node is only ever prepared as the next
/// call prepares it, its state never leaks out of an operation, a refusal or a thread, a shared DAG never multiplies its work,
/// and its work is counted and refused just above each limit.
/// </summary>
[TestClass]
public class ExpressionSimplifierBinaryFinishingResourceTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>
    /// Builds <c>(x0 / q) + y1 + ... + yn + (-2 * x) + x * (2 + z)</c>, left-associated, iteratively: the quotient refuses every
    /// level, the last canonicalization sorts the two products together at the end of the spine, and only a binary rule factors
    /// them (the sum <c>2 + z</c> refuses the global factoring).
    /// </summary>
    /// <param name="terms">The number of parameters between the quotient and the pair.</param>
    /// <returns>The chain.</returns>
    private static Expression PairChain(int terms)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x"), z = Expression.Parameter(typeof(double), "z");
        Expression chain = Expression.Divide(Expression.Parameter(typeof(double), "x0"), Expression.Parameter(typeof(double), "q"));
        for (int i = 1; i <= terms; i++) chain = Expression.Add(chain, Expression.Parameter(typeof(double), "y" + i));
        chain = Expression.Add(chain, Expression.Multiply(Expression.Constant(-2.0), x));
        return Expression.Add(chain, Expression.Multiply(x, Expression.Add(Expression.Constant(2.0), z)));
    }

    /// <summary>Simplifies <paramref name="e"/> inside an outer operation and returns the visit counters of the whole call.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="e">The expression.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Count(ExpressionSimplifier simplifier, Expression e)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, e), static state =>
        {
            state.simplifier.Simplify(state.e);
            return (ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>Runs <see cref="ExpressionTransformer.FinishRefusedChain"/> on a result inside an operation of a new simplifier.</summary>
    /// <param name="result">The result of the levels.</param>
    /// <returns>The finished result.</returns>
    private static Expression Finish(Expression result)
    {
        var simplifier = new ExpressionSimplifier(NoLimits);
        return ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, result), static state => state.simplifier.FinishRefusedChain(state.result, state.result));
    }

    /// <summary>Runs <paramref name="action"/> with the binary finishing switched off or on, on this thread.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="finishing">Whether the binary finishing runs.</param>
    /// <param name="action">The action.</param>
    /// <returns>Its result.</returns>
    private static T WithFinishing<T>(bool finishing, Func<T> action)
    {
        ExpressionSimplifier.RefusedBinaryFinishingDisabled = !finishing;
        try
        {
            return action();
        }
        finally
        {
            ExpressionSimplifier.RefusedBinaryFinishingDisabled = false;
        }
    }

    /// <summary>An extension node whose every member throws once armed.</summary>
    private sealed class ArmedExtension : Expression
    {
        /// <summary>Gets or sets whether the members throw.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of calls made while armed.</summary>
        public int Calls { get; private set; }

        /// <summary>Counts a call and throws when armed.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value returned while not armed.</param>
        /// <returns><paramref name="value"/>.</returns>
        private T Hit<T>(T value)
        {
            if (!Armed) return value;
            Calls++;
            throw new InvalidOperationException("A member of an extension node was called.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Hit(ExpressionType.Extension);

        /// <inheritdoc/>
        public override Type Type => Hit(typeof(double));

        /// <inheritdoc/>
        public override bool CanReduce => Hit(false);

        /// <inheritdoc/>
        public override string ToString() => Hit("extension");
    }

    /// <summary>A plain multiplication, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The product.</returns>
    public static double Times(double x, double y) => x * y;

    /// <summary>
    /// The pre-test reads the result with type tests only: a last pair holding an armed extension node, a call of an impersonating
    /// method or a product carrying an impersonating operator never has any member of them read by the search, whether or not
    /// the pair shares a factor; an ordinary pair is factored.
    /// </summary>
    [TestMethod]
    public void PreTest_NeverReadsHostileNodes()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        Expression quotient = Expression.Divide(b, c);
        Expression sum = Expression.Add(Expression.Constant(2.0), c);
        var extension = new ArmedExtension();
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierBinaryFinishingResourceTests).GetMethod(nameof(Times))!);

        Expression ordinary = Expression.Add(quotient, Expression.Add(Expression.Multiply(Expression.Constant(-2.0), a), Expression.Multiply(a, sum)));
        Assert.AreEqual("(b / c) + (c * a)", ExpressionTraceFormatter.Format(Finish(ordinary)));

        // No shared factor, or an opaque one (never a shared factor): the search reads nothing of the hostile nodes and returns the
        // result itself.
        Expression[] unshared =
        [
            Expression.Add(quotient, Expression.Add(Expression.Multiply(b, extension), Expression.Multiply(a, sum))),
            Expression.Add(quotient, Expression.Add(extension, Expression.Multiply(a, sum))),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(b, Expression.Call(callMethod, c)), Expression.Multiply(a, sum))),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(b, c, operatorMethod), Expression.Multiply(a, sum))),
            Expression.Add(quotient, Expression.Add(extension, extension)),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(extension, b), Expression.Multiply(extension, c))),
        ];
        extension.Armed = true;
        callMethod.Trap.Reset();
        callMethod.Trap.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression result in unshared) Assert.AreSame(result, Finish(result));
        }
        finally
        {
            // See ExpressionFlattenResourceTests: an armed impersonating method must not stay in the framework's caches.
            callMethod.Trap.Armed = false;
            operatorMethod.Trap.Armed = false;
            extension.Armed = false;
        }

        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, callMethod.Trap.ArmedCalls + callMethod.Trap.TypeReads);
        Assert.AreEqual(0, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads);
    }

    /// <summary>
    /// A pair sharing a factor whose other factor is a call of an impersonating method is never transformed again by the finishing
    /// (its candidate must be made of runtime reflection objects only): the result is the historical one, the method is never
    /// invoked, and the same pair with the runtime method is finished.
    /// </summary>
    [TestMethod]
    public void HostilePair_IsNeverTransformedAgain()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        Expression Source(MethodInfo method)
            => Expression.Add(Expression.Add(Expression.Divide(b, c), Expression.Multiply(a, Expression.Call(method, c))), Expression.Multiply(a, Expression.Add(Expression.Constant(2.0), c)));

        Expression hostile = Source(callMethod);
        callMethod.Trap.Reset();
        string finished = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(hostile));
        string historical = WithFinishing(false, () => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(hostile)));
        Assert.AreEqual(historical, finished);
        Assert.AreEqual(0, callMethod.Trap.Evaluations, "The impersonated method is never invoked.");

        Expression runtime = Source(sin);
        string reference = WithFinishing(false, () =>
        {
            var simplifier = new ExpressionSimplifier();
            return ExpressionTraceFormatter.Format(simplifier.Simplify(simplifier.Simplify(runtime)));
        });
        Assert.AreEqual(reference, ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(runtime)));
        Assert.AreNotEqual(WithFinishing(false, () => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(runtime))), reference);
    }

    /// <summary>Outside an operation, inside a comparer simplification and for a derived simplifier, the binary finishing does nothing.</summary>
    [TestMethod]
    public void BinaryFinishing_OutsideAnOperation_ReturnsTheResult()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x"), y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
        Expression result = Expression.Add(Expression.Divide(y, z), Expression.Add(Expression.Multiply(Expression.Constant(-2.0), x), Expression.Multiply(x, Expression.Add(Expression.Constant(2.0), z))));
        Assert.AreSame(result, new ExpressionSimplifier().FinishRefusedChain(result, result));
        Assert.AreNotSame(result, Finish(result));
    }

    /// <summary>
    /// The work of the binary finishing is counted: each budget refuses the simplification just above its limit with an exception
    /// of the right kind, every limit between the historical work and the finished work refuses inside the finishing, and the
    /// marker of the inside of a refused chain is cleared after every refusal.
    /// </summary>
    [TestMethod]
    public void BinaryFinishing_IsCounted_AndRefusedJustAboveEachLimit()
    {
        Expression chain = PairChain(16);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform - 1 }).Simplify(chain)).Kind);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 }).Simplify(chain)).Kind);

        long historical = WithFinishing(false, () => Count(new ExpressionSimplifier(NoLimits), chain).Transform);
        Assert.IsTrue(transform > historical, $"{historical} then {transform} transformation visits");
        for (long limit = historical; limit < transform; limit++)
        {
            var limited = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = limit });
            Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain)).Kind);
            Assert.IsFalse(ExpressionTransformer.WithinRefusedChain, "A refusal leaves the state cleared.");
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }

        // The finishing costs at most the next call it replaces: one call of the branch against two calls of the reference.
        long twoCalls = WithFinishing(false, () =>
        {
            var simplifier = new ExpressionSimplifier(NoLimits);
            return ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, chain), static state =>
            {
                state.simplifier.Simplify(state.simplifier.Simplify(state.chain));
                return ExpressionResourceBudget.TransformVisits;
            });
        });
        Assert.IsTrue(transform <= twoCalls, $"one call {transform}, two reference calls {twoCalls}");
    }

    /// <summary>
    /// The marker of the inside of a refused chain, set while the candidate and the prefix are run again, never leaks: a direct
    /// call restores the caller's value, and a nested operation is the boundary of its own chain.
    /// </summary>
    [TestMethod]
    public void BinaryState_NeverLeaks_ThroughNestedOperations()
    {
        Expression chain = PairChain(4);
        string expected = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain));
        ExpressionTransformer.WithinRefusedChain = true;
        try
        {
            Assert.AreEqual(expected, ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain)), "A nested operation is the boundary of its own chain.");
            Assert.IsTrue(ExpressionTransformer.WithinRefusedChain, "The caller's state is restored by the operation.");
            ParameterExpression x = Expression.Parameter(typeof(double), "x"), y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
            Expression result = Expression.Add(Expression.Divide(y, z), Expression.Add(Expression.Multiply(Expression.Constant(-2.0), x), Expression.Multiply(x, Expression.Add(Expression.Constant(2.0), z))));
            Finish(result);
            Assert.IsTrue(ExpressionTransformer.WithinRefusedChain, "The caller's state is restored by the finishing.");
        }
        finally
        {
            ExpressionTransformer.WithinRefusedChain = false;
        }
    }

    /// <summary>
    /// A shared DAG whose paths multiply never multiplies the work of the pre-test: a group operand that is such a DAG is read up to
    /// its first terms only, and two distinct large factors are never compared, so the finishing returns after a bounded number
    /// of visits; the whole simplification of the DAG behaves as with the finishing switched off.
    /// </summary>
    [TestMethod]
    public void ExplosiveDags_AreTestedAfterBoundedWork()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        Expression Explosive()
        {
            Expression shared = Expression.Add(Expression.Divide(a, b), Expression.Divide(a, c));
            for (int i = 0; i < 40; i++) shared = Expression.Add(shared, shared);
            return shared;
        }

        // Two distinct instances of one large product (the multiplicative DAG of the same depth is not used: the whole-chain test
        // of mission C3c already walks such a term, finishing or not).
        Expression Product()
        {
            Expression product = Expression.Multiply(a, b);
            for (int i = 0; i < 400; i++) product = Expression.Multiply(product, i % 2 == 0 ? a : b);
            return product;
        }

        Expression[] results =
        [
            Expression.Add(Expression.Divide(a, c), Expression.Add(Explosive(), b)),
            Expression.Add(Expression.Divide(a, c), Expression.Add(Expression.Multiply(Product(), b), Expression.Multiply(Product(), c))),
        ];
        foreach (Expression result in results)
        {
            // The visits the binary finishing adds to the earlier finishings of the boundary (M1, M4), which walk the result anyway.
            long Visits(bool finishing) => WithFinishing(finishing, () => ExpressionResourceBudget.Run(NoLimits, null, null, null, result, static r =>
            {
                long before = ExpressionResourceBudget.StructuralVisits;
                new ExpressionSimplifier(NoLimits).FinishRefusedChain(r, r);
                return ExpressionResourceBudget.StructuralVisits - before;
            }));

            long added = Visits(finishing: true) - Visits(finishing: false);
            Assert.IsTrue(added < 1_000, $"{added} structural visits added by the binary finishing");
        }

        string Outcome(Expression source, bool finishing)
            => WithFinishing(finishing, () =>
            {
                try
                {
                    return ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source));
                }
                catch (ExpressionResourceLimitException exception)
                {
                    return "refused: " + exception.Kind;
                }
            });

        // The whole simplification of the group DAG.
        Assert.AreEqual(Outcome(results[0], finishing: false), Outcome(results[0], finishing: true));
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, the binary finishing never turns a refused chain the levels simplify into a stack
    /// refusal: at every size where the simplification without it (the result of <c>master</c>) succeeds, the finished one succeeds
    /// too.
    /// </summary>
    [TestMethod]
    public void BinaryFinishing_NeverRefusesWhatTheLevelsAccept_OnASmallStack()
    {
        var outcomes = new List<string>();
        RunOnThread(() =>
        {
            foreach (int terms in new[] { 64, 128, 256, 512, 1_024 })
            {
                Expression chain = PairChain(terms);
                bool levels = Succeeds(chain, finishing: false);
                bool finished = Succeeds(chain, finishing: true);
                outcomes.Add($"{terms}: levels {levels}, finished {finished}");
                Assert.IsTrue(finished || !levels, string.Join("; ", outcomes));
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);
            }
        }, SmallStack, Timeout);
    }

    /// <summary>Simplifies <paramref name="chain"/> without limits, with or without the binary finishing.</summary>
    /// <param name="chain">The chain.</param>
    /// <param name="finishing">Whether the binary finishing runs.</param>
    /// <returns><see langword="true"/> on success, <see langword="false"/> on a stack refusal.</returns>
    private static bool Succeeds(Expression chain, bool finishing)
        => WithFinishing(finishing, () =>
        {
            try
            {
                new ExpressionSimplifier(NoLimits).Simplify(chain);
                return true;
            }
            catch (ExpressionResourceLimitException exception) when (exception.Kind == ExpressionResourceLimitKind.Stack)
            {
                return false;
            }
        });

    /// <summary>
    /// Re-entrance and isolation: refused chains with and without a pair to finish, one under a call and one inside the argument
    /// of another chain, simplified concurrently on several threads by one instance, give the trees of a sequential run.
    /// </summary>
    [TestMethod]
    public void BinaryFinishing_IsIsolatedBetweenThreads()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x"), y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
        Expression group = Expression.Add(Expression.Add(Expression.Divide(x, z), y), Expression.Divide(x, z));
        Expression none = Expression.Add(Expression.Add(Expression.Divide(x, y), Expression.Multiply(x, y)), Expression.Multiply(z, Expression.Constant(3.0)));
        Expression underCall = Expression.Add(Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, PairChain(6)), group);
        Expression nested = Expression.Multiply(PairChain(3), Expression.Add(group, PairChain(2)));
        Expression[] sources = [group, none, underCall, nested, PairChain(24)];
        string[] expected = sources.Select(source => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source))).ToArray();

        var simplifier = new ExpressionSimplifier();
        var failures = new List<string>();
        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            int index = i % sources.Length;
            string actual = ExpressionTraceFormatter.Format(simplifier.Simplify(sources[index]));
            if (actual != expected[index])
            {
                lock (failures) failures.Add($"{index}: {actual}");
            }
        });

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(5)));
    }
}
