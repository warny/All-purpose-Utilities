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
/// Security invariants of the multiplicative DAGs (mission "multiplicative DAG resource bounds",
/// <c>docs/expressions/SimplifierFlatten.md</c> §24): a product whose shared nodes multiply its occurrences
/// (<c>s = s * s</c>, n + 1 distinct product nodes, 2^(n+1) - 1 occurrences) is decided by the factorable test after a
/// number of visits linear in its distinct nodes, the boundary finishing never collects its factors once per path, the
/// multiplicative factor collection is counted by the structural budget, no hostile node is read, and no state leaks out of
/// a refusal, a nested operation or a thread.
/// </summary>
[TestClass]
public class ExpressionSimplifierMultiplicativeDagResourceTests
{
    /// <summary>The stack size of the small-stack worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread: a walk of the occurrences of a deep DAG would never finish.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>
    /// The limits of the walks that must stay linear: no shape limit, and visit budgets far above the linear work, so that a
    /// regression (a walk of the occurrences) fails the test with a resource exception instead of blocking the job.
    /// </summary>
    private static readonly ExpressionResourceLimits SafetyNet = ExpressionResourceLimits.Unlimited with { MaxTransformVisits = 5_000_000, MaxStructuralVisits = 5_000_000 };

    /// <summary>
    /// Runs <paramref name="action"/> on a background thread with an explicit stack size and rethrows its exception: a thread
    /// that does not finish in time fails the test and never keeps the test host alive.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="stackSize">The thread stack size in bytes.</param>
    private static void RunBounded(Action action, int stackSize)
    {
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, stackSize) { IsBackground = true };
        thread.Start();
        if (!thread.Join(Timeout)) throw new TimeoutException("The worker thread did not finish in time.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>The parameters of the trees.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x"), Y = Expression.Parameter(typeof(double), "y"),
        A = Expression.Parameter(typeof(double), "a"), B = Expression.Parameter(typeof(double), "b"), C = Expression.Parameter(typeof(double), "c");

    /// <summary>
    /// Builds the multiplicative DAG <c>s = leaf * y</c>, then <c>s = s * s</c> <paramref name="levels"/> times, shared or
    /// rebuilt as a tree (small <paramref name="levels"/> only for a tree).
    /// </summary>
    /// <param name="levels">The number of squarings.</param>
    /// <param name="shared">Whether both operands of each squaring are the same instance.</param>
    /// <param name="leaf">The leftmost leaf; <c>x</c> by default.</param>
    /// <param name="last">The rightmost leaf of the base product; <c>y</c> by default.</param>
    /// <returns>The product.</returns>
    private static Expression Dag(int levels, bool shared = true, Expression? leaf = null, Expression? last = null)
    {
        Expression Base() => Expression.Multiply(leaf ?? X, last ?? Y);
        Expression Tree(int level) => level == 0 ? Base() : Expression.Multiply(Tree(level - 1), Tree(level - 1));
        if (!shared) return Tree(levels);
        Expression result = Base();
        for (int level = 0; level < levels; level++) result = Expression.Multiply(result, result);
        return result;
    }

    /// <summary>Runs the admission of a chain root inside an operation of a new simplifier and returns its decision and its structural visits.</summary>
    /// <param name="root">The raw root.</param>
    /// <returns>Whether the chain is admitted, and the structural visits of the admission.</returns>
    private static (bool Admitted, long Visits) Admission(Expression root)
    {
        var simplifier = new ExpressionSimplifier(SafetyNet);
        return ExpressionResourceBudget.Run(SafetyNet, simplifier, null, null, (simplifier, root), static state =>
        {
            long before = ExpressionResourceBudget.StructuralVisits;
            bool admitted = state.simplifier.FlattenAdmission(state.root) == ExpressionRuleRejection.None;
            return (admitted, ExpressionResourceBudget.StructuralVisits - before);
        });
    }

    /// <summary>Runs the boundary finishing of a result inside an operation of a new simplifier.</summary>
    /// <param name="result">The result.</param>
    /// <param name="limits">The limits of the operation.</param>
    /// <returns>The finished result and the structural visits of the finishing.</returns>
    private static (Expression Finished, long Visits) Finish(Expression result, ExpressionResourceLimits limits)
    {
        var simplifier = new ExpressionSimplifier(limits);
        return ExpressionResourceBudget.Run(limits, simplifier, null, null, (simplifier, result), static state =>
        {
            long before = ExpressionResourceBudget.StructuralVisits;
            Expression finished = state.simplifier.FinishRefusedChain(state.result, state.result);
            return (finished, ExpressionResourceBudget.StructuralVisits - before);
        });
    }

    /// <summary>
    /// A factorable chain whose two terms hold a 40-level shared product (2^41 product occurrences each) is decided by the
    /// admission after a number of visits linear in the distinct nodes: the walk of the occurrences would never finish.
    /// </summary>
    [TestMethod]
    public void ExplosiveSharedProduct_IsDecidedAfterLinearWork()
    {
        Expression dag = Dag(40);
        Expression chain = Expression.Add(Expression.Multiply(dag, A), Expression.Multiply(dag, B));
        (bool admitted, long visits) = (false, 0L);
        RunBounded(() => (admitted, visits) = Admission(chain), 16 * 1024 * 1024);
        Assert.IsFalse(admitted, "The revisits of the shared product exceed its distinct nodes.");
        Assert.IsTrue(visits < 1_000, $"{visits} structural visits for 42 distinct product nodes");
    }

    /// <summary>
    /// The decision over the distinct nodes is the decision of the walk of the occurrences: every tree, and every DAG up to
    /// 64 product occurrences, is decided as its tree copy, for ordinary terms, two calls (counted once per occurrence), a
    /// leaf of another type, a non-finite constant and a custom operator; above, a DAG whose revisits exceed its distinct
    /// nodes is refused while its tree copy keeps the decision of <c>master</c>.
    /// </summary>
    [TestMethod]
    public void SharedProducts_AreDecidedAsTheirTreeCopies_WithinTheAllowance()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        MethodInfo times = typeof(ExpressionSimplifierMultiplicativeDagResourceTests).GetMethod(nameof(Times))!;
        var variants = new (string Name, Func<int, bool, Expression> Build, bool Factorable)[]
        {
            ("ordinary", (n, shared) => Dag(n, shared), true),
            ("call", (n, shared) => Dag(n, shared, Expression.Call(sin, X)), true),
            ("nan", (n, shared) => Dag(n, shared, last: Expression.Constant(double.NaN)), false),
            ("convert", (n, shared) => Dag(n, shared, last: Expression.Convert(Expression.Parameter(typeof(int), "i"), typeof(double))), false),
            ("custom", (n, shared) => Dag(n, shared, last: Expression.Multiply(Y, Y, times)), false),
        };

        foreach ((string name, Func<int, bool, Expression> build, bool factorable) in variants)
        {
            for (int levels = 0; levels <= 7; levels++)
            {
                Expression ChainOf(Expression product) => Expression.Add(Expression.Multiply(product, A), Expression.Multiply(Expression.Constant(2.0), A));
                bool tree = Admission(ChainOf(build(levels, false))).Admitted;
                bool dag = Admission(ChainOf(build(levels, true))).Admitted;

                // A call leaf is a factor once per occurrence: two calls from the first squaring on, in the tree as in the DAG.
                bool expected = factorable && (name != "call" || levels == 0);
                Assert.AreEqual(expected, tree, $"{name} tree, {levels} levels");

                // Up to 5 levels (at most 64 product occurrences with the product by a) the DAG is walked as its tree copy; above,
                // its revisits exceed its distinct nodes and it is refused.
                Assert.AreEqual(levels <= 5 && tree, dag, $"{name} DAG, {levels} levels");
            }
        }
    }

    /// <summary>
    /// A product shared a few times inside a large tree (its revisits within its distinct nodes) keeps the decision of its tree
    /// copy above the plain walk, so the allowance never refuses an ordinary term.
    /// </summary>
    [TestMethod]
    public void ModeratelySharedProduct_KeepsTheDecisionOfItsTreeCopy()
    {
        ParameterExpression[] parameters = Enumerable.Range(0, 80).Select(i => Expression.Parameter(typeof(double), "p" + i)).ToArray();
        Expression shared = Expression.Multiply(X, Y);
        Expression Product(Expression twice) => parameters.Aggregate(Expression.Multiply(twice, twice), (acc, p) => Expression.Multiply(acc, p));
        Expression copy = Expression.Multiply(Expression.Multiply(X, Y), Expression.Multiply(X, Y));
        Expression ChainOf(Expression product) => Expression.Add(product, Expression.Multiply(Expression.Constant(2.0), X));

        Assert.IsTrue(Admission(ChainOf(Product(shared))).Admitted);
        Assert.IsTrue(Admission(ChainOf(parameters.Aggregate(copy, (acc, p) => Expression.Multiply(acc, p)))).Admitted);
    }

    /// <summary>
    /// The boundary finishing of a result holding the explosive product in a factorable suffix (missions C3c, M4 and B) returns
    /// after linear work, far below its safety-net budget: the suffix is refused by its product test, so the global factoring never collects the
    /// factors of the product once per path.
    /// </summary>
    [TestMethod]
    public void BoundaryFinishing_OfExplosiveProduct_IsLinear()
    {
        Expression dag = Dag(40);
        Expression[] results =
        [
            Expression.Add(Expression.Multiply(dag, A), Expression.Multiply(dag, B)),
            Expression.Add(Expression.Divide(A, C), Expression.Add(Expression.Multiply(dag, A), Expression.Multiply(dag, B))),
            Expression.Add(Expression.Divide(A, C), Expression.Add(dag, Expression.Multiply(Expression.Constant(2.0), A))),
        ];

        foreach (Expression result in results)
        {
            (Expression finished, long visits) = (result, 0L);
            RunBounded(() => (finished, visits) = Finish(result, SafetyNet), 16 * 1024 * 1024);
            Assert.AreSame(result, finished);
            Assert.IsTrue(visits < 2_000, $"{visits} structural visits");
        }
    }

    /// <summary>
    /// The public entry points under the default limits refuse the explosive inputs at once by their logical node count, and,
    /// with the node limit lifted, by a visit budget well before any walk of all the occurrences; the engine state is restored
    /// after each refusal and the next simplification succeeds.
    /// </summary>
    [TestMethod]
    public void ExplosiveInputs_AreRefusedByALimit_AndTheStateIsRestored()
    {
        Expression dag = Dag(40);
        Expression[] inputs =
        [
            dag,
            Expression.Add(Expression.Divide(A, C), dag),
            Expression.Add(Expression.Divide(A, C), Expression.Add(dag, Expression.Multiply(Expression.Constant(2.0), A))),
            Expression.Add(Expression.Multiply(dag, A), Expression.Multiply(dag, B)),
        ];

        RunBounded(() =>
        {
            foreach (Expression input in inputs)
            {
                Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(input)).Kind);
                var nodesOff = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue });
                ExpressionResourceLimitKind kind = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => nodesOff.Simplify(input)).Kind;
                Assert.IsTrue(kind is ExpressionResourceLimitKind.Work or ExpressionResourceLimitKind.StructuralWork, kind.ToString());
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
                Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);
                Assert.IsNotNull(nodesOff.Simplify(Expression.Add(Expression.Multiply(X, Y), Expression.Multiply(X, A))));
            }
        }, 16 * 1024 * 1024);
    }

    /// <summary>
    /// The multiplicative factor collection consumes one structural visit per expanded product: the simplification of a large
    /// product is refused by the structural budget just below its own count, with the exception of that budget, and succeeds at
    /// that count; the counters report the expanded products.
    /// </summary>
    [TestMethod]
    public void FactorCollection_IsCountedByTheStructuralBudget()
    {
        ParameterExpression i = Expression.Parameter(typeof(long), "i"), j = Expression.Parameter(typeof(long), "j");
        Expression product = Enumerable.Range(0, 40).Aggregate((Expression)i, (acc, k) => Expression.Multiply(acc, k % 2 == 0 ? j : Expression.Parameter(typeof(long), "k" + k)));
        long structural = ExpressionResourceBudget.Run(NoLimits, null, null, null, product, static e =>
        {
            new ExpressionSimplifier(NoLimits).Simplify(e);
            return ExpressionResourceBudget.StructuralVisits;
        });

        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(product));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 }).Simplify(product)).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);

        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            new ExpressionSimplifier(NoLimits).Simplify(product);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        Assert.IsTrue(statistics.CollectedProducts >= 40, $"{statistics.CollectedProducts} expanded products");
    }

    /// <summary>
    /// Runs the admission of <c>term + 2 * d</c> with the admission counters installed, the guard on or off (the walk of the
    /// occurrences of <c>master</c>).
    /// </summary>
    /// <param name="term">The product term under test.</param>
    /// <param name="guardDisabled">Whether <see cref="ExpressionSimplifier.MultiplicativeDagGuardDisabled"/> is set.</param>
    /// <returns>Whether the chain is admitted, and the number of terms decided over their distinct product nodes.</returns>
    private static (bool Admitted, long SharedProductTests) AdmissionOfTerm(Expression term, bool guardDisabled = false)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        ExpressionSimplifier.MultiplicativeDagGuardDisabled = guardDisabled;
        try
        {
            bool admitted = Admission(Expression.Add(term, Expression.Multiply(Expression.Constant(2.0), D))).Admitted;
            return (admitted, statistics.SharedProductTests);
        }
        finally
        {
            ExpressionSimplifier.MultiplicativeDagGuardDisabled = false;
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>The parameter of the second term of <see cref="AdmissionOfTerm"/>, never a factor of a term under test.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");

    /// <summary>Distinct parameters filling the product chains of the boundary tests.</summary>
    private static readonly ParameterExpression[] Fillers = Enumerable.Range(0, 128).Select(i => Expression.Parameter(typeof(double), "f" + i)).ToArray();

    /// <summary>
    /// Builds the left-deep product of <paramref name="factors"/> factors: each shared node of <paramref name="shared"/> used
    /// as many times as given (the same instance each time), then distinct parameters.
    /// </summary>
    /// <param name="factors">The number of factors of the chain (its product nodes are one fewer).</param>
    /// <param name="shared">The shared factors and their numbers of uses.</param>
    /// <returns>The product.</returns>
    private static Expression ChainOf(int factors, params (Expression Node, int Uses)[] shared)
    {
        var list = shared.SelectMany(s => Enumerable.Repeat(s.Node, s.Uses)).ToList();
        Assert.IsTrue(list.Count <= factors, "Too many shared uses for the chain.");
        list.AddRange(Fillers.Take(factors - list.Count));
        return list.Aggregate(Expression.Multiply);
    }

    /// <summary>The distinct and logical counts of the product nodes of a term (memoized by reference), to pin the cases.</summary>
    /// <param name="term">The term.</param>
    /// <returns>The distinct product nodes and the product occurrences.</returns>
    private static (int Distinct, long Occurrences) ProductCounts(Expression term)
    {
        var occurrences = new Dictionary<Expression, long>(ReferenceEqualityComparer.Instance);
        long Count(Expression e)
        {
            if (e is not BinaryExpression { NodeType: ExpressionType.Multiply } product) return 0;
            if (occurrences.TryGetValue(product, out long known)) return known;
            long count = 1 + Count(product.Left) + Count(product.Right);
            occurrences[product] = count;
            return count;
        }

        long total = Count(term);
        return (occurrences.Count, total);
    }

    /// <summary>
    /// The calls of a term are counted once per occurrence by the test over distinct nodes: a product of more than 64
    /// ordinary multiplications holding a single call is admitted, and one holding a shared sub-product that holds a call
    /// (two occurrences of the call, one revisit) is refused, as by the walk of the occurrences; both are decided by the new
    /// test.
    /// </summary>
    [TestMethod]
    public void CallMultiplicities_AreCountedPerOccurrence_PastThePlainWalk()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        Expression oneCall = Expression.Multiply(ChainOf(70), Expression.Call(sin, X));
        Expression sharedCall = Expression.Multiply(Expression.Call(sin, X), Y);
        Expression twoCalls = ChainOf(70, (sharedCall, 2));
        Expression twoDistinctCalls = Expression.Multiply(ChainOf(70), Expression.Multiply(Expression.Call(sin, X), Expression.Call(sin, Y)));

        Assert.IsTrue(ProductCounts(oneCall).Occurrences > 64);
        Assert.IsTrue(ProductCounts(twoCalls).Occurrences > 64);

        (bool admitted, long tests) = AdmissionOfTerm(oneCall);
        Assert.IsTrue(admitted, "One call in a product of 70 factors.");
        Assert.IsTrue(tests > 0, "The term is decided over its distinct product nodes.");
        Assert.IsTrue(AdmissionOfTerm(oneCall, guardDisabled: true).Admitted, "The walk of the occurrences admits it too.");

        (admitted, tests) = AdmissionOfTerm(twoCalls);
        Assert.IsFalse(admitted, "A shared sub-product holding a call is two calls.");
        Assert.IsTrue(tests > 0);
        Assert.IsFalse(AdmissionOfTerm(twoCalls, guardDisabled: true).Admitted);

        (admitted, tests) = AdmissionOfTerm(twoDistinctCalls);
        Assert.IsFalse(admitted, "Two distinct calls.");
        Assert.IsTrue(tests > 0);
        Assert.IsFalse(AdmissionOfTerm(twoDistinctCalls, guardDisabled: true).Admitted);
    }

    /// <summary>
    /// The plain walk decides up to 64 product occurrences, the test over distinct nodes beyond: <c>s = x * y</c> squared five
    /// times (6 distinct products, 63 occurrences, 57 revisits) is admitted, so is its product by one parameter (7, 64), and
    /// its product by two parameters (8, 65) is refused by its revisits; the first two are decided by the plain walk alone.
    /// </summary>
    [TestMethod]
    public void PlainWalkBoundary_At63_64_And65Occurrences()
    {
        Expression dag = Dag(5);
        var cases = new (Expression Term, int Distinct, long Occurrences, bool Admitted, bool SharedTest)[]
        {
            (dag, 6, 63, true, false),
            (Expression.Multiply(dag, Fillers[0]), 7, 64, true, false),
            (Expression.Multiply(Expression.Multiply(dag, Fillers[0]), Fillers[1]), 8, 65, false, true),
        };

        foreach ((Expression term, int distinct, long occurrences, bool expected, bool sharedTest) in cases)
        {
            Assert.AreEqual((distinct, occurrences), ProductCounts(term), "The case is the intended one.");
            (bool admitted, long tests) = AdmissionOfTerm(term);
            Assert.AreEqual(expected, admitted, $"{occurrences} occurrences");
            Assert.AreEqual(sharedTest, tests > 0, $"{occurrences} occurrences: test over distinct nodes");
            Assert.IsTrue(AdmissionOfTerm(term, guardDisabled: true).Admitted, $"{occurrences} occurrences: master admits every case");
        }
    }

    /// <summary>
    /// Past the plain walk, a term is admitted while its revisits stay within <c>max(16, distinct product nodes)</c> and refused
    /// one revisit beyond, whatever the sharing pattern; the floor of 16 is never the bound there, since a term of more than 64
    /// occurrences has more distinct product nodes than 16 whenever its revisits are at most 48. Every case is decided by the
    /// test over distinct nodes, and the admitted ones are admitted by the walk of the occurrences too.
    /// </summary>
    [TestMethod]
    public void SharedRevisitAllowance_IsExactPastThePlainWalk()
    {
        // u has two product nodes (each use revisits two), v one: 41 chain factors (40 chain products) + 3 shared products.
        Expression u = Expression.Multiply(Expression.Multiply(A, B), C);
        Expression v = Expression.Multiply(X, Y);
        var cases = new (Expression Term, int Distinct, long Occurrences, bool Admitted)[]
        {
            (ChainOf(41, (u, 22), (v, 2)), 43, 86, true),   // revisits 2 * 21 + 1 = 43 = distinct
            (ChainOf(41, (u, 23), (v, 1)), 43, 87, false),  // revisits 2 * 22 + 0 = 44 = distinct + 1
            (ChainOf(41, (u, 21), (v, 3)), 43, 85, true),   // revisits 42 = distinct - 1
            (ChainOf(70), 69, 69, true),                    // a tree: no revisit
        };

        foreach ((Expression term, int distinct, long occurrences, bool expected) in cases)
        {
            Assert.AreEqual((distinct, occurrences), ProductCounts(term), "The case is the intended one.");
            (bool admitted, long tests) = AdmissionOfTerm(term);
            Assert.AreEqual(expected, admitted, $"{distinct} distinct, {occurrences} occurrences");
            Assert.IsTrue(tests > 0, "Decided over distinct nodes.");
            Assert.IsTrue(AdmissionOfTerm(term, guardDisabled: true).Admitted, "Every case is a factorable product for master.");
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
    /// A deep shared product whose rightmost leaf is an armed extension node, a call of an impersonating method or a product
    /// carrying an impersonating operator is refused by type tests and the provenance check only, in linear work: no member of
    /// the hostile objects is read, whether the walk refuses on the hostile leaf or on the revisits first.
    /// </summary>
    [TestMethod]
    public void HostileLeaves_OfSharedProducts_AreNeverRead()
    {
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var extension = new ArmedExtension();
        var callMethod = new ImpersonatingMethod(sin);
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierMultiplicativeDagResourceTests).GetMethod(nameof(Times))!);
        Expression[] leaves = [extension, Expression.Call(callMethod, Y), Expression.Multiply(Y, Y, operatorMethod)];
        Expression[] roots = leaves.SelectMany(leaf => new[] { 3, 40 }.Select(levels =>
            (Expression)Expression.Add(Expression.Multiply(Dag(levels, last: leaf), A), Expression.Multiply(Expression.Constant(2.0), A)))).ToArray();

        extension.Armed = true;
        callMethod.Trap.Reset();
        callMethod.Trap.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression root in roots)
            {
                (bool admitted, long visits) = (true, 0L);
                RunBounded(() => (admitted, visits) = Admission(root), 16 * 1024 * 1024);
                Assert.IsFalse(admitted);
                Assert.IsTrue(visits < 1_000, $"{visits} structural visits");
            }
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
    /// On a 256 KiB thread, the shared product test is iterative: the admission of a 400-level shared product and the
    /// simplification of the moderate DAG families never fail on the stack.
    /// </summary>
    [TestMethod]
    public void SharedProductTest_IsIterative_OnASmallStack()
    {
        RunBounded(() =>
        {
            Expression deep = Dag(400);
            Assert.IsFalse(Admission(Expression.Add(Expression.Multiply(deep, A), Expression.Multiply(deep, B))).Admitted);
            Expression moderate = Dag(8);
            Assert.IsNotNull(new ExpressionSimplifier().Simplify(Expression.Add(Expression.Multiply(moderate, A), Expression.Multiply(moderate, B))));
            Assert.IsNotNull(new ExpressionSimplifier().Simplify(Expression.Add(Expression.Divide(A, C), Expression.Add(moderate, Expression.Multiply(Expression.Constant(2.0), A)))));
        }, SmallStack);
    }

    /// <summary>
    /// The state of the test is local to a call: shared and copied products, in nested operations (an outer operation's
    /// comparer) and concurrently on several threads, give the trees of a sequential run, and an operation refused by the
    /// structural budget in the middle of the factorable test leaves no state behind.
    /// </summary>
    [TestMethod]
    public void SharedProductTest_IsIsolated_BetweenThreadsRefusalsAndNestedOperations()
    {
        Expression dag = Dag(6), tree = Dag(6, shared: false);
        Expression[] sources =
        [
            Expression.Add(Expression.Multiply(dag, A), Expression.Multiply(dag, B)),
            Expression.Add(Expression.Multiply(tree, A), Expression.Multiply(tree, B)),
            Expression.Add(Expression.Divide(A, C), Expression.Add(dag, Expression.Multiply(Expression.Constant(2.0), A))),
            Expression.Add(Expression.Multiply(Dag(2), A), Expression.Multiply(Dag(2), B)),
        ];
        string[] expected = sources.Select(source => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source))).ToArray();

        // A refusal at every structural limit below the work of the first source leaves the engine as it was.
        long structural = ExpressionResourceBudget.Run(NoLimits, null, null, null, sources[0], static e =>
        {
            new ExpressionSimplifier(NoLimits).Simplify(e);
            return ExpressionResourceBudget.StructuralVisits;
        });
        for (long limit = 1; limit < structural; limit += Math.Max(1, structural / 50))
        {
            var limited = new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = limit });
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(sources[0])).Kind);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            Assert.IsFalse(ExpressionTransformer.WithinRefusedChain);
        }

        // Nested: the simplification inside an outer operation gives the same trees.
        string[] nested = ExpressionResourceBudget.Run(NoLimits, null, null, null, sources, static all
            => all.Select(source => ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(source))).ToArray());
        CollectionAssert.AreEqual(expected, nested);

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
