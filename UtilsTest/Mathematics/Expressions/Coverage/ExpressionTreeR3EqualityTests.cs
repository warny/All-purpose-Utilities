using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R3, checkpoint B: structural equality, hashing and canonical keys of the initializer and index families (public and raw
/// comparers). Bindings and initializers are compared in declaration order; members, binding kinds, <c>Add</c> methods,
/// constructors and indexers are distinguished; bound parameters are compared up to alpha-renaming (R2 scope model). Positive
/// pairs agree on every contract; negative pairs are unequal (their hashes may collide).
/// </summary>
[TestClass]
[TestCategory("ETC-R3")]
public class ExpressionTreeR3EqualityTests
{
    /// <summary>A free parameter shared by both sides of the pairs.</summary>
    private static readonly ParameterExpression Free = Expression.Parameter(typeof(double), "free");

    /// <summary>A free trace parameter shared by both sides of the pairs.</summary>
    private static readonly ParameterExpression Log = Expression.Parameter(typeof(R3TraceLog), "log");

    /// <summary>A free grid parameter shared by both sides of the pairs.</summary>
    private static readonly ParameterExpression Grid = Expression.Parameter(typeof(R3ShadowingGrid), "grid");

    /// <summary>Gets a public member of <see cref="R3TracedHolder"/>.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The member.</returns>
    private static MemberInfo M(string name) => R3TracedHolder.Member(name);

    /// <summary>Builds <c>new R3TracedHolder(log)</c>.</summary>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder() => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog)])!, Log);

    /// <summary>Builds <c>new R3TracedHolder(log, seed)</c>.</summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder(Expression seed) => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog), typeof(double)])!, Log, seed);

    /// <summary>Builds <c>new R3ShadowingList(log)</c>.</summary>
    /// <returns>The creation.</returns>
    private static NewExpression NewList() => Expression.New(typeof(R3ShadowingList).GetConstructor([typeof(R3TraceLog)])!, Log);

    /// <summary>Builds an <c>Add</c> initializer of <see cref="R3TracedList"/>, selecting the base overload by the argument types.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The initializer.</returns>
    private static ElementInit Add(params Expression[] arguments)
        => Expression.ElementInit(R3TracedList.AddMethod(arguments.Select(argument => argument.Type).ToArray()), arguments);

    /// <summary>Builds a lambda over a fresh parameter named <paramref name="name"/> whose body is built by <paramref name="body"/>.</summary>
    /// <param name="name">The parameter name (never significant).</param>
    /// <param name="body">Builds the body from the parameter.</param>
    /// <returns>The lambda.</returns>
    private static Expression Lambda(string name, Func<ParameterExpression, Expression> body)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), name);
        return Expression.Lambda(body(x), x);
    }

    /// <summary>
    /// Asserts that <paramref name="x"/> and <paramref name="y"/> are equal in both directions on the public and raw comparers,
    /// hash identically on both, can be retrieved from a <see cref="HashSet{T}"/> and a <see cref="Dictionary{TKey, TValue}"/>
    /// keyed by the other, and tie in the canonical order.
    /// </summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <param name="message">The case name.</param>
    private static void AssertEquivalent(Expression x, Expression y, string message)
    {
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(x, y), $"{message}: raw x = y");
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(y, x), $"{message}: raw y = x");
        Assert.AreEqual(ExpressionComparer.StructuralHashRaw(x), ExpressionComparer.StructuralHashRaw(y), $"{message}: raw hash");
        Assert.IsTrue(ExpressionComparer.Default.Equals(x, y), $"{message}: public x = y");
        Assert.IsTrue(ExpressionComparer.Default.Equals(y, x), $"{message}: public y = x");
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(x), ExpressionComparer.Default.GetHashCode(y), $"{message}: public hash");

        var set = new HashSet<Expression>(ExpressionComparer.Default) { x };
        Assert.IsTrue(set.Contains(y), $"{message}: HashSet");
        var dictionary = new Dictionary<Expression, int>(ExpressionComparer.Default) { [y] = 1 };
        Assert.IsTrue(dictionary.ContainsKey(x), $"{message}: Dictionary");

        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(x, y, []), $"{message}: canonical x vs y");
        Assert.AreEqual(0, ExpressionCanonicalOrder.Compare(y, x, []), $"{message}: canonical y vs x");
    }

    /// <summary>
    /// Asserts that <paramref name="x"/> and <paramref name="y"/> are unequal in both directions on the public and raw comparers
    /// and, unless they differ only by a free parameter (which the canonical order ties by design, without inventing an order
    /// between two free parameters), are ordered antisymmetrically and never tied by the canonical order.
    /// </summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <param name="message">The case name.</param>
    /// <param name="keysSeparate">Whether the canonical keys must separate the trees.</param>
    private static void AssertDistinct(Expression x, Expression y, string message, bool keysSeparate = true)
    {
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(x, y), $"{message}: raw x = y");
        Assert.IsFalse(ExpressionComparer.StructuralEqualsRaw(y, x), $"{message}: raw y = x");
        Assert.IsFalse(ExpressionComparer.Default.Equals(x, y), $"{message}: public x = y");
        Assert.IsFalse(ExpressionComparer.Default.Equals(y, x), $"{message}: public y = x");
        if (!keysSeparate) return;

        int forward = ExpressionCanonicalOrder.Compare(x, y, []);
        Assert.AreNotEqual(0, forward, $"{message}: canonical keys must separate the trees");
        Assert.AreEqual(-Math.Sign(forward), Math.Sign(ExpressionCanonicalOrder.Compare(y, x, [])), $"{message}: antisymmetry");
    }

    /// <summary>Two independently built but identical trees of every R3 catalog sample are equivalent on every contract.</summary>
    [TestMethod]
    public void IndependentCatalogSamples_AreEquivalent()
    {
        string[] names = ["MemberInit", "MemberInit.MemberMemberBinding", "MemberInit.MemberListBinding", "ListInit", "ListInit.ExplicitAddMethod", "Index", "Index.Indexer"];
        foreach (string name in names)
        {
            AssertEquivalent(ExpressionNodeCatalog.Get(name).Build(), ExpressionNodeCatalog.Get(name).Build(), name);
        }
    }

    /// <summary>Independently built nested initializations (member, nested member and list bindings, two-parameter <c>Add</c>) are equivalent.</summary>
    [TestMethod]
    public void IndependentNestedInitializations_AreEquivalent()
    {
        static Expression Build() => Expression.MemberInit(
            NewHolder(Expression.Add(Free, Expression.Constant(1.0))),
            Expression.Bind(M(nameof(R3TracedHolder.Value)), Free),
            Expression.MemberBind(
                M(nameof(R3TracedHolder.Child)),
                Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Other)), Free)),
                Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(Free), Add(Free, Expression.Constant(2.0)))),
            Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(Expression.Constant("text"))));

        AssertEquivalent(Build(), Build(), "nested initialization");
        AssertEquivalent(
            Expression.ListInit(NewList(), Add(Free), Add(Free, Free)),
            Expression.ListInit(NewList(), Add(Free), Add(Free, Free)),
            "list initialization");
        AssertEquivalent(
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [Expression.Constant(1), Expression.Constant(2)]),
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [Expression.Constant(1), Expression.Constant(2)]),
            "indexer access");
    }

    /// <summary>
    /// Bound parameters are compared up to alpha-renaming inside the R3 families: two lambdas over differently named and
    /// allocated parameters, used in bindings, <c>Add</c> arguments and index arguments, are equivalent; a free parameter is
    /// not equivalent to a bound one.
    /// </summary>
    [TestMethod]
    public void AlphaRenamedParameters_AreEquivalent()
    {
        Expression Build(string name) => Lambda(name, x => Expression.MemberInit(
            NewHolder(x),
            Expression.Bind(M(nameof(R3TracedHolder.Value)), Expression.Add(x, Free)),
            Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(x, x)),
            Expression.Bind(M(nameof(R3TracedHolder.Other)), Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [Expression.Convert(x, typeof(int)), Expression.Constant(0)]))));

        AssertEquivalent(Build("x"), Build("y"), "alpha-renamed lambda");
        AssertDistinct(
            Lambda("x", x => Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), x))),
            Lambda("x", x => Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), Free))),
            "bound versus free");
    }

    /// <summary>
    /// Metadata distinguishes otherwise identical trees: the assigned member, the binding kind, the binding order, the
    /// constructor, the <c>Add</c> method (a hiding method on the same type), the order of the <c>Add</c> arguments, the
    /// initializer order, and the indexer (a hiding indexer on the same receiver), the receiver and the index arguments.
    /// </summary>
    [TestMethod]
    public void MetadataAndOrder_DistinguishTheTrees()
    {
        // Distinct constants rather than free parameters: two free parameters of the same type tie in the canonical order by design.
        Expression a = Expression.Constant(1.0);
        Expression b = Expression.Constant(2.0);
        MemberAssignment value = Expression.Bind(M(nameof(R3TracedHolder.Value)), a);
        MemberAssignment other = Expression.Bind(M(nameof(R3TracedHolder.Other)), b);

        AssertDistinct(
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a)),
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Other)), a)),
            "different member");
        AssertDistinct(
            Expression.MemberInit(NewHolder(), value, other),
            Expression.MemberInit(NewHolder(), other, value),
            "permuted bindings");
        AssertDistinct(
            Expression.MemberInit(NewHolder(), value),
            Expression.MemberInit(NewHolder(), value, value),
            "repeated binding");
        AssertDistinct(
            Expression.MemberInit(NewHolder(), value),
            Expression.MemberInit(NewHolder(a), value),
            "different constructor");
        AssertDistinct(
            Expression.MemberInit(NewHolder(), Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Value)), a))),
            Expression.MemberInit(NewHolder(), Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Other)), a))),
            "different nested member");
        AssertDistinct(
            Expression.MemberInit(NewHolder(), Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(a))),
            Expression.MemberInit(NewHolder(), Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(a), Add(a))),
            "different initializer count");

        AssertDistinct(
            Expression.ListInit(NewList(), Add(a)),
            Expression.ListInit(NewList(), Expression.ElementInit(R3ShadowingList.HidingAdd, a)),
            "different Add method");
        AssertDistinct(
            Expression.ListInit(NewList(), Add(a, b)),
            Expression.ListInit(NewList(), Add(b, a)),
            "permuted Add arguments");
        AssertDistinct(
            Expression.ListInit(NewList(), Add(a), Add(b)),
            Expression.ListInit(NewList(), Add(b), Add(a)),
            "permuted initializers");

        Expression row = Expression.Constant(1);
        Expression column = Expression.Constant(2);
        AssertDistinct(
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [row, column]),
            Expression.MakeIndex(Grid, R3ShadowingGrid.HidingIndexer, [row, column]),
            "different indexer");
        AssertDistinct(
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [row, column]),
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [column, row]),
            "permuted index arguments");
        AssertDistinct(
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [row, column]),
            Expression.MakeIndex(Expression.Parameter(typeof(R3ShadowingGrid), "other"), R3TracedGrid.CellIndexer, [row, column]),
            "different receiver",
            keysSeparate: false);
        AssertDistinct(
            Expression.ArrayAccess(Expression.Parameter(typeof(double[,]), "array"), row, column),
            Expression.ArrayAccess(Expression.Parameter(typeof(double[,]), "array"), row, column),
            "different free array receivers",
            keysSeparate: false);
    }

    /// <summary>
    /// The structural hash reflects the metadata and the order that equality distinguishes (no systematic collision): every
    /// pair of the distinct corpus below hashes differently on the raw hash.
    /// </summary>
    [TestMethod]
    public void Hash_SeparatesTheMetadataEqualityDistinguishes()
    {
        Expression[] corpus = DistinctCorpus();
        int[] hashes = corpus.Select(ExpressionComparer.StructuralHashRaw).ToArray();
        for (int i = 0; i < corpus.Length; i++)
        {
            for (int j = i + 1; j < corpus.Length; j++)
            {
                Assert.AreNotEqual(hashes[i], hashes[j], $"corpus {i} and {j} collide");
            }
        }
    }

    /// <summary>Builds a corpus of pairwise distinct R3 trees differing by one dimension each.</summary>
    /// <returns>The corpus.</returns>
    private static Expression[] DistinctCorpus()
    {
        Expression a = Expression.Constant(1.0);
        Expression b = Expression.Constant(2.0);
        Expression row = Expression.Constant(1);
        Expression column = Expression.Constant(2);
        return
        [
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a)),
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Other)), a)),
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a), Expression.Bind(M(nameof(R3TracedHolder.Other)), b)),
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Other)), b), Expression.Bind(M(nameof(R3TracedHolder.Value)), a)),
            Expression.MemberInit(NewHolder(a)),
            Expression.MemberInit(NewHolder(), Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Value)), a))),
            Expression.MemberInit(NewHolder(), Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(a))),
            Expression.ListInit(NewList(), Add(a)),
            Expression.ListInit(NewList(), Expression.ElementInit(R3ShadowingList.HidingAdd, a)),
            Expression.ListInit(NewList(), Add(a, b)),
            Expression.ListInit(NewList(), Add(b, a)),
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [row, column]),
            Expression.MakeIndex(Grid, R3ShadowingGrid.HidingIndexer, [row, column]),
            Expression.MakeIndex(Grid, R3TracedGrid.CellIndexer, [column, row]),
            Expression.MakeIndex(Grid, R3TracedGrid.NameIndexer, [Expression.Constant("k")]),
        ];
    }

    /// <summary>
    /// The canonical order is a total preorder consistent with equality over a corpus mixing R3 trees and their alpha-renamed
    /// or rebuilt twins: equal trees tie, distinct trees never tie, the comparison is antisymmetric and transitive, and the
    /// raw hash agrees with every equality.
    /// </summary>
    [TestMethod]
    public void CanonicalOrder_IsConsistentWithEquality_AndTransitive()
    {
        Expression[] distinct = DistinctCorpus();
        Expression[] twins = DistinctCorpus();
        Expression[] corpus =
        [
            .. distinct,
            .. twins,
            Lambda("x", x => Expression.ListInit(NewList(), Add(x))),
            Lambda("y", y => Expression.ListInit(NewList(), Add(y))),
            Lambda("x", x => Expression.MemberInit(NewHolder(x), Expression.Bind(M(nameof(R3TracedHolder.Value)), x))),
        ];

        int n = corpus.Length;
        var compare = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                compare[i, j] = Math.Sign(ExpressionCanonicalOrder.Compare(corpus[i], corpus[j], []));
                bool equal = ExpressionComparer.StructuralEqualsRaw(corpus[i], corpus[j]);
                Assert.AreEqual(equal, compare[i, j] == 0, $"key tie must match equality for {i},{j}");
                if (equal) Assert.AreEqual(ExpressionComparer.StructuralHashRaw(corpus[i]), ExpressionComparer.StructuralHashRaw(corpus[j]), $"hash {i},{j}");
            }
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                Assert.AreEqual(-compare[i, j], compare[j, i], $"antisymmetry {i},{j}");
                for (int k = 0; k < n; k++)
                {
                    if (compare[i, j] <= 0 && compare[j, k] <= 0) Assert.IsTrue(compare[i, k] <= 0, $"transitivity {i},{j},{k}");
                }
            }
        }
    }

    /// <summary>
    /// Comparing, hashing and keying initializations never runs user code: the constructors, setters, getters, <c>Add</c>
    /// methods and indexers of instrumented constant receivers are never called.
    /// </summary>
    [TestMethod]
    public void EqualityHashAndKeys_NeverRunUserCode()
    {
        var log = new R3TraceLog();
        var grid = new R3TracedGrid(log);
        Expression Build() => Expression.Block(
            Expression.MemberInit(
                Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog)])!, Expression.Constant(log)),
                Expression.Bind(M(nameof(R3TracedHolder.Value)), Free),
                Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Other)), Free)),
                Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(Free))),
            Expression.Assign(Expression.MakeIndex(Expression.Constant(grid), R3TracedGrid.CellIndexer, [Expression.Constant(0), Expression.Constant(1)]), Free));

        Expression x = Build();
        Expression y = Build();
        AssertEquivalent(x, y, "instrumented trees");
        Assert.AreEqual(0, log.Entries.Count, string.Join(" ; ", log.Entries));
    }
}
