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
/// The enriched description of simplified terms (mission C3a, <c>docs/expressions/SimplifierFlatten.md</c> §14): the raw
/// structural hash, the canonical-order key and the canonicalization state that the canonicalizations compute are read back
/// from the operation's metadata table instead of being computed again, without changing any result.
/// </summary>
/// <remarks>
/// Results are compared through <see cref="Dump"/> (exact shape) against the result of an operation whose table holds no
/// earlier entry. The comparison with the results of <c>master</c> is the differential campaign of the mission (S5 audit
/// harness), not these tests.
/// </remarks>
[TestClass]
public class ExpressionSimplifierMetadataTests
{
    /// <summary>A derived simplifier without any member.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Free <see cref="double"/> parameters.</summary>
    private static readonly ParameterExpression[] D = [.. Enumerable.Range(0, 40).Select(static i => Expression.Parameter(typeof(double), "d" + i))];

    /// <summary><see cref="Math.Sin(double)"/>.</summary>
    private static readonly MethodInfo Sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary><see cref="Math.Cos(double)"/>.</summary>
    private static readonly MethodInfo Cos = typeof(Math).GetMethod(nameof(Math.Cos), [typeof(double)])!;

    /// <summary>Builds a <see cref="double"/> constant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(double value) => Expression.Constant(value, typeof(double));

    /// <summary>The sum of <paramref name="terms"/>, left-associated.</summary>
    /// <param name="terms">The terms.</param>
    /// <returns>The chain.</returns>
    private static Expression LeftSum(IEnumerable<Expression> terms) => terms.Aggregate(Expression.Add);

    /// <summary>Runs <paramref name="action"/> with metadata counters installed and returns them.</summary>
    /// <param name="action">The measured action.</param>
    /// <returns>The counters.</returns>
    private static SimplificationMetadataStatistics Measure(Action action)
    {
        var statistics = new SimplificationMetadataStatistics();
        ExpressionSimplifier.MetadataStatistics = statistics;
        try
        {
            action();
        }
        finally
        {
            ExpressionSimplifier.MetadataStatistics = null;
        }

        return statistics;
    }

    /// <summary>A stable textual dump of a tree: node types, types, methods, constant values and parameter names.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The dump.</returns>
    private static string Dump(Expression? expression) => expression switch
    {
        null => "null",
        ParameterExpression parameter => parameter.Name + ":" + parameter.Type.Name,
        ConstantExpression constant => "C(" + constant.Type.Name + ":" + Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture) + ")",
        BinaryExpression binary => binary.NodeType + "(" + Dump(binary.Left) + "," + Dump(binary.Right) + ")",
        UnaryExpression unary => unary.NodeType + "(" + Dump(unary.Operand) + ")",
        MethodCallExpression call => call.Method.Name + "(" + string.Join(",", call.Arguments.Select(Dump)) + ")",
        LambdaExpression lambda => "L(" + string.Join(",", lambda.Parameters.Select(Dump)) + "=>" + Dump(lambda.Body) + ")",
        _ => expression.NodeType.ToString(),
    };

    /// <summary>The limits of the operations of <see cref="InOperation"/>: wide, so that every nested call keeps its own default budget.</summary>
    private static readonly ExpressionResourceLimits SharedLimits = new() { MaxTransformVisits = 1_000_000_000, MaxStructuralVisits = 10_000_000_000 };

    /// <summary>Runs <paramref name="function"/> inside one expression operation of <paramref name="simplifier"/>.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="simplifier">The owner of the operation.</param>
    /// <param name="function">The function.</param>
    /// <returns>Its result.</returns>
    private static T InOperation<T>(ExpressionSimplifier simplifier, Func<T> function)
        => ExpressionResourceBudget.Run(SharedLimits, simplifier, null, null, function, static f => f());

    /// <summary>
    /// The trees of the reference corpus: level by level chains (the reuse case), admitted flatten chains, function-like
    /// terms, products, lambdas binding the chain's parameters, a shared sub-chain and the C3 target families.
    /// </summary>
    /// <returns>The corpus.</returns>
    private static IEnumerable<Expression> Corpus()
    {
        ParameterExpression x = D[0], a = D[1], b = D[2], y = D[3];
        yield return Expression.Add(x, x);
        yield return LeftSum([x, x, x]);
        yield return LeftSum([Expression.Multiply(C(2), x), Expression.Multiply(C(3), x), Expression.Multiply(C(4), x)]);
        yield return Expression.Add(Expression.Multiply(a, x), Expression.Multiply(C(3), x));
        yield return LeftSum([Expression.Multiply(a, x), Expression.Multiply(C(3), x), x]);
        yield return Expression.Add(Expression.Power(x, C(2)), Expression.Multiply(C(2), Expression.Power(x, C(2))));
        yield return Expression.Add(Expression.Multiply(C(3), Expression.Call(Cos, x)), Expression.Multiply(C(5), Expression.Call(Cos, x)));
        yield return Expression.Add(Expression.Multiply(C(3), Expression.Call(Cos, x)), Expression.Multiply(C(5), Expression.Call(Sin, x)));
        yield return Expression.Add(Expression.Subtract(Expression.Multiply(a, x), Expression.Multiply(b, x)), Expression.Multiply(C(4), x));
        yield return LeftSum([Expression.Multiply(C(2), x), Expression.Multiply(C(3), y), Expression.Multiply(C(4), x)]);
        yield return LeftSum(
        [
            Expression.Power(x, C(2)), Expression.Multiply(C(2), x), Expression.Multiply(a, x), x, C(15), a,
            Expression.Multiply(C(3), Expression.Call(Cos, x)), Expression.Multiply(C(5), Expression.Call(Sin, x)),
        ]);
        yield return LeftSum(D.Take(24));
        yield return LeftSum(D.Take(12).Concat(D.Take(12)));
        yield return D.Take(16).Aggregate<Expression>(Expression.Subtract);
        yield return D.Take(12).Select(static (p, i) => (Expression)Expression.Multiply(C(i + 2), p)).Aggregate(Expression.Add);
        yield return D.Take(10).Aggregate<Expression>(Expression.Multiply);
        yield return LeftSum(D.Take(8).Select(static p => (Expression)Expression.Call(Sin, p)).Concat(D.Take(8).Select(static p => (Expression)Expression.Call(Sin, p))));

        Expression shared = LeftSum([a, b, C(2)]);
        yield return Expression.Add(Expression.Add(shared, x), shared);

        ParameterExpression u = Expression.Parameter(typeof(double), "u");
        ParameterExpression v = Expression.Parameter(typeof(double), "v");
        Expression body = LeftSum([u, v, u, Expression.Multiply(C(2), v), x]);
        yield return Expression.Lambda<Func<double, double, double>>(body, u, v);
        Expression inner = Expression.Lambda<Func<double, double>>(LeftSum([u, x, u]), u);
        yield return Expression.Lambda<Func<double, Func<double, double>>>(inner, v);
    }

    /// <summary>
    /// Every corpus tree simplifies to the dump it gave before the table existed: the reference is the simplification of the
    /// same tree with no earlier entry (a fresh operation), compared with a simplification whose operation already holds the
    /// metadata of every node of the tree (the tree simplified twice in one operation).
    /// </summary>
    [TestMethod]
    public void ReuseNeverChangesAResult()
    {
        foreach (Expression tree in Corpus())
        {
            var simplifier = new ExpressionSimplifier();
            string reference = Dump(simplifier.Simplify(tree));
            (Expression First, Expression Second) both = InOperation(simplifier, () => (simplifier.TransformDetached(tree), simplifier.TransformDetached(tree)));
            Assert.AreEqual(reference, Dump(both.First), $"First simplification of {tree}.");
            Assert.AreEqual(reference, Dump(both.Second), $"Second simplification of {tree} in the same operation.");
        }
    }

    /// <summary>
    /// A level by level chain (a derived simplifier, whose plan does not hold the flatten rule) keys and hashes each of its
    /// terms once: every enclosing level reads the metadata back. Before the table each level rebuilt the keys of every term
    /// below it: level <c>k</c> keys <c>k</c> terms, <c>n(n + 1)/2 - 1</c> builds for <c>n</c> terms.
    /// </summary>
    [TestMethod]
    public void LevelByLevelChainKeysEachTermOnce()
    {
        const int Count = 24;
        Expression chain = LeftSum(D.Take(Count));
        SimplificationMetadataStatistics statistics = Measure(() => new DerivedSimplifier().Simplify(chain));

        // The first level (two terms) stores nothing, so its two terms are keyed and hashed once more by the second one.
        Assert.IsTrue(statistics.KeyBuilds <= Count + 4, $"Each term keyed about once ({statistics.KeyBuilds}).");
        Assert.IsTrue(statistics.HashComputations <= Count + 4, $"Each term hashed about once ({statistics.HashComputations}).");
        // At least once per level and term below it (the rules of a level compare its operands too, which keys them again).
        Assert.IsTrue(statistics.KeyReuses >= Count * (Count + 1) / 2 - 1 - Count, $"Every enclosing level reuses the keys of the terms below it ({statistics.KeyReuses}).");
        Assert.AreEqual(statistics.KeyReuses, statistics.HashReuses);
    }

    /// <summary>An admitted flatten chain canonicalizes once: nothing is reused and nothing is built twice.</summary>
    [TestMethod]
    public void AdmittedChainBuildsEachKeyOnce()
    {
        const int Count = 24;
        Expression chain = LeftSum(D.Take(Count));
        SimplificationMetadataStatistics statistics = Measure(() => new ExpressionSimplifier().Simplify(chain));

        Assert.AreEqual(Count, statistics.KeyBuilds, "One key per term.");
        Assert.AreEqual(0, statistics.KeyReuses, "One canonicalization: nothing to reuse.");
    }

    /// <summary>The stored hash is the raw structural hash, consistent with the raw structural equality.</summary>
    [TestMethod]
    public void HashIsTheRawStructuralHash()
    {
        var simplifier = new ExpressionSimplifier();
        Expression first = Expression.Multiply(C(3), Expression.Call(Cos, D[0]));
        Expression second = Expression.Multiply(C(3), Expression.Call(Cos, D[0]));
        Expression other = Expression.Multiply(C(3), Expression.Call(Cos, D[1]));
        ParameterExpression[][] scopes = [];

        (int First, int Again, int Second, int Other) hashes = InOperation(simplifier, () => (
            ExpressionSimplifier.GetStructuralHash(first),
            ExpressionSimplifier.GetStructuralHash(first),
            ExpressionSimplifier.GetStructuralHash(second),
            ExpressionSimplifier.GetStructuralHash(other)));

        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(first), hashes.First);
        Assert.AreEqual(hashes.First, hashes.Again, "Deterministic within the operation.");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(first, second));
        Assert.AreEqual(hashes.First, hashes.Second, "Structurally equal nodes have the same hash.");
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(other), hashes.Other);
    }

    /// <summary>
    /// A hash collision never groups two terms: the grouping confirms a hash match by structural equality. Two distinct
    /// unknown-kind terms with the same stored hash stay apart.
    /// </summary>
    [TestMethod]
    public void HashCollisionIsConfirmedStructurally()
    {
        var simplifier = new ExpressionSimplifier();
        Expression x = Expression.Multiply(C(2), D[0]);
        Expression y = Expression.Multiply(C(2), D[1]);
        string reference = Dump(InOperation(simplifier, () => ExpressionSimplifierAccess.CanonicalizeAdditive(simplifier, x, y)));

        // Forge a collision: both terms get the same stored hash before the canonicalization reads it.
        string forged = Dump(InOperation(simplifier, () =>
        {
            ExpressionMetadataTable table = ExpressionResourceBudget.ActiveSession!.SimplificationMetadata ??= new ExpressionMetadataTable();
            table.SetHash(x, 42);
            table.SetHash(y, 42);
            return ExpressionSimplifierAccess.CanonicalizeAdditive(simplifier, x, y);
        }));

        Assert.AreEqual(reference, forged, "Equal hashes are not equality.");
        StringAssert.Contains(forged, "d0");
        StringAssert.Contains(forged, "d1");
    }

    /// <summary>
    /// A key depends on the enclosing scopes: a node keyed under one set of frames is keyed again under another one, and the
    /// key reused under the same frames is the key a fresh build gives.
    /// </summary>
    [TestMethod]
    public void KeyIsReusedOnlyUnderTheSameScopes()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression u = Expression.Parameter(typeof(double), "u");
        Expression term = Expression.Multiply(C(2), u);
        ParameterExpression[] frame = [u];
        ParameterExpression[][] bound = [frame];
        ParameterExpression[][] sameFrames = [frame];
        ParameterExpression[][] free = [];

        SimplificationMetadataStatistics statistics = Measure(() => InOperation(simplifier, () =>
        {
            var first = ExpressionSimplifier.GetCanonicalKey(term, bound);
            var again = ExpressionSimplifier.GetCanonicalKey(term, sameFrames);
            var unbound = ExpressionSimplifier.GetCanonicalKey(term, free);
            Assert.AreSame(first, again, "Same frames: the stored key.");
            Assert.AreNotSame(first, unbound, "Other frames: a new key.");
            Assert.AreEqual(0, ExpressionCanonicalOrder.BuildKeyFromSnapshot(term, bound).CompareTo(again));
            Assert.AreEqual(0, ExpressionCanonicalOrder.BuildKeyFromSnapshot(term, free).CompareTo(unbound));
            Assert.AreNotEqual(0, first.CompareTo(unbound), "A bound and a free parameter key differently.");
            return 0;
        }));

        Assert.AreEqual(2, statistics.KeyBuilds);
        Assert.AreEqual(1, statistics.KeyReuses);
    }

    /// <summary>
    /// The states: leaves of the built-in type are canonical; the prepared terms of a flatten chain are locally simplified for
    /// the instance that prepared them only; anything else is unknown, and a derived simplifier proves nothing about leaves.
    /// </summary>
    [TestMethod]
    public void StatesAreClaimsOfTheRecordingInstance()
    {
        var simplifier = new ExpressionSimplifier();
        var other = new ExpressionSimplifier();
        var derived = new DerivedSimplifier();
        Expression product = Expression.Multiply(D[1], C(3));
        Expression chain = LeftSum([D[0], product, C(5)]);

        InOperation(simplifier, () =>
        {
            // States are recorded only in a table the operation already holds.
            ExpressionResourceBudget.ActiveSession!.SimplificationMetadata ??= new ExpressionMetadataTable();
            Expression result = simplifier.TransformDetached(chain);
            Assert.AreEqual(CanonicalizationState.Canonical, simplifier.GetCanonicalizationState(D[0]));
            Assert.AreEqual(CanonicalizationState.Canonical, simplifier.GetCanonicalizationState(C(5)));
            Assert.AreEqual(CanonicalizationState.Unknown, derived.GetCanonicalizationState(D[0]), "A derived simplifier may rewrite leaves.");

            Expression preparedProduct = FindProduct(result);
            Assert.AreEqual(CanonicalizationState.LocallySimplified, simplifier.GetCanonicalizationState(preparedProduct));
            Assert.AreEqual(CanonicalizationState.Unknown, other.GetCanonicalizationState(preparedProduct), "Another instance reads nothing.");
            Assert.AreEqual(CanonicalizationState.Unknown, simplifier.GetCanonicalizationState(result), "The root was not recorded.");
            Assert.AreEqual(CanonicalizationState.Unknown, simplifier.GetCanonicalizationState(product), "A raw term is not simplified.");
            return 0;
        });

        // The table lives as long as the operation.
        Assert.AreEqual(CanonicalizationState.Unknown, simplifier.GetCanonicalizationState(product));
        Assert.IsNull(ExpressionResourceBudget.ActiveSession?.SimplificationMetadata);
    }

    /// <summary>Finds the first product of a tree.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The product.</returns>
    private static Expression FindProduct(Expression expression) => expression switch
    {
        BinaryExpression { NodeType: ExpressionType.Multiply } product => product,
        BinaryExpression binary => TryFind(binary.Left) ?? FindProduct(binary.Right),
        _ => throw new AssertFailedException("No product in " + expression),
    };

    /// <summary>Finds the first product of a tree, or <see langword="null"/>.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The product, or <see langword="null"/>.</returns>
    private static Expression? TryFind(Expression expression) => expression switch
    {
        BinaryExpression { NodeType: ExpressionType.Multiply } product => product,
        BinaryExpression binary => TryFind(binary.Left) ?? TryFind(binary.Right),
        _ => null,
    };

    /// <summary>The leaf state is the claim <c>S(e) = e</c>: simplifying a leaf gives a structurally equal leaf.</summary>
    [TestMethod]
    public void CanonicalLeavesAreFixedPoints()
    {
        var simplifier = new ExpressionSimplifier();
        Expression[] leaves = [D[0], C(5), C(double.NaN), C(double.PositiveInfinity), C(-0.0), Expression.Constant(3m), Expression.Constant(null, typeof(string)), Expression.Constant("text")];
        foreach (Expression leaf in leaves)
        {
            Assert.AreEqual(CanonicalizationState.Canonical, simplifier.GetCanonicalizationState(leaf));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(leaf, simplifier.Simplify(leaf)), $"S({leaf}) = {leaf}.");
        }
    }

    /// <summary>
    /// A node shared by the tree and by a lambda that binds its parameters is keyed once per lexical context; the results
    /// match the reference for every context.
    /// </summary>
    [TestMethod]
    public void SharedNodeAcrossLexicalContexts()
    {
        ParameterExpression u = Expression.Parameter(typeof(double), "u");
        Expression shared = Expression.Multiply(C(2), u);
        Expression inner = Expression.Lambda<Func<double, double>>(LeftSum([shared, D[0], shared]), u);
        Expression outer = Expression.Lambda<Func<double, Func<double, double>>>(Expression.Block(inner), u);
        Expression tree = Expression.Lambda<Func<double, double>>(LeftSum([shared, D[0], shared]), u);

        foreach (Expression candidate in new[] { inner, outer, tree })
        {
            var simplifier = new ExpressionSimplifier();
            string reference = Dump(simplifier.Simplify(candidate));
            string again = Dump(InOperation(simplifier, () => { simplifier.TransformDetached(tree); simplifier.TransformDetached(inner); return simplifier.TransformDetached(candidate); }));
            Assert.AreEqual(reference, again);
        }
    }

    /// <summary>
    /// Policies and derived simplifiers running inside the operation of another instance give the results they give alone (a
    /// derived simplifier or another instance enters an operation of its own, with its own table; the owner's joins it). A tree a simplifier refuses alone (the level by level treatment of a derived simplifier exceeds
    /// the transformation budget on the repeated chains of the corpus, as on <c>master</c>: the table never changes a
    /// transformation visit) is refused in the shared operation too.
    /// </summary>
    [TestMethod]
    public void InstancesSharingAnOperationKeepTheirResults()
    {
        ExpressionSimplifier[] simplifiers =
        [
            new ExpressionSimplifier(),
            new DerivedSimplifier(),
            new ExpressionSimplifier { Options = new ExpressionSimplifierOptions { AggressiveSimplification = true } },
        ];
        foreach (Expression tree in Corpus())
        {
            string[] alone = [.. simplifiers.Select(simplifier => Outcome(() => simplifier.Simplify(tree)))];
            string[] shared = InOperation(simplifiers[0], () => simplifiers.Select(simplifier => Outcome(() => simplifier.TransformDetached(tree))).ToArray());
            for (int i = 0; i < simplifiers.Length; i++) Assert.AreEqual(alone[i], shared[i], $"{simplifiers[i].GetType().Name} #{i} on {tree}.");
        }
    }

    /// <summary>
    /// A nested public call (user code calling <see cref="ExpressionSimplifier.Simplify(Expression)"/> while an operation
    /// runs) is an operation of its own: it starts with an empty table, never reads the caller's entries, so it costs exactly
    /// what it costs alone, and the caller's table is restored, unchanged, when it returns.
    /// </summary>
    [TestMethod]
    public void NestedPublicCallStartsWithAnEmptyTable()
    {
        var simplifier = new ExpressionSimplifier();
        Expression chain = LeftSum(D.Take(16).Append(D[0]));
        SimplificationMetadataStatistics alone = Measure(() => new ExpressionSimplifier().Simplify(chain));

        InOperation(simplifier, () =>
        {
            simplifier.TransformDetached(chain);
            ExpressionMetadataTable callerTable = ExpressionResourceBudget.ActiveSession!.SimplificationMetadata!;
            int callerCount = callerTable.Count;
            Assert.IsTrue(callerCount > 0, "The caller's operation holds entries.");

            SimplificationMetadataStatistics nested = Measure(() => new ExpressionSimplifier().Simplify(chain));
            Assert.AreEqual(alone.KeyBuilds, nested.KeyBuilds, "No caller key is reused.");
            Assert.AreEqual(alone.HashComputations, nested.HashComputations, "No caller hash is reused.");
            Assert.AreEqual(alone.KeyReuses, nested.KeyReuses);
            Assert.AreSame(callerTable, ExpressionResourceBudget.ActiveSession!.SimplificationMetadata, "The caller's table is restored.");
            Assert.AreEqual(callerCount, callerTable.Count, "The nested call added nothing to it.");
            return 0;
        });
    }

    /// <summary>The dump of a simplification, or <c>LIMIT</c> when it exceeds a resource limit.</summary>
    /// <param name="simplify">The simplification.</param>
    /// <returns>The outcome.</returns>
    private static string Outcome(Func<Expression> simplify)
    {
        try
        {
            return Dump(simplify());
        }
        catch (ExpressionResourceLimitException)
        {
            return "LIMIT";
        }
    }

    /// <summary>A budget refusal in the middle of a hash leaves no entry: the next operation computes it from scratch.</summary>
    [TestMethod]
    public void BudgetRefusalLeavesNoPartialEntry()
    {
        Expression chain = LeftSum(D.Take(30).Append(D[0]));
        var limited = new ExpressionSimplifier(new ExpressionResourceLimits { MaxStructuralVisits = 200 });
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain));
        Assert.IsNull(ExpressionResourceBudget.ActiveSession?.SimplificationMetadata, "The table is dropped with the operation.");

        var simplifier = new ExpressionSimplifier();
        Assert.AreEqual(Dump(simplifier.Simplify(chain)), Dump(new ExpressionSimplifier().Simplify(chain)));
    }

    /// <summary>
    /// The table grows only for a new node: updating the hash, key or state of a stored node, even at the load-factor
    /// threshold, keeps its capacity, and the next new node grows it.
    /// </summary>
    [TestMethod]
    public void TableGrowsOnlyForNewNodes()
    {
        var table = new ExpressionMetadataTable();
        int capacity = table.Capacity;
        Expression[] nodes = [.. Enumerable.Range(0, capacity / 2).Select(static i => (Expression)Expression.Constant((double)i))];
        foreach (Expression node in nodes) table.SetHash(node, 1);
        Assert.AreEqual(capacity / 2, table.Count, "At the threshold.");

        var simplifier = new ExpressionSimplifier();
        ParameterExpression[][] scopes = [];
        ExpressionCanonicalOrder.KeyNode key = ExpressionCanonicalOrder.BuildKeyFromSnapshot(nodes[0], scopes);
        foreach (Expression node in nodes)
        {
            table.SetHash(node, 2);
            table.SetKey(node, key, scopes);
            table.SetState(node, CanonicalizationState.LocallySimplified, simplifier);
        }

        Assert.AreEqual(capacity, table.Capacity, "Updates of stored nodes never grow the table.");
        Assert.AreEqual(capacity / 2, table.Count);
        Assert.IsTrue(table.TryGetValue(nodes[^1], out ExpressionMetadataTable.Entry entry) && entry.HasHash && entry.Hash == 2 && entry.Key is not null);

        table.SetHash(Expression.Constant(-1.0), 3);
        Assert.AreEqual(capacity * 2, table.Capacity, "A new node past the threshold grows it.");
        foreach (Expression node in nodes) Assert.IsTrue(table.TryGetValue(node, out _), "Every node survives the growth.");
    }

    /// <summary>The description gathers the hash, the key and the state of a term.</summary>
    [TestMethod]
    public void DescribeTermGathersTheMetadata()
    {
        var simplifier = new ExpressionSimplifier();
        Expression term = Expression.Multiply(C(3), D[0]);
        ParameterExpression[][] scopes = [];
        SimplifiedExpressionInfo info = InOperation(simplifier, () => simplifier.DescribeTerm(term, scopes));

        Assert.AreSame(term, info.Expression);
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(term), info.StructuralHash);
        Assert.AreEqual(0, ExpressionCanonicalOrder.BuildKeyFromSnapshot(term, scopes).CompareTo(info.Key));
        Assert.AreEqual(CanonicalizationState.Unknown, info.State);
    }

    /// <summary>Access to the private canonicalization helpers.</summary>
    private static class ExpressionSimplifierAccess
    {
        /// <summary>The hash accessor.</summary>
        private static readonly MethodInfo HashMethod = typeof(ExpressionSimplifier).GetMethod("GetStructuralHash", BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>The key accessor.</summary>
        private static readonly MethodInfo KeyMethod = typeof(ExpressionSimplifier).GetMethod("GetCanonicalKey", BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>The additive canonicalization.</summary>
        private static readonly MethodInfo CanonicalizeMethod = typeof(ExpressionSimplifier).GetMethod("CanonicalizeAdditiveExpression", BindingFlags.NonPublic | BindingFlags.Instance)!;

        /// <summary>Calls the hash accessor.</summary>
        /// <param name="expression">The node.</param>
        /// <returns>Its hash.</returns>
        public static int Hash(Expression expression) => (int)HashMethod.Invoke(null, [expression])!;

        /// <summary>Calls the key accessor.</summary>
        /// <param name="expression">The node.</param>
        /// <param name="scopes">The scopes.</param>
        /// <returns>Its key.</returns>
        public static ExpressionCanonicalOrder.KeyNode Key(Expression expression, ParameterExpression[][] scopes)
            => (ExpressionCanonicalOrder.KeyNode)KeyMethod.Invoke(null, [expression, scopes])!;

        /// <summary>Canonicalizes <c>left + right</c>.</summary>
        /// <param name="simplifier">The simplifier.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The canonical form.</returns>
        public static Expression CanonicalizeAdditive(ExpressionSimplifier simplifier, Expression left, Expression right)
            => (Expression)CanonicalizeMethod.Invoke(simplifier, [ExpressionType.Add, left, right, typeof(double)])!;
    }
}
