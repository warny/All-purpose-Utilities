using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The spine chains of the flatten admission (mission C3b, <c>docs/expressions/SimplifierFlatten.md</c> §12): combs of linear
/// terms whose terms repeat, admitted only where no signature rule can apply at any level, and simplified by finalizing each
/// level above their linear sub-chain, which gives exactly the level by level result.
/// </summary>
/// <remarks>
/// The reference is <see cref="LevelByLevelSimplifier"/>, a derived simplifier without any member: its plan holds no flatten
/// rule, so it simplifies level by level. Trees are compared through <see cref="Dump"/>.
/// </remarks>
[TestClass]
public class ExpressionSimplifierFlattenSpineTests
{
    /// <summary>
    /// Mission C3c: the factorable family supersedes the families tested here (linear, anchored and spine chains) on the chains
    /// it admits; it is skipped on the test thread so that they keep being validated as the reference and the fallback they are.
    /// </summary>
    [TestInitialize]
    public void DisableFactorableChains() => ExpressionSimplifier.FactorableChainsDisabled = true;

    /// <summary>Restores the factorable family on the test thread.</summary>
    [TestCleanup]
    public void RestoreFactorableChains() => ExpressionSimplifier.FactorableChainsDisabled = false;

    /// <summary>A derived simplifier without any member: the level by level reference.</summary>
    private sealed class LevelByLevelSimplifier : ExpressionSimplifier
    {
        /// <summary>Merges identical monomials as the built-in type does (mission C3c), so that only the flatten rule differs.</summary>
        internal override bool MergesMonomials => true;

        /// <summary>Initializes a simplifier with the default limits.</summary>
        public LevelByLevelSimplifier()
        {
        }

        /// <summary>Initializes a simplifier with <paramref name="limits"/>.</summary>
        /// <param name="limits">The resource limits.</param>
        public LevelByLevelSimplifier(ExpressionResourceLimits limits)
            : base(limits)
        {
        }
    }

    private static readonly ParameterExpression[] D = [.. Enumerable.Range(0, 140).Select(static i => Expression.Parameter(typeof(double), "d" + i))];
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>The policies of the comparisons: the default and the explicit ones.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    private static ConstantExpression C<T>(T value) => Expression.Constant(value, typeof(T));

    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(Expression.Add);

    private static Expression RightSum(IEnumerable<Expression> terms) => terms.Reverse().Aggregate((acc, term) => Expression.Add(term, acc));

    /// <summary>A stable textual dump of a tree.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The dump.</returns>
    private static string Dump(Expression? expression) => expression switch
    {
        null => "null",
        ParameterExpression parameter => parameter.Name + ":" + parameter.Type.Name,
        ConstantExpression constant => "C(" + constant.Type.Name + ":" + Convert.ToString(constant.Value, CultureInfo.InvariantCulture) + ")",
        BinaryExpression binary => binary.NodeType + "(" + Dump(binary.Left) + "," + Dump(binary.Right) + ")",
        UnaryExpression unary => unary.NodeType + "(" + Dump(unary.Operand) + ")",
        MethodCallExpression call => call.Method.Name + "(" + string.Join(",", call.Arguments.Select(Dump)) + ")",
        LambdaExpression lambda => "L(" + string.Join(",", lambda.Parameters.Select(Dump)) + "=>" + Dump(lambda.Body) + ")",
        _ => expression.NodeType.ToString(),
    };

    /// <summary>Simplifies <paramref name="tree"/> with admission counters installed.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="options">The policy.</param>
    /// <returns>The result and the counters.</returns>
    private static (Expression Result, FlattenAdmissionStatistics Statistics) Simplify(Expression tree, ExpressionSimplifierOptions? options = null)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            return (new ExpressionSimplifier { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(tree), statistics);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>Asserts the result of the built-in simplifier is the level by level one, for both passes and every policy.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="spine">Whether the root must be admitted as a spine chain (<see langword="null"/>: not checked).</param>
    private static void AssertLevelByLevel(Expression tree, bool? spine)
    {
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            var reference = new LevelByLevelSimplifier { Options = options };
            Expression expected = reference.Simplify(tree);
            (Expression actual, FlattenAdmissionStatistics statistics) = Simplify(tree, options);
            Assert.AreEqual(Dump(expected), Dump(actual), "first pass of " + Dump(tree));
            Assert.AreEqual(Dump(reference.Simplify(expected)), Dump(Simplify(actual, options).Result), "second pass of " + Dump(tree));
            if (spine is { } admitted)
            {
                Assert.AreEqual(admitted, statistics.Spine > 0 && IsSpineRoot(tree, options), "spine admission of the root of " + Dump(tree) + " under " + options);
            }
        }
    }

    /// <summary>Whether the admission of an instance of <paramref name="options"/> admits the root of <paramref name="tree"/> as a spine chain.</summary>
    /// <param name="tree">A tree whose root is an additive chain.</param>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when the root is admitted as a spine chain.</returns>
    private static bool IsSpineRoot(Expression tree, ExpressionSimplifierOptions options)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, tree), static state => state.simplifier.FlattenAdmission(state.tree));
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        return statistics.Spine == 1;
    }

    /// <summary>The families the spine admission accepts, each reaching the flatten rule and keeping the level by level result.</summary>
    [TestMethod]
    public void RepeatedFamilies_AreAdmitted_AndKeepTheLevelByLevelResult()
    {
        ParameterExpression x = D[0], y = D[1], z = D[2];
        Expression[] trees =
        [
            Expression.Add(LeftSum(D.Take(16)), x),                                              // repeated parameter at the root
            LeftSum(D.Take(16).Concat(D.Take(4))),                                                // several levels above the linear part
            LeftSum(D.Take(8).Concat(D.Take(8))),                                                 // every parameter twice
            D.Take(4).Aggregate(LeftSum(D.Take(12)), static (acc, p) => Expression.Subtract(acc, p)), // cancellations at the top
            Expression.Add(LeftSum(Enumerable.Range(0, 12).Select(static i => (Expression)Expression.Multiply(C(i + 2.0), D[i]))), Expression.Multiply(C(2.0), D[12])), // repeated coefficient
            Expression.Add(LeftSum([x, Expression.Multiply(C(3.0), y), C(3.0)]), Expression.Multiply(C(3.0), z)), // coefficient equal to a constant
            Expression.Add(x, RightSum(D.Take(10))),                                              // right comb
            Expression.Add(Expression.Add(Expression.Multiply(x, y), z), Expression.Multiply(y, x)), // the same product in the other order
            Expression.Add(Expression.Add(Expression.Multiply(C(2.0), x), y), x),                 // 2*x + y + x
            Expression.Subtract(Expression.Add(x, y), Expression.Multiply(C(-2.0), x)),           // effective signs agree: x - (-2)*x
            Expression.Add(Expression.Add(D[3], Expression.Add(x, y)), x),                        // mixed comb
        ];

        foreach (Expression tree in trees) AssertLevelByLevel(tree, spine: true);
    }

    /// <summary>The same families over the integral and single-precision chain types.</summary>
    [TestMethod]
    public void RepeatedFamilies_OfEveryFlattenedType_AreAdmitted()
    {
        foreach (Type type in new[] { typeof(int), typeof(long), typeof(uint), typeof(ulong), typeof(float) })
        {
            ParameterExpression[] p = [.. Enumerable.Range(0, 10).Select(i => Expression.Parameter(type, "p" + i))];
            AssertLevelByLevel(LeftSum(p.Concat(p.Take(3))), spine: true);
            AssertLevelByLevel(Expression.Add(LeftSum(p.Take(6)), Expression.Multiply(Expression.Constant(Convert.ChangeType(3, type), type), p[0])), spine: true);
        }
    }

    /// <summary>The chains on which a rule may apply keep the refusal, and their level by level treatment.</summary>
    [TestMethod]
    public void ChainsWhereARuleMayApply_AreStillRefused()
    {
        ParameterExpression x = D[0], y = D[1], z = D[2];
        Expression[] trees =
        [
            LeftSum([x, x, x]),                                                                    // the deepest pair factors
            Expression.Add(Expression.Subtract(Expression.Add(x, y), y), y),                       // x + y - y + y: one monomial left
            Expression.Add(Expression.Add(x, y), Expression.Add(z, x)),                            // two crossed operands
            Expression.Add(Expression.Add(Expression.Multiply(x, x), y), x),                       // a square
            Expression.Add(LeftSum([x, C(1.0), y, C(2.0)]), C(3.0)),                               // a third constant
            Expression.Subtract(Expression.Add(x, y), Expression.Add(z, x)),                       // a subtracted group
            Expression.Add(Expression.Add(Expression.Call(Sin, x), y), x),                         // a term that is not linear
            Expression.Add(Expression.Subtract(Expression.Add(Expression.Multiply(C(2.0), x), y), x), Expression.Subtract(z, z)), // not a comb
        ];

        foreach (Expression tree in trees) AssertLevelByLevel(tree, spine: false);
    }

    /// <summary>
    /// A monomial whose integral coefficients may wrap to zero is not certainly non-zero: the admission refuses the level above
    /// it, while the same chain with small coefficients is admitted.
    /// </summary>
    [TestMethod]
    public void WrappingCoefficients_AreNotCertainlyNonZero()
    {
        ParameterExpression a = Expression.Parameter(typeof(int), "a"), b = Expression.Parameter(typeof(int), "b"), c = Expression.Parameter(typeof(int), "c");
        Expression Big(ParameterExpression p) => Expression.Multiply(Expression.Constant(1 << 30), p);

        // b*2^30 three times: 3 * 2^30 >= 2^31, so b may vanish; with a and c, a and c stay certain - admitted.
        AssertLevelByLevel(LeftSum([a, Big(b), c, Big(b), Big(b), a]), spine: null);
        AssertLevelByLevel(LeftSum([a, Big(b), Big(b), Big(b), a]), spine: false);
    }

    /// <summary>Seeded random combs and trees of repeated terms, every type and policy: always the level by level result.</summary>
    [TestMethod]
    public void RandomChains_KeepTheLevelByLevelResult()
    {
        var random = new Random(31);
        int admitted = 0;
        for (int i = 0; i < 400; i++)
        {
            ParameterExpression[] p = [.. D.Take(2 + random.Next(4))];
            Expression Term()
            {
                ParameterExpression parameter = p[random.Next(p.Length)];
                return random.Next(6) switch
                {
                    0 => Expression.Multiply(C(new[] { 2.0, 3.0, -2.0, 0.5 }[random.Next(4)]), parameter),
                    1 => Expression.Multiply(parameter, p[random.Next(p.Length)]),
                    2 => C(new[] { 1.0, 2.0, -3.0 }[random.Next(3)]),
                    _ => parameter,
                };
            }

            Expression tree = Term();
            int n = 2 + random.Next(14);
            for (int k = 1; k < n; k++)
            {
                tree = random.Next(3) switch
                {
                    0 => Expression.Subtract(tree, Term()),
                    1 => Expression.Add(Term(), tree),
                    _ => Expression.Add(tree, Term()),
                };
            }

            AssertLevelByLevel(tree, spine: null);
            if (Simplify(tree).Statistics.Spine > 0) admitted++;
        }

        Assert.IsTrue(admitted > 40, "the corpus exercises the spine admission (" + admitted + ")");
    }

    /// <summary>
    /// An admitted chain is simplified without its rule dispatch: fewer transformation visits than level by level, and a long
    /// repeated chain is treated well within the default budget, with the counters of the levels finalized one by one.
    /// </summary>
    [TestMethod]
    public void AdmittedChains_CostLessThanLevelByLevel()
    {
        Expression tree = LeftSum(D.Take(48).Concat(D.Take(8)));
        (Expression result, FlattenAdmissionStatistics statistics) = Simplify(tree);
        Assert.AreEqual(1, statistics.Spine);
        Assert.AreEqual(8, statistics.SpineLevels, "the eight repeated terms are the levels above the linear part");
        Assert.AreEqual(Dump(new LevelByLevelSimplifier(ExpressionResourceLimits.Unlimited).Simplify(tree)), Dump(result));

        // 128 distinct terms and 8 repetitions: within the default budget (the level by level treatment needs far more).
        Assert.AreEqual(1, Simplify(LeftSum(D.Take(128).Concat(D.Take(8)))).Statistics.Spine);

        Expression small = Expression.Add(LeftSum(D.Take(16)), D[0]);
        long builtIn = MinimalTransformVisits(limits => new ExpressionSimplifier(limits), small);
        long levelByLevel = MinimalTransformVisits(limits => new LevelByLevelSimplifier(limits), small);
        Assert.IsTrue(builtIn < levelByLevel, $"built-in {builtIn} visits, level by level {levelByLevel}");
        Assert.IsTrue(builtIn <= 2 + 17 + 2, $"one visit per level and per term ({builtIn})");
    }

    /// <summary>The smallest transformation budget under which <paramref name="tree"/> is simplified.</summary>
    /// <param name="create">Creates the simplifier for given limits.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The number of transformation visits.</returns>
    private static long MinimalTransformVisits(Func<ExpressionResourceLimits, ExpressionSimplifier> create, Expression tree)
    {
        bool Fits(long visits)
        {
            try
            {
                create(ExpressionResourceLimits.Default with { MaxTransformVisits = visits }).Simplify(tree);
                return true;
            }
            catch (ExpressionResourceLimitException)
            {
                return false;
            }
        }

        long low = 0, high = 1;
        while (!Fits(high)) (low, high) = (high, high * 2);
        while (high - low > 1)
        {
            long middle = (low + high) / 2;
            if (Fits(middle)) high = middle;
            else low = middle;
        }

        return high;
    }

    /// <summary>
    /// A budget refusal in the middle of the treatment leaves the instance reusable, and the next chain it simplifies is
    /// treated according to its own admission (no stale spine decision).
    /// </summary>
    [TestMethod]
    public void BudgetRefusal_LeavesNoStaleDecision()
    {
        Expression repeated = LeftSum(D.Take(40).Concat(D.Take(10)));
        var limited = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = 30 });
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(repeated));

        Expression linear = LeftSum(D.Skip(60).Take(10));
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(linear)), Dump(limited.Simplify(linear)));
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(repeated)), Dump(new ExpressionSimplifier().Simplify(repeated)));
    }

    /// <summary>
    /// A refused spine root records its spine nodes down to the cause: each sub-chain offered later is refused by one lookup
    /// instead of another spine walk, so the spine test of <c>x + x + ... + x</c> walks the chain once per operation, and the
    /// result stays the level by level one.
    /// </summary>
    [TestMethod]
    public void RefusedSpineRoots_AreWalkedOncePerOperation()
    {
        Expression tree = LeftSum(Enumerable.Repeat<Expression>(D[0], 16));
        (Expression result, FlattenAdmissionStatistics statistics) = Simplify(tree);
        Assert.AreEqual(1, statistics.SpineWalks);
        Assert.IsTrue(statistics.KnownSpineRefusals >= 13, "the sub-chains above the deepest pair (" + statistics.KnownSpineRefusals + ")");
        Assert.AreEqual(1, statistics.SpineCauses[(int)FlattenSpineRefusalCause.DeepestPair]);
        Assert.AreEqual(0, statistics.Spine);
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(tree)), Dump(result));
    }

    /// <summary>
    /// The admitted spine root belongs to its operation (PR #701 review): an exception between the admission and the flatten
    /// rule (a budget refusal of the extraction or of the root's visit, or any other exception) leaves no root behind once the
    /// operation is over; a nested public operation neither sees nor consumes its caller's root, and gives it back on exit;
    /// outside an operation the admission does not admit a spine chain at all.
    /// </summary>
    [TestMethod]
    public void AdmittedSpineRoot_BelongsToItsOperation()
    {
        Expression tree = Expression.Add(LeftSum(D.Take(16)), D[0]);
        Expression other = Expression.Add(LeftSum(D.Skip(20).Take(8)), D[20]);
        var simplifier = new ExpressionSimplifier();

        // Outside an operation: refused (nothing could carry the decision to the rule).
        Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, simplifier.FlattenAdmission(tree));

        // Any exception after the admission: the next operation starts without a root.
        Assert.ThrowsExactly<InvalidOperationException>(() => ExpressionResourceBudget.Run<(ExpressionSimplifier simplifier, Expression tree), int>(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, tree), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.None, state.simplifier.FlattenAdmission(state.tree));
            Assert.AreSame(state.tree, ExpressionResourceBudget.ActiveSession!.AdmittedSpineRoot);
            throw new InvalidOperationException("after the admission");
        }));
        Assert.IsNull(LeftOverRoot(simplifier));

        // Budget refusals between the admission and the rule (the root's visit is consumed after the extraction): every small
        // budget that refuses the chain leaves no root behind.
        for (long visits = 1; visits < 5; visits++)
        {
            var limited = new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxTransformVisits = visits });
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(tree));
            Assert.IsNull(LeftOverRoot(limited), "after a refusal with " + visits + " visits");
        }

        // A nested public operation runs with its own root and gives the caller's back.
        ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, tree, other), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.None, state.simplifier.FlattenAdmission(state.tree));
            Expression? seen = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, state.simplifier, null, null, state.other, static inner =>
                ExpressionResourceBudget.ActiveSession!.AdmittedSpineRoot);
            Assert.IsNull(seen, "a nested operation does not see its caller's root");
            Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(state.other)), Dump(new ExpressionSimplifier().Simplify(state.other)), "a nested simplification uses its own root");
            Assert.AreSame(state.tree, ExpressionResourceBudget.ActiveSession!.AdmittedSpineRoot, "the caller's root is given back");
            return 0;
        });
        Assert.IsNull(LeftOverRoot(simplifier));
        Assert.AreEqual(Dump(new LevelByLevelSimplifier().Simplify(tree)), Dump(simplifier.Simplify(tree)));
    }

    /// <summary>The spine root a new operation of <paramref name="simplifier"/> finds in its session when it starts.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <returns>The root left in the session, or <see langword="null"/>.</returns>
    private static Expression? LeftOverRoot(ExpressionSimplifier simplifier)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, 0, static _ => ExpressionResourceBudget.ActiveSession!.AdmittedSpineRoot);

    /// <summary>A derived simplifier keeps the effect gate: it never admits a spine chain.</summary>
    [TestMethod]
    public void DerivedSimplifiers_DoNotAdmitSpineChains()
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            new LevelByLevelSimplifier().Simplify(Expression.Add(LeftSum(D.Take(10)), D[0]));
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        Assert.AreEqual(0, statistics.Spine);
        Assert.AreEqual(0, statistics.SpineWalks);
    }

    /// <summary>
    /// A chain bound by a lambda (keys depend on the lambda's scopes) and a chain sharing a sub-chain keep the level by level
    /// result.
    /// </summary>
    [TestMethod]
    public void LambdasAndSharedSubChains_KeepTheLevelByLevelResult()
    {
        ParameterExpression u = Expression.Parameter(typeof(double), "u");
        ParameterExpression v = Expression.Parameter(typeof(double), "v");
        Expression body = LeftSum([u, v, D[0], u, v, D[0]]);
        AssertLevelByLevel(Expression.Lambda<Func<double, double, double>>(body, u, v), spine: null);

        Expression shared = LeftSum(D.Take(6));
        AssertLevelByLevel(Expression.Add(Expression.Add(shared, D[0]), Expression.Multiply(shared, D[7])), spine: null);
        AssertLevelByLevel(Expression.Add(Expression.Negate(Expression.Add(LeftSum(D.Take(6)), D[1])), D[2]), spine: null);
    }
}
