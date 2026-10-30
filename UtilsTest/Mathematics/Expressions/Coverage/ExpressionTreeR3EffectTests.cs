using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R3, checkpoint C: the initializer and index families stay observable for the simplifier (ETC-15, strict C-06 boundary
/// of ETC R1b). Although they are now compared, hashed and keyed structurally, no rule or canonicalization may drop, merge,
/// repeat or move an initialization, an <c>Add</c> call, a getter, a setter, an indexer access or an array access that may
/// throw: every case below runs exactly like the original, with the same user calls in the same order, for the exact and a
/// derived simplifier.
/// </summary>
[TestClass]
[TestCategory("ETC-R3")]
public class ExpressionTreeR3EffectTests
{
    /// <summary>The free parameter of the samples; they run with <c>p = 1</c>.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Gets a public member of <see cref="R3TracedHolder"/>.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The member.</returns>
    private static System.Reflection.MemberInfo M(string name) => R3TracedHolder.Member(name);

    /// <summary>Builds <c>new R3TracedHolder(log) { Value = value }</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns>The initialization.</returns>
    private static Expression Holder(R3TraceLog log, Expression value)
        => Expression.MemberInit(
            Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog)])!, Expression.Constant(log)),
            Expression.Bind(M(nameof(R3TracedHolder.Value)), value));

    /// <summary>Builds the traced read <c>grid[name]</c>.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="name">The cell name.</param>
    /// <returns>The index access.</returns>
    private static Expression Cell(R3TracedGrid grid, string name)
        => Expression.MakeIndex(Expression.Constant(grid), R3TracedGrid.NameIndexer, [Expression.Constant(name)]);

    /// <summary>
    /// Runs the tree built by <paramref name="build"/> before and after simplification (exact and derived simplifiers) and
    /// asserts the same result, exception and trace of user calls; nothing runs during the simplification itself.
    /// </summary>
    /// <param name="build">Builds the tree over a fresh trace.</param>
    private static void AssertSameBehavior(Func<R3TraceLog, Expression> build)
    {
        var originalLog = new R3TraceLog();
        (string? Value, string? Exception, string[] Trace) expected = Run(build(originalLog), originalLog);
        foreach (ExpressionSimplifier simplifier in new ExpressionSimplifier[] { new(), new DerivedSimplifier() })
        {
            var log = new R3TraceLog();
            Expression simplified = simplifier.Simplify(build(log));
            Assert.AreEqual(0, log.Entries.Count, "No user code may run during the simplification.");
            (string? Value, string? Exception, string[] Trace) actual = Run(simplified, log);
            Assert.AreEqual(expected.Exception, actual.Exception, simplifier.GetType().Name + ": same exception");
            Assert.AreEqual(expected.Value, actual.Value, simplifier.GetType().Name + ": same result");
            CollectionAssert.AreEqual(expected.Trace, actual.Trace, simplifier.GetType().Name + ": same user calls " + string.Join(" ; ", actual.Trace));
        }
    }

    /// <summary>Compiles <paramref name="body"/> over <see cref="P"/>, runs it with <c>p = 1</c>, and records its result, exception and trace.</summary>
    /// <param name="body">The body.</param>
    /// <param name="log">The trace it writes to.</param>
    /// <returns>The result text (or <see langword="null"/>), the exception type (or <see langword="null"/>) and the trace.</returns>
    private static (string? Value, string? Exception, string[] Trace) Run(Expression body, R3TraceLog log)
    {
        Func<double, object> compiled = Expression.Lambda<Func<double, object>>(Expression.Convert(body, typeof(object)), P).Compile();
        try
        {
            object result = compiled(1.0);
            return (result is double number ? R3TraceLog.Text(number) : result?.ToString(), null, log.Entries.ToArray());
        }
        catch (Exception exception)
        {
            return (null, exception.GetType().FullName, log.Entries.ToArray());
        }
    }

    /// <summary>Two equal traced indexer reads added together are not merged into <c>2 * read</c>: the getter still runs twice.</summary>
    [TestMethod]
    public void EqualIndexerReads_AreNeverMerged()
    {
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Add(Cell(grid, "k"), Cell(grid, "k"));
        });
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Add(Expression.Multiply(P, Cell(grid, "k")), Expression.Multiply(P, Cell(grid, "k")));
        });
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Add(Expression.Multiply(Expression.Constant(2.0), Cell(grid, "k")), Expression.Multiply(Expression.Constant(3.0), Cell(grid, "k")));
        });
    }

    /// <summary>
    /// <c>a[i] - a[i]</c> and <c>a[i] * 0</c> are not reduced to <c>0</c> when the access may throw: an out-of-range index
    /// keeps throwing <see cref="IndexOutOfRangeException"/>, and an indexer read keeps running its getter.
    /// </summary>
    [TestMethod]
    public void ThrowingOrObservableAccesses_AreNeverCancelled()
    {
        Expression OutOfRange() => Expression.ArrayAccess(Expression.Constant(new double[2]), Expression.Convert(Expression.Add(P, Expression.Constant(4.0)), typeof(int)));
        AssertSameBehavior(_ => Expression.Subtract(OutOfRange(), OutOfRange()));
        AssertSameBehavior(_ => Expression.Multiply(OutOfRange(), Expression.Constant(0.0)));
        AssertSameBehavior(_ => Expression.Add(Expression.Multiply(Expression.Constant(0.0), OutOfRange()), P));
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Subtract(Cell(grid, "k"), Cell(grid, "k"));
        });
        AssertSameBehavior(log => Expression.Multiply(Cell(new R3TracedGrid(log), "k"), Expression.Constant(0.0)));
    }

    /// <summary>
    /// Two structurally equal initializations are two creations: reading the same member from both, subtracting them, or
    /// comparing their references keeps both constructions and both setters, and never reuses one allocation.
    /// </summary>
    [TestMethod]
    public void EqualInitializations_AreNeverDeduplicated()
    {
        AssertSameBehavior(log => Expression.Subtract(
            Expression.Property(Holder(log, P), nameof(R3TracedHolder.Value)),
            Expression.Property(Holder(log, P), nameof(R3TracedHolder.Value))));
        AssertSameBehavior(log => Expression.Add(
            Expression.Field(Holder(log, P), nameof(R3TracedHolder.Field)),
            Expression.Field(Holder(log, P), nameof(R3TracedHolder.Field))));
        AssertSameBehavior(log => Expression.ReferenceEqual(Holder(log, P), Holder(log, P)));
        AssertSameBehavior(log => Expression.Multiply(
            Expression.Property(Expression.ListInit(Expression.New(typeof(List<double>)), P, P), nameof(List<double>.Count)),
            Expression.Constant(0)));
    }

    /// <summary>
    /// An index write used as an operand keeps its effect and its position: <c>(a[0] = 5) * 0 + a[0]</c> still writes before
    /// reading, and <c>a[0] + (a[0] = 5)</c> still reads the old value first (no reordering of a read beyond a write).
    /// </summary>
    [TestMethod]
    public void IndexWrites_AreNeitherDroppedNorReordered()
    {
        Expression Element(double[] array) => Expression.ArrayAccess(Expression.Constant(array), Expression.Constant(0));
        AssertSameBehavior(_ =>
        {
            var array = new[] { 1.0 };
            return Expression.Add(Expression.Multiply(Expression.Assign(Element(array), Expression.Constant(5.0)), Expression.Constant(0.0)), Element(array));
        });
        AssertSameBehavior(_ =>
        {
            var array = new[] { 1.0 };
            return Expression.Add(Element(array), Expression.Assign(Element(array), Expression.Constant(5.0)));
        });
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Add(Cell(grid, "k"), Expression.AddAssign(Cell(grid, "k"), P));
        });
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Subtract(Expression.PreIncrementAssign(Cell(grid, "k")), Expression.PreIncrementAssign(Cell(grid, "k")));
        });
    }

    /// <summary>
    /// The additive and multiplicative canonicalizations keep observable R3 terms in their source order: reads of different
    /// cells and initializations are evaluated in the original order, whatever their canonical keys.
    /// </summary>
    [TestMethod]
    public void Canonicalization_KeepsObservableTermsInPlace()
    {
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Add(Expression.Add(Cell(grid, "b"), P), Expression.Add(Cell(grid, "a"), Expression.Multiply(P, Expression.Constant(1.0))));
        });
        AssertSameBehavior(log =>
        {
            var grid = new R3TracedGrid(log);
            return Expression.Multiply(Expression.Multiply(Cell(grid, "z"), Expression.Constant(2.0)), Expression.Multiply(Cell(grid, "a"), P));
        });
        AssertSameBehavior(log => Expression.Add(
            Expression.Property(Holder(log, Expression.Constant(9.0)), nameof(R3TracedHolder.Value)),
            Expression.Property(Holder(log, Expression.Constant(1.0)), nameof(R3TracedHolder.Value))));
    }

    /// <summary>
    /// Children of the R3 families are still simplified independently, in place: <c>p * 1</c> inside a binding, an <c>Add</c>
    /// argument or an index becomes <c>p</c>, while the initialization itself, its order and its calls are kept.
    /// </summary>
    [TestMethod]
    public void Children_AreSimplifiedInPlace()
    {
        Expression simplified = new ExpressionSimplifier().Simplify(Expression.ListInit(
            Expression.New(typeof(List<double>)),
            Expression.Multiply(P, Expression.Constant(1.0)),
            Expression.Add(P, Expression.Constant(0.0))));
        var listInit = (ListInitExpression)simplified;
        Assert.AreEqual(2, listInit.Initializers.Count);
        Assert.IsTrue(listInit.Initializers.All(initializer => ReferenceEquals(initializer.Arguments[0], P)));
    }
}
