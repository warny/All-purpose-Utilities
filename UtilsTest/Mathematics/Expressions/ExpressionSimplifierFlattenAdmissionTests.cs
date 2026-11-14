using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The admission of the flatten rule of <see cref="ExpressionSimplifier"/> with its table of known refusals (Flatten mission
/// C1, <c>docs/expressions/SimplifierFlatten.md</c> §9): every decision is exactly the one of the linear-chain definition of
/// PR B, whatever the order in which the nodes are offered, only proven refusals are recorded, and a refused chain whose cause
/// lies deep is no longer walked again from every level.
/// </summary>
/// <remarks>
/// The reference of every decision is <see cref="ReferenceIsLinear"/>, a direct and recursive transcription of the definition
/// (§3 of the document), independent of the iterative walk and of its table.
/// </remarks>
[TestClass]
public class ExpressionSimplifierFlattenAdmissionTests
{
    /// <summary>A derived simplifier without any member: it keeps the level by level treatment.</summary>
    private sealed class LevelByLevelSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Free <see cref="double"/> parameters.</summary>
    private static readonly ParameterExpression[] D = [.. Enumerable.Range(0, 128).Select(static i => Expression.Parameter(typeof(double), "d" + i))];

    /// <summary>Free <see cref="int"/> parameters.</summary>
    private static readonly ParameterExpression[] I = [.. Enumerable.Range(0, 8).Select(static i => Expression.Parameter(typeof(int), "i" + i))];

    /// <summary><see cref="Math.Sin(double)"/>: a total function, inert for the effect analysis.</summary>
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>A function the effect analysis does not know: its calls are observable.</summary>
    /// <returns>Always one.</returns>
    public static double Unknown() => 1;

    /// <summary>Builds a call of <see cref="Unknown"/>.</summary>
    /// <returns>The call.</returns>
    private static Expression Call() => Expression.Call(typeof(ExpressionSimplifierFlattenAdmissionTests).GetMethod(nameof(Unknown))!);

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value, typeof(double));

    /// <summary>Builds a left-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(static (left, right) => Expression.Add(left, right));

    /// <summary>Builds a right-associated sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression RightSum(IEnumerable<Expression> terms) => terms.Reverse().Aggregate(static (right, left) => Expression.Add(left, right));

    /// <summary>Builds a balanced sum of <paramref name="terms"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The sum.</returns>
    private static Expression BalancedSum(IReadOnlyList<Expression> terms)
        => terms.Count == 1 ? terms[0] : Expression.Add(BalancedSum([.. terms.Take(terms.Count / 2)]), BalancedSum([.. terms.Skip(terms.Count / 2)]));

    /// <summary>The parameters <c>d0</c> to <c>d(n-1)</c>, with the term at <paramref name="at"/> replaced by <paramref name="term"/>.</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="at">The position of the replaced term.</param>
    /// <param name="term">The replacing term.</param>
    /// <returns>The terms.</returns>
    private static Expression[] Terms(int n, int at, Expression term)
    {
        Expression[] terms = [.. D.Take(n)];
        terms[at] = term;
        return terms;
    }

    #region Reference definition

    /// <summary>Whether a chain crosses <paramref name="node"/>: an ordinary <c>Add</c> or <c>Subtract</c> the default <c>CanFlattenThrough</c> accepts.</summary>
    /// <param name="node">A node.</param>
    /// <returns><see langword="true"/> when the node is crossed.</returns>
    private static bool IsCrossed(Expression node)
        => node is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract, Method: null } binary
            && (binary.Type == typeof(double) || binary.Type == typeof(int) || binary.Type == typeof(long) || binary.Type == typeof(float)
                || binary.Type == typeof(uint) || binary.Type == typeof(ulong) || binary.Type == typeof(short) || binary.Type == typeof(ushort));

    /// <summary>
    /// The linear-chain definition of PR B (§3), transcribed recursively: the type, ordinary crossed nodes, no subtracted
    /// group, no pair of constants, linear terms only, no repeated parameter or coefficient, no coefficient equal to a
    /// constant term, at most two constant terms.
    /// </summary>
    /// <param name="root">A crossed root.</param>
    /// <returns><see langword="true"/> for a linear chain.</returns>
    private static bool ReferenceIsLinear(BinaryExpression root)
    {
        Type type = root.Type;
        if (type != typeof(double) && type != typeof(int) && type != typeof(long) && type != typeof(float) && type != typeof(uint) && type != typeof(ulong)) return false;

        var nodes = new List<BinaryExpression>();
        var terms = new List<Expression>();
        Collect(root, nodes, terms);
        foreach (BinaryExpression node in nodes)
        {
            if (node.NodeType == ExpressionType.Subtract && IsCrossed(node.Right)) return false;
            if (node.Left is ConstantExpression && node.Right is ConstantExpression) return false;
        }

        if (!terms.All(term => IsReferenceLinearTerm(term, type))) return false;
        if (!nodes.All(ExpressionSimplifier.IsOrdinaryBinaryArithmetic)) return false;

        var parameters = new List<ParameterExpression>();
        var coefficients = new List<object>();
        var constants = new List<object>();
        foreach (Expression term in terms)
        {
            switch (term)
            {
                case ParameterExpression parameter:
                    parameters.Add(parameter);
                    break;
                case ConstantExpression constant:
                    constants.Add(constant.Value!);
                    break;
                default:
                    foreach (Expression factor in new[] { ((BinaryExpression)term).Left, ((BinaryExpression)term).Right })
                    {
                        if (factor is ParameterExpression parameter) parameters.Add(parameter);
                        else coefficients.Add(((ConstantExpression)factor).Value!);
                    }

                    break;
            }
        }

        return parameters.Distinct().Count() == parameters.Count
            && coefficients.Distinct().Count() == coefficients.Count
            && !coefficients.Intersect(constants).Any()
            && constants.Count <= 2;
    }

    /// <summary>Collects the crossed nodes and the terms of the chain rooted at <paramref name="node"/>.</summary>
    /// <param name="node">A crossed node.</param>
    /// <param name="nodes">Receives the crossed nodes.</param>
    /// <param name="terms">Receives the terms.</param>
    private static void Collect(BinaryExpression node, List<BinaryExpression> nodes, List<Expression> terms)
    {
        nodes.Add(node);
        foreach (Expression operand in new[] { node.Left, node.Right })
        {
            if (IsCrossed(operand)) Collect((BinaryExpression)operand, nodes, terms);
            else terms.Add(operand);
        }
    }

    /// <summary>Whether <paramref name="term"/> is a linear term of a chain of <paramref name="type"/> (§3, condition 3).</summary>
    /// <param name="term">A term.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a linear term.</returns>
    private static bool IsReferenceLinearTerm(Expression term, Type type) => term switch
    {
        ParameterExpression parameter => parameter.Type == type,
        ConstantExpression constant => IsReferenceConstant(constant, type),
        BinaryExpression { NodeType: ExpressionType.Multiply } product when product.Type == type && ExpressionSimplifier.IsOrdinaryBinaryArithmetic(product)
            => (product.Left, product.Right) switch
            {
                (ParameterExpression, ParameterExpression) => true,
                (ParameterExpression, ConstantExpression coefficient) => IsReferenceCoefficient(coefficient, type),
                (ConstantExpression coefficient, ParameterExpression) => IsReferenceCoefficient(coefficient, type),
                _ => false,
            },
        _ => false,
    };

    /// <summary>Whether <paramref name="constant"/> is a finite non-zero primitive of exactly <paramref name="type"/>.</summary>
    /// <param name="constant">A constant.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for such a constant.</returns>
    private static bool IsReferenceConstant(ConstantExpression constant, Type type)
        => constant.Type == type && constant.Value is { } value && value.GetType() == type && value switch
        {
            double d => double.IsFinite(d) && d != 0,
            float f => float.IsFinite(f) && f != 0,
            _ => Convert.ToDecimal(value) != 0,
        };

    /// <summary>Whether <paramref name="constant"/> is a linear constant other than <c>1</c> and <c>-1</c>.</summary>
    /// <param name="constant">A constant factor.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for an accepted coefficient.</returns>
    private static bool IsReferenceCoefficient(ConstantExpression constant, Type type)
        => IsReferenceConstant(constant, type) && Convert.ToDouble(constant.Value) is not (1d or -1d);

    #endregion

    /// <summary>Every <c>Add</c>/<c>Subtract</c> node of a tree, in pre-order (the order in which the traversal offers them), each shared node once.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The nodes.</returns>
    private static List<BinaryExpression> AdditiveNodes(Expression tree)
    {
        var result = new List<BinaryExpression>();
        var seen = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Expression>();
        stack.Push(tree);
        while (stack.Count > 0)
        {
            Expression node = stack.Pop();
            if (!seen.Add(node)) continue;
            switch (node)
            {
                case BinaryExpression binary:
                    if (binary.NodeType is ExpressionType.Add or ExpressionType.Subtract) result.Add(binary);
                    stack.Push(binary.Right);
                    stack.Push(binary.Left);
                    break;
                case UnaryExpression unary:
                    stack.Push(unary.Operand);
                    break;
                case MethodCallExpression call:
                    for (int i = call.Arguments.Count - 1; i >= 0; i--) stack.Push(call.Arguments[i]);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Offers every additive node of <paramref name="tree"/> to the admission inside one operation, in the given order, and
    /// checks each decision and every recorded node against the reference.
    /// </summary>
    /// <param name="label">The case.</param>
    /// <param name="tree">The tree.</param>
    /// <param name="order">The nodes, in the order to offer them.</param>
    /// <returns>The number of nodes recorded as known refusals.</returns>
    private static int CheckWithinOneOperation(string label, Expression tree, IReadOnlyList<BinaryExpression> order)
    {
        var simplifier = new ExpressionSimplifier();
        return ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, label, tree, order), static state =>
        {
            foreach (BinaryExpression node in state.order)
            {
                bool expected = ReferenceIsLinear(node);
                Assert.AreEqual(expected ? ExpressionRuleRejection.None : ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(node), $"{state.label}: {node}");
            }

            ExpressionNodeSet? table = ExpressionResourceBudget.ActiveSession!.FlattenRefusals;
            int recorded = 0;
            foreach (BinaryExpression node in AdditiveNodes(state.tree))
            {
                if (table is not null && table.Contains(node))
                {
                    recorded++;
                    Assert.IsFalse(ReferenceIsLinear(node), $"{state.label}: a recorded node must be refused: {node}");
                }
            }

            Assert.AreEqual(table?.Count ?? 0, recorded, state.label + ": every recorded node belongs to the tree");
            return recorded;
        });
    }

    /// <summary>The trees of the targeted families of the brief, in every grouping, plus seeded random chains and shared DAGs.</summary>
    /// <returns>The labelled trees.</returns>
    private static IEnumerable<(string Label, Expression Tree)> Corpus()
    {
        foreach (int n in new[] { 2, 3, 8, 33 })
        {
            foreach ((string position, int at) in new[] { ("first", 0), ("middle", n / 2), ("last", n - 1) })
            {
                foreach ((string kind, Expression term) in new[] { ("call", Call()), ("sin", Expression.Call(Sin, D[at])), ("negate", (Expression)Expression.Negate(D[at])), ("zero", C(0)) })
                {
                    Expression[] terms = Terms(n, at, term);
                    yield return ($"left {kind} {position} {n}", LeftSum(terms));
                    yield return ($"right {kind} {position} {n}", RightSum(terms));
                    yield return ($"balanced {kind} {position} {n}", BalancedSum(terms));
                }

                if (n > 2 && at > 0)
                {
                    Expression[] repeated = Terms(n, at, D[0]);
                    yield return ($"left repeat {position} {n}", LeftSum(repeated));
                    yield return ($"right repeat {position} {n}", RightSum(repeated));
                    yield return ($"balanced repeat {position} {n}", BalancedSum(repeated));
                }
            }
        }

        // 2*x0 + 3*x1 + ... + 2*x(n-1): a coefficient repeated at the end; x0 + ... + x98 + x0; the coefficient repeated deep (S5 p).
        Expression coefficients = Expression.Multiply(C(2), D[0]);
        for (int i = 1; i < 98; i++) coefficients = Expression.Add(coefficients, Expression.Multiply(C(i + 2), D[i]));
        yield return ("2*x0 + ... + 2*x98", Expression.Add(coefficients, Expression.Multiply(C(2), D[98])));
        yield return ("x0 + ... + x98 + x0", Expression.Add(LeftSum(D.Take(99)), D[0]));
        Expression p = Expression.Multiply(C(100), D[0]);
        for (int i = 1; i < 101; i++) p = Expression.Subtract(p, i % 2 == 1 ? Expression.Multiply(D[i], C(i + 2)) : Expression.Multiply(C(i + 2), D[i]));
        yield return ("p 101", p);

        // A coefficient equal to a constant deep in the chain, and three constants.
        yield return ("coefficient = constant", LeftSum([C(7), .. D.Skip(1).Take(20), Expression.Multiply(C(7), D[30]), D[31]]));
        yield return ("third constant", LeftSum([D[0], C(1), D[1], C(2), D[2], D[3], C(3), D[4]]));
        yield return ("a - (b + c)", Expression.Subtract(D[0], Expression.Add(D[1], D[2])));
        yield return ("(a + 1) + (2 + b)", Expression.Add(Expression.Add(D[0], C(1)), Expression.Add(C(2), D[1])));
        yield return ("deep subtracted group", LeftSum([.. D.Take(10), Expression.Subtract(D[10], Expression.Add(D[11], D[12])), .. D.Skip(13).Take(10)]));
        yield return ("int: deep pair of constants", LeftSum([.. I.Take(4), Expression.Add(Expression.Constant(1), Expression.Constant(2)), .. I.Skip(4)]));

        // Shared nodes: a refused sub-chain and an accepted sub-chain, each reused at two places.
        Expression refusedShared = LeftSum([Call(), D[1], D[2]]);
        Expression acceptedShared = LeftSum([D[3], D[4], D[5]]);
        yield return ("dag", Expression.Add(Expression.Add(LeftSum([refusedShared, D[6], D[7]]), Expression.Multiply(acceptedShared, refusedShared)), acceptedShared));
        yield return ("dag repeated", LeftSum([acceptedShared, D[8], acceptedShared, D[9]]));

        // Seeded random chains: random groupings and operators over linear and non-linear terms.
        var random = new Random(20261114);
        Expression[] pool =
        [
            .. D.Take(6), C(2), C(3), C(5), C(0), C(double.NaN),
            Expression.Multiply(C(2), D[6]), Expression.Multiply(D[7], C(3)), Expression.Multiply(D[8], D[9]), Expression.Multiply(C(1), D[10]),
            Expression.Call(Sin, D[11]), Expression.Negate(D[12]), Call(), D[0], D[1],
        ];
        for (int c = 0; c < 400; c++)
        {
            int count = random.Next(2, 24);
            var terms = new List<Expression>();
            for (int i = 0; i < count; i++) terms.Add(pool[random.Next(pool.Length)]);
            yield return ($"random {c}", RandomTree(random, terms, 0, terms.Count));
        }
    }

    /// <summary>Builds a random grouping of <paramref name="terms"/> with random additions and subtractions.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="terms">The terms.</param>
    /// <param name="start">The first term.</param>
    /// <param name="count">The number of terms.</param>
    /// <returns>The tree.</returns>
    private static Expression RandomTree(Random random, List<Expression> terms, int start, int count)
    {
        if (count == 1) return terms[start];
        int split = random.Next(4) switch { 0 => 1, 1 => count - 1, _ => random.Next(1, count) };
        Expression left = RandomTree(random, terms, start, split);
        Expression right = RandomTree(random, terms, start + split, count - split);
        return random.Next(3) == 0 ? Expression.Subtract(left, right) : Expression.Add(left, right);
    }

    /// <summary>
    /// Every admission decision is the one of the definition, whether the nodes of a tree are offered top-down inside one
    /// operation (the order of the traversal, which fills the table of known refusals), bottom-up, or each in an operation of
    /// its own; and every node recorded in the table is a refused chain of the tree.
    /// </summary>
    [TestMethod]
    public void AdmissionDecisions_AreTheReferenceDecisions_InEveryOrder()
    {
        int cases = 0, recorded = 0;
        foreach ((string label, Expression tree) in Corpus())
        {
            List<BinaryExpression> nodes = AdditiveNodes(tree);
            if (nodes.Count == 0) continue;
            recorded += CheckWithinOneOperation(label + " (top-down)", tree, nodes);
            CheckWithinOneOperation(label + " (bottom-up)", tree, [.. Enumerable.Reverse(nodes)]);

            var simplifier = new ExpressionSimplifier();
            foreach (BinaryExpression node in nodes)
            {
                Assert.AreEqual(ReferenceIsLinear(node) ? ExpressionRuleRejection.None : ExpressionRuleRejection.ChainNotAdmitted, simplifier.FlattenAdmission(node), $"{label} (alone): {node}");
            }

            cases++;
        }

        Assert.IsTrue(cases > 500, $"{cases} trees");
        Assert.IsTrue(recorded > 1_000, $"{recorded} recorded refusals: the corpus must exercise the table");
    }

    /// <summary>
    /// The targeted families of the brief simplify exactly as the level by level treatment does (first and second pass): the
    /// table only answers admissions faster.
    /// </summary>
    [TestMethod]
    public void TargetedFamilies_GiveTheLevelByLevelResult()
    {
        // The level by level reference grows too fast for the chains of about a hundred terms (they are compared with master by
        // the differential campaign instead).
        foreach ((string label, Expression tree) in Corpus().Where(static c => !c.Label.StartsWith("random", StringComparison.Ordinal) && AdditiveNodes(c.Tree).Count <= 40))
        {
            Expression expected = new LevelByLevelSimplifier().Simplify(tree);
            Expression actual = new ExpressionSimplifier().Simplify(tree);
            Assert.AreEqual(expected.ToString(), actual.ToString(), label);
            Assert.AreEqual(new LevelByLevelSimplifier().Simplify(expected).ToString(), new ExpressionSimplifier().Simplify(actual).ToString(), label + " (second pass)");
        }
    }

    /// <summary>Runs one simplification with the admission counters installed.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The counters.</returns>
    private static FlattenAdmissionStatistics Measure(Expression tree)
    {
        var statistics = new FlattenAdmissionStatistics { DistinctNodes = new HashSet<Expression>(ReferenceEqualityComparer.Instance) };
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(tree);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        return statistics;
    }

    /// <summary>
    /// A chain refused by its deepest term is walked once, from its root: every level below is answered by the table (before
    /// mission C1, each level walked down to the term again, n²/2 nodes). In the left and the right groupings, with the term
    /// in the middle (the linear chains below it are walked on their own), and through a whole simplification whose
    /// comparisons offer chains of their own (<c>sin(x0) + x1 + ... + x31</c>): no node is examined twice.
    /// </summary>
    [TestMethod]
    public void DeepCause_IsWalkedOnce()
    {
        const int N = 100;

        // Left: -d0 + d1 + ... + d99; right: d0 + (... + (d98 + -d99)). Every node contains the negation.
        foreach ((string label, Expression tree) in new[] { ("left", LeftSum(Terms(N, 0, Expression.Negate(D[0])))), ("right", RightSum(Terms(N, N - 1, Expression.Negate(D[N - 1])))) })
        {
            FlattenAdmissionStatistics statistics = MeasureAdmissions(tree);
            Assert.AreEqual(N - 1, statistics.Admissions, label);
            Assert.AreEqual(1, statistics.Walks, label);
            Assert.AreEqual(N - 1, statistics.NodesExamined, label);
            Assert.AreEqual(N - 2, statistics.KnownRefusals, label);
            Assert.AreEqual(N - 2, statistics.RecordedRefusals, label);
            Assert.AreEqual(1, statistics.Causes[(int)FlattenRefusalCause.NonLinearTerm], label);
        }

        // Middle: d0 + ... + d49 + -d50 + d51 + ... + d99, left-associated: the 49 levels above the negation are recorded; the
        // 49 linear chains below it are walked and admitted.
        FlattenAdmissionStatistics middle = MeasureAdmissions(LeftSum(Terms(N, 50, Expression.Negate(D[50]))));
        Assert.AreEqual(N - 50 - 1, middle.KnownRefusals);
        Assert.AreEqual(N - 50 - 1, middle.RecordedRefusals);
        Assert.AreEqual(49, middle.Accepted);
        Assert.AreEqual(1 + 49, middle.Walks);

        FlattenAdmissionStatistics simplification = Measure(LeftSum(Terms(32, 0, Expression.Call(Sin, D[0]))));
        Assert.IsTrue(simplification.KnownRefusals > 0);
        Assert.AreEqual(simplification.NodesExamined, simplification.DistinctNodes!.Count, "No node is examined twice.");
    }

    /// <summary>
    /// Offers every additive node of <paramref name="tree"/> to the admission, top-down, inside one operation with the
    /// counters installed: the admissions of the source alone, without those of the comparisons a simplification makes.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The counters.</returns>
    private static FlattenAdmissionStatistics MeasureAdmissions(Expression tree)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            var simplifier = new ExpressionSimplifier();
            ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, tree), static state =>
            {
                foreach (BinaryExpression node in AdditiveNodes(state.tree)) state.simplifier.FlattenAdmission(node);
                return 0;
            });
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        return statistics;
    }

    /// <summary>
    /// A repetition deep in a chain is found once: the levels that hold both occurrences are recorded down to the earlier
    /// one, and the next level, which no longer holds it, is walked and admitted. A repetition next to the root records
    /// nothing (the chain below it is admitted by its own walk, as before).
    /// </summary>
    [TestMethod]
    public void DeepRepetition_RecordsTheLevelsThatHoldBothOccurrences()
    {
        // d0 + d1 + d2 + d0 + d4 + ... + d39, left-associated: the root walk meets d0 first at term 3 (spine index 36).
        Expression[] terms = Terms(40, 3, D[0]);
        FlattenAdmissionStatistics statistics = MeasureAdmissions(LeftSum(terms));
        Assert.AreEqual(1, statistics.Causes[(int)FlattenRefusalCause.RepeatedParameter]);
        Assert.AreEqual(36, statistics.RecordedRefusals);
        Assert.AreEqual(36, statistics.KnownRefusals);
        Assert.AreEqual(3, statistics.Walks, "The root, then the two admitted chains below the earlier occurrence.");
        Assert.AreEqual(39, statistics.Admissions);

        // The repetition of the last term: nothing to record (repeatLast).
        FlattenAdmissionStatistics last = MeasureAdmissions(Expression.Add(LeftSum(D.Take(39)), D[0]));
        Assert.AreEqual(0, last.RecordedRefusals);
        Assert.AreEqual(0, last.KnownRefusals);

        // A coefficient repeated deep, and a coefficient equal to a constant deep: same recording.
        FlattenAdmissionStatistics coefficient = MeasureAdmissions(LeftSum([.. D.Take(3), Expression.Multiply(C(2), D[3]), .. D.Skip(4).Take(30), Expression.Multiply(C(2), D[40])]));
        Assert.AreEqual(1, coefficient.Causes[(int)FlattenRefusalCause.RepeatedCoefficient]);
        Assert.AreEqual(0, coefficient.RecordedRefusals, "The repeated coefficient is the right operand of the root.");
        FlattenAdmissionStatistics deep = MeasureAdmissions(LeftSum([Expression.Multiply(C(2), D[0]), .. D.Skip(1).Take(2), Expression.Multiply(C(2), D[3]), .. D.Skip(4).Take(30)]));
        Assert.AreEqual(1, deep.Causes[(int)FlattenRefusalCause.RepeatedCoefficient]);
        Assert.AreEqual(30, deep.RecordedRefusals);
        FlattenAdmissionStatistics constant = MeasureAdmissions(LeftSum([C(2), .. D.Skip(1).Take(2), Expression.Multiply(C(2), D[3]), .. D.Skip(4).Take(30)]));
        Assert.AreEqual(1, constant.Causes[(int)FlattenRefusalCause.CoefficientEqualsConstant]);
        Assert.AreEqual(30, constant.RecordedRefusals);
    }

    /// <summary>
    /// The table belongs to the outermost operation: a nested operation sees it, the next operation starts without it, and
    /// a node recorded by one simplification is walked again by the next one.
    /// </summary>
    [TestMethod]
    public void KnownRefusals_LastOneOperation()
    {
        Expression tree = LeftSum(Terms(20, 0, Expression.Negate(D[0])));
        var simplifier = new ExpressionSimplifier();
        ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, tree), static state =>
        {
            Assert.IsNull(ExpressionResourceBudget.ActiveSession!.FlattenRefusals);
            state.simplifier.Simplify(state.tree);
            Assert.IsTrue(ExpressionResourceBudget.ActiveSession!.FlattenRefusals!.Count >= 18, "A nested operation fills the table of the outer one.");
            return 0;
        });

        ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, 0, static _ =>
        {
            Assert.IsNull(ExpressionResourceBudget.ActiveSession!.FlattenRefusals, "The outermost exit drops the table.");
            return 0;
        });

        FlattenAdmissionStatistics first = Measure(tree);
        FlattenAdmissionStatistics second = Measure(tree);
        Assert.AreEqual(first.Walks, second.Walks);
        Assert.AreEqual(first.KnownRefusals, second.KnownRefusals);
    }

    /// <summary>The counters are opt-in: none is installed by default, and the admission records nothing then.</summary>
    [TestMethod]
    public void AdmissionStatistics_AreOptIn()
    {
        Assert.IsNull(ExpressionSimplifier.AdmissionStatistics);
        var statistics = new FlattenAdmissionStatistics();
        new ExpressionSimplifier().Simplify(LeftSum(Terms(10, 0, Expression.Negate(D[0]))));
        Assert.AreEqual(0, statistics.Admissions);
        Assert.AreEqual(9, MeasureAdmissions(LeftSum(Terms(10, 0, Expression.Negate(D[0])))).Admissions);
        Assert.IsNull(ExpressionSimplifier.AdmissionStatistics);
    }
}
