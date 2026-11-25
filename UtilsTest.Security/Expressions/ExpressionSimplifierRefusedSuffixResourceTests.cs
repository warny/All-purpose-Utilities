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
/// Security invariants of the suffix finishing of refused chains (mission M4, <c>docs/expressions/SimplifierFlatten.md</c> §22):
/// it never reads an extension node nor a custom reflection object it does not already read, its state never leaks out of an
/// operation, a refusal or a thread, a shared DAG never multiplies its work, and its work is counted, linear in the result
/// and refused just above each limit.
/// </summary>
[TestClass]
public class ExpressionSimplifierRefusedSuffixResourceTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>
    /// Builds <c>((2*x1 + x0/q) + 2*x2) + 2*x3 + ...</c>, left-associated, iteratively: every node holds the quotient, so the
    /// chain is refused at every level, and the canonical result <c>(x0/q) + (2*x1 + (2*x2 + ...))</c> ends with a factorable
    /// suffix (family M4-1).
    /// </summary>
    /// <param name="terms">The number of products.</param>
    /// <returns>The chain.</returns>
    private static Expression SuffixChain(int terms)
    {
        ParameterExpression x0 = Expression.Parameter(typeof(double), "x0"), q = Expression.Parameter(typeof(double), "q");
        Expression chain = Expression.Add(Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "x1")), Expression.Divide(x0, q));
        for (int i = 2; i <= terms; i++) chain = Expression.Add(chain, Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "x" + i)));
        return chain;
    }

    /// <summary>
    /// Builds the canonical result the levels give <see cref="SuffixChain"/>: <c>(x0/q) + (2*x1 + (2*x2 + ...))</c>,
    /// right-associated, iteratively.
    /// </summary>
    /// <param name="terms">The number of products.</param>
    /// <returns>The result.</returns>
    private static Expression LevelResult(int terms)
    {
        Expression chain = Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "x" + terms));
        for (int i = terms - 1; i >= 1; i--) chain = Expression.Add(Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "x" + i)), chain);
        return Expression.Add(Expression.Divide(Expression.Parameter(typeof(double), "x0"), Expression.Parameter(typeof(double), "q")), chain);
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
    /// The search reads the result with type tests only: a suffix holding an armed extension node, a call of an impersonating
    /// method or a product carrying an impersonating operator is not factorable, so the result is returned as is without any
    /// member of them being read; the same result with an ordinary suffix is factored.
    /// </summary>
    [TestMethod]
    public void SuffixSearch_NeverReadsHostileNodes()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        Expression quotient = Expression.Divide(b, c);
        var extension = new ArmedExtension();
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierRefusedSuffixResourceTests).GetMethod(nameof(Times))!);

        Expression ordinary = Expression.Add(quotient, Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, c)));
        Assert.AreEqual("(b / c) + (a * (b + c))", ExpressionTraceFormatter.Format(Finish(ordinary)));

        Expression[] hostile =
        [
            Expression.Add(quotient, Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, extension))),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(a, b), extension)),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, Expression.Call(callMethod, c)))),
            Expression.Add(quotient, Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, c, operatorMethod))),
        ];
        extension.Armed = true;
        callMethod.Trap.Reset();
        callMethod.Trap.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression result in hostile) Assert.AreSame(result, Finish(result));
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
    /// A prefix holding an impersonating call is prepared again as the next call prepares it: its method is never invoked nor
    /// trusted (its provenance is checked first), and the factorable suffix after it is factored exactly as with the runtime
    /// method, which the simplifier does not distinguish by name.
    /// </summary>
    [TestMethod]
    public void HostilePrefix_IsPreparedAsTheNextCallPreparesIt()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        Expression suffix = Expression.Add(Expression.Multiply(a, b), Expression.Multiply(a, c));
        Expression result = Expression.Add(Expression.Call(callMethod, c), suffix);

        callMethod.Trap.Reset();
        Expression finished = Finish(result);
        Assert.AreEqual(0, callMethod.Trap.Evaluations, "The impersonated method is never invoked.");
        StringAssert.Contains(ExpressionTraceFormatter.Format(finished), "a * (b + c)");
    }

    /// <summary>Outside an operation and for a derived simplifier, the suffix finishing does nothing.</summary>
    [TestMethod]
    public void SuffixFinishing_OutsideAnOperation_ReturnsTheResult()
    {
        Expression result = LevelResult(4);
        Assert.AreSame(result, new ExpressionSimplifier().FinishRefusedChain(result, result));
    }

    /// <summary>
    /// The work of the suffix finishing is counted: each budget refuses the simplification just above its limit with an
    /// exception of the right kind, and the finishing alone (one bottom-up pass, one linear and one factorable walk, one
    /// canonicalization and one prefix level) stays linear in the result.
    /// </summary>
    [TestMethod]
    public void SuffixFinishing_IsCounted_AndRefusedJustAboveEachLimit()
    {
        Expression chain = SuffixChain(32);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform - 1 }).Simplify(chain)).Kind);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 }).Simplify(chain)).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);

        long Finishing(int terms)
        {
            Expression levels = LevelResult(terms);
            return ExpressionResourceBudget.Run(NoLimits, null, null, null, levels, static result =>
            {
                long before = ExpressionResourceBudget.StructuralVisits;
                new ExpressionSimplifier(NoLimits).FinishRefusedChain(result, result);
                return ExpressionResourceBudget.StructuralVisits - before;
            });
        }

        // A deep factored sum is keyed recursively by the canonical order: a large stack, so that only the counters are measured.
        long small = 0, large = 0;
        RunOnThread(() =>
        {
            small = Finishing(1_000);
            large = Finishing(4_000);
        }, 64 * 1024 * 1024, Timeout);
        Assert.IsTrue(large < 5 * small, $"{small} then {large} structural visits for four times the terms");
    }

    /// <summary>
    /// The marker of the inside of a refused chain, set while the prefix is run again, never leaks: a refusal of the budget in
    /// the middle of the prefix leaves it cleared, a direct call restores the caller's value, and the instance finishes again
    /// after a refusal.
    /// </summary>
    [TestMethod]
    public void SuffixState_NeverLeaks_ThroughRefusalsAndNestedOperations()
    {
        Expression chain = SuffixChain(16);
        (long transform, _) = Count(new ExpressionSimplifier(NoLimits), chain);
        ExpressionSimplifier.RefusedSuffixFinishingDisabled = true;
        long historical;
        try
        {
            historical = Count(new ExpressionSimplifier(NoLimits), chain).Transform;
        }
        finally
        {
            ExpressionSimplifier.RefusedSuffixFinishingDisabled = false;
        }

        // Every limit between the historical work and the finished work refuses inside the suffix finishing.
        Assert.IsTrue(transform > historical, $"{historical} then {transform} transformation visits");
        for (long limit = historical; limit < transform; limit++)
        {
            var limited = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = limit });
            Assert.AreEqual(ExpressionResourceLimitKind.Work, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain)).Kind);
            Assert.IsFalse(ExpressionTransformer.WithinRefusedChain, "A refusal leaves the state cleared.");
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }

        string expected = ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain));
        ExpressionTransformer.WithinRefusedChain = true;
        try
        {
            Assert.AreEqual(expected, ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(chain)), "A nested operation is the boundary of its own chain.");
            Assert.IsTrue(ExpressionTransformer.WithinRefusedChain, "The caller's state is restored by the operation.");
            Expression levels = LevelResult(4);
            var simplifier = new ExpressionSimplifier(NoLimits);
            ExpressionResourceBudget.Run(NoLimits, simplifier, null, null, (simplifier, levels), static state => state.simplifier.FinishRefusedChain(state.levels, state.levels));
            Assert.IsTrue(ExpressionTransformer.WithinRefusedChain, "The caller's state is restored by the finishing.");
        }
        finally
        {
            ExpressionTransformer.WithinRefusedChain = false;
        }
    }

    /// <summary>
    /// A shared DAG whose paths multiply (each node the sum of the same node twice) never multiplies the work of the search: the
    /// factorable test refuses it once the revisits exceed the distinct nodes, so the finishing returns the result after a number
    /// of visits linear in the distinct nodes, and the whole simplification behaves as with the finishing switched off.
    /// </summary>
    [TestMethod]
    public void ExplosiveDag_IsRefusedAfterLinearWork()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        Expression shared = Expression.Add(Expression.Multiply(Expression.Constant(2.0), a), Expression.Multiply(Expression.Constant(2.0), b));
        for (int i = 0; i < 40; i++) shared = Expression.Add(shared, shared);
        Expression result = Expression.Add(Expression.Divide(a, c), shared);
        long visits = ExpressionResourceBudget.Run(NoLimits, null, null, null, result, static r =>
        {
            long before = ExpressionResourceBudget.StructuralVisits;
            new ExpressionSimplifier(NoLimits).FinishRefusedChain(r, r);
            return ExpressionResourceBudget.StructuralVisits - before;
        });
        Assert.IsTrue(visits < 20_000, $"{visits} structural visits for 41 distinct crossed nodes");

        string Outcome(bool finishing)
        {
            ExpressionSimplifier.RefusedSuffixFinishingDisabled = !finishing;
            try
            {
                return ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(result));
            }
            catch (ExpressionResourceLimitException exception)
            {
                return "refused: " + exception.Kind;
            }
            finally
            {
                ExpressionSimplifier.RefusedSuffixFinishingDisabled = false;
            }
        }

        Assert.AreEqual(Outcome(finishing: false), Outcome(finishing: true));
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, the suffix finishing never turns a refused chain the levels simplify into a stack
    /// refusal: at every size where the simplification without it (the result of <c>master</c>) succeeds, the finished one
    /// succeeds too.
    /// </summary>
    [TestMethod]
    public void SuffixFinishing_NeverRefusesWhatTheLevelsAccept_OnASmallStack()
    {
        var outcomes = new List<string>();
        RunOnThread(() =>
        {
            foreach (int terms in new[] { 64, 128, 256, 512, 1_024, 2_048 })
            {
                Expression chain = SuffixChain(terms);
                bool levels = Succeeds(chain, finishing: false);
                bool finished = Succeeds(chain, finishing: true);
                outcomes.Add($"{terms}: levels {levels}, finished {finished}");
                Assert.IsTrue(finished || !levels, string.Join("; ", outcomes));
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);
            }
        }, SmallStack, Timeout);
    }

    /// <summary>Simplifies <paramref name="chain"/> without limits, with or without the suffix finishing.</summary>
    /// <param name="chain">The chain.</param>
    /// <param name="finishing">Whether the suffix finishing runs.</param>
    /// <returns><see langword="true"/> on success, <see langword="false"/> on a stack refusal.</returns>
    private static bool Succeeds(Expression chain, bool finishing)
    {
        ExpressionSimplifier.RefusedSuffixFinishingDisabled = !finishing;
        try
        {
            new ExpressionSimplifier(NoLimits).Simplify(chain);
            return true;
        }
        catch (ExpressionResourceLimitException exception) when (exception.Kind == ExpressionResourceLimitKind.Stack)
        {
            return false;
        }
        finally
        {
            ExpressionSimplifier.RefusedSuffixFinishingDisabled = false;
        }
    }

    /// <summary>
    /// The state is per thread: refused chains with and without a factorable suffix, simplified concurrently on several threads,
    /// give the trees of a sequential run.
    /// </summary>
    [TestMethod]
    public void SuffixFinishing_IsIsolatedBetweenThreads()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x"), y = Expression.Parameter(typeof(double), "y"), z = Expression.Parameter(typeof(double), "z");
        Expression nested1179 = Expression.Subtract(Expression.Multiply(Expression.Constant(-2.0), y), Expression.Add(Expression.Divide(y, Expression.Constant(0.0)), Expression.Multiply(Expression.Constant(2.0), z)));
        Expression none = Expression.Add(Expression.Add(Expression.Divide(x, y), Expression.Multiply(x, y)), Expression.Multiply(z, Expression.Constant(3.0)));
        Expression underCall = Expression.Add(Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, SuffixChain(6)), nested1179);
        Expression[] sources = [nested1179, none, underCall, SuffixChain(24)];
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
