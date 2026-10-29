using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Pins the user-code behavior of the simplifier and of the public comparer on additive chains holding opaque
/// constants (roadmap S5 P15): how many times, in which order and with which arguments the constants' own
/// <see cref="object.Equals(object?)"/> is called, which exception propagates, what re-entrant calls observe, and the
/// simplified trees. Every expected value was measured on the pre-P15 baseline (<c>ac702499</c>) and these tests pass
/// unchanged on it: P15 must remove internal work only.
/// <para>
/// ETC R1b (C-06): the opaque constants are now carried by the inert term of <see cref="SafeOpaqueTerm"/> instead of the
/// user method <see cref="IdentityFromObject"/>, which the simplifier no longer merges, cancels or reorders. Every user-call
/// count and call sequence is unchanged; the expected trees and form digests, whose text includes the carrier, were
/// re-measured on <c>master</c> (<c>b071bd0a</c>) with the new carrier and are identical on this branch. The extension-node
/// scenarios (an extension node is unknown code, never rewritten since R1b) assert the new read counts, bounded by their
/// former baseline values.
/// </para>
/// </summary>
[TestClass]
public class ExpressionSimplifierOpaqueConstantInvariantTests
{
    /// <summary>Returns a constant for an opaque argument, embedding user objects in numeric terms.</summary>
    /// <param name="value">An opaque value, ignored.</param>
    /// <returns>Always one.</returns>
    public static double IdentityFromObject(object value) => 1.0;

    /// <summary>Returns the product of two values for a custom-operator expression.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The product.</returns>
    public static double CustomMultiply(double left, double right) => left * right;

    /// <summary>Marker exception thrown by a <see cref="LoggingConstant"/>.</summary>
    private sealed class LoggingConstantException : Exception
    {
        /// <summary>Initializes the exception.</summary>
        /// <param name="message">The message identifying the throwing call.</param>
        public LoggingConstantException(string message) : base(message) { }
    }

    /// <summary>
    /// An opaque constant value whose <see cref="Equals(object?)"/> appends <c>self~other</c> to a shared log, then
    /// delegates to a behavior (reference equality by default).
    /// </summary>
    private sealed class LoggingConstant
    {
        /// <summary>The shared call log.</summary>
        private readonly List<string> _log;

        /// <summary>The comparison behavior, or <see langword="null"/> for reference equality.</summary>
        private readonly Func<LoggingConstant, object?, bool>? _behavior;

        /// <summary>Initializes the constant.</summary>
        /// <param name="id">The identifier written to the log.</param>
        /// <param name="log">The shared call log.</param>
        /// <param name="behavior">The comparison behavior, or <see langword="null"/> for reference equality.</param>
        public LoggingConstant(string id, List<string> log, Func<LoggingConstant, object?, bool>? behavior = null)
        {
            Id = id;
            _log = log;
            _behavior = behavior;
        }

        /// <summary>Gets the identifier written to the log.</summary>
        public string Id { get; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            _log.Add(Id + "~" + ((obj as LoggingConstant)?.Id ?? "?"));
            return _behavior is null ? ReferenceEquals(this, obj) : _behavior(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 17;
    }

    /// <summary>Builds the term <c>coefficient * carrier(value)</c>, with the inert carrier of <see cref="SafeOpaqueTerm"/>.</summary>
    /// <param name="coefficient">The numeric coefficient.</param>
    /// <param name="value">The opaque value.</param>
    /// <returns>The term.</returns>
    private static Expression OpaqueTerm(double coefficient, object value)
        => Expression.Multiply(Expression.Constant(coefficient), SafeOpaqueTerm.Carrier(value));

    /// <summary>Folds <paramref name="items"/> into a left-associated chain.</summary>
    /// <param name="items">The chain items.</param>
    /// <param name="operation">The binary factory.</param>
    /// <returns>The left-associated chain.</returns>
    private static Expression LeftChain(IReadOnlyList<Expression> items, Func<Expression, Expression, BinaryExpression> operation)
    {
        Expression accumulator = items[0];
        for (int i = 1; i < items.Count; i++) accumulator = operation(accumulator, items[i]);
        return accumulator;
    }

    /// <summary>
    /// Builds a chain whose every term is <c>c_i * carrier(constant_i)</c>, with coefficients 2, 3, ...;
    /// when <paramref name="firstSortsLast"/> is set, the first term's coefficient is 100, so the canonical order puts
    /// it last and every right sub-chain of the canonical form contains it.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="firstSortsLast">Whether the first term sorts last.</param>
    /// <param name="make">Creates the opaque value of term <c>i</c>.</param>
    /// <param name="operation">The chain operator.</param>
    /// <returns>The chain.</returns>
    private static Expression OpaqueChain(int n, bool firstSortsLast, Func<int, object> make, Func<Expression, Expression, BinaryExpression> operation)
        => LeftChain(Enumerable.Range(0, n).Select(i => OpaqueTerm(firstSortsLast && i == 0 ? 100 : i + 2, make(i))).ToArray(), operation);

    /// <summary>
    /// Builds the subtraction chain <c>100*Id(first) - 3*Id(second) - 4*x2 - 5*x3 - ...</c> (coefficient/parameter
    /// order alternating by parity), whose first term sorts last; with <paramref name="second"/> <see langword="null"/>
    /// the second term is <c>x1 * 3</c>.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="first">The opaque value of the first term.</param>
    /// <param name="second">The opaque value of the second term, or <see langword="null"/>.</param>
    /// <returns>The chain and its free parameters.</returns>
    private static (Expression Chain, ParameterExpression[] Parameters) MixedChain(int n, object first, object? second)
    {
        ParameterExpression[] xs = Enumerable.Range(0, n).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression[] terms = xs.Select((x, i) => i % 2 == 0
            ? (Expression)Expression.Multiply(Expression.Constant((double)(i + 2)), x)
            : Expression.Multiply(x, Expression.Constant((double)(i + 2)))).ToArray();
        terms[0] = OpaqueTerm(100, first);
        if (second is not null) terms[1] = OpaqueTerm(3, second);
        return (LeftChain(terms, Expression.Subtract), xs);
    }

    /// <summary>Simplifies with a fresh simplifier.</summary>
    /// <param name="expression">The expression to simplify.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Runs <paramref name="action"/> and returns the innermost exception it throws.</summary>
    /// <param name="action">The action expected to throw.</param>
    /// <returns>The innermost exception, unwrapping reflective rule invocation layers.</returns>
    private static Exception Innermost(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            while (exception.InnerException is not null) exception = exception.InnerException;
            return exception;
        }

        Assert.Fail("The action was expected to throw.");
        return null!;
    }

    /// <summary>Computes a deterministic FNV-1a digest of a call log, for pinning long logs compactly.</summary>
    /// <param name="log">The call log.</param>
    /// <returns>The digest.</returns>
    private static ulong Digest(IEnumerable<string> log)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in string.Join(" ", log))
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash;
    }

    /// <summary>The exact sequence of user calls of a short chain is pinned call by call.</summary>
    [TestMethod]
    public void AllOpaqueSubtraction_PinsEveryUserCallInOrder()
    {
        var log = new List<string>();
        Expression result = Simplify(OpaqueChain(4, false, i => new LoggingConstant("d" + i, log), Expression.Subtract));

        Assert.AreEqual("d0~d1 d0~d1 d0~d1 d1~d2 d1~d2 d1~d2 d1~d2 d1~d2 d1~d2", string.Join(" ", log));
        Assert.AreEqual(
            "((2 * Truncate(((value(UtilsTest.Mathematics.Expressions.ExpressionSimplifierOpaqueConstantInvariantTests+LoggingConstant) As Nullable`1) ?? 1))) + ((0 - (3 * Truncate(((value(UtilsTest.Mathematics.Expressions.ExpressionSimplifierOpaqueConstantInvariantTests+LoggingConstant) As Nullable`1) ?? 1)))) + ((0 - (4 * Truncate(((value(UtilsTest.Mathematics.Expressions.ExpressionSimplifierOpaqueConstantInvariantTests+LoggingConstant) As Nullable`1) ?? 1)))) + (0 - (5 * Truncate(((value(UtilsTest.Mathematics.Expressions.ExpressionSimplifierOpaqueConstantInvariantTests+LoggingConstant) As Nullable`1) ?? 1)))))))",
            result.ToString());
    }

    /// <summary>
    /// Longer chains, including those whose first term sorts last (the pathological P15 shape), keep the number and
    /// the exact sequence of user calls.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="firstSortsLast">Whether the first term sorts last.</param>
    /// <param name="subtraction">Whether the chain subtracts (otherwise it adds).</param>
    /// <param name="expectedCalls">The baseline number of user calls.</param>
    [TestMethod]
    [DataRow(6, false, true, 111)]
    [DataRow(8, false, true, 1077)]
    [DataRow(6, true, true, 313)]
    [DataRow(8, true, true, 7813)]
    [DataRow(8, false, false, 729)]
    public void AllOpaqueChains_KeepUserCallCountAndOrder(int n, bool firstSortsLast, bool subtraction, int expectedCalls)
    {
        var log = new List<string>();
        Expression first = Simplify(OpaqueChain(n, firstSortsLast, i => new LoggingConstant("d" + i, log), subtraction ? Expression.Subtract : Expression.Add));
        List<string> firstLog = [.. log];

        log.Clear();
        Expression second = Simplify(OpaqueChain(n, firstSortsLast, i => new LoggingConstant("d" + i, log), subtraction ? Expression.Subtract : Expression.Add));

        Assert.AreEqual(expectedCalls, firstLog.Count);
        CollectionAssert.AreEqual(firstLog, log, "Two independent simplifications must call user code identically.");
        Assert.AreEqual(first.ToString(), second.ToString());
    }

    /// <summary>A stateful <see cref="object.Equals(object?)"/> (true on every m-th call) keeps its baseline calls and results.</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="firstSortsLast">Whether the first term sorts last.</param>
    /// <param name="subtraction">Whether the chain subtracts (otherwise it adds).</param>
    /// <param name="modulo">The call period of a <see langword="true"/> answer.</param>
    /// <param name="expectedCalls">The baseline number of user calls.</param>
    /// <param name="expectedDigest">The baseline digest of the call log.</param>
    [TestMethod]
    [DataRow(8, true, true, 2, 127, 960459701759300888UL)]
    [DataRow(8, false, false, 3, 729, 1988502928281474364UL)]
    public void StatefulEquals_KeepsBaselineCallsAndOrder(int n, bool firstSortsLast, bool subtraction, int modulo, int expectedCalls, ulong expectedDigest)
    {
        var log = new List<string>();
        int calls = 0;
        Simplify(OpaqueChain(n, firstSortsLast, i => new LoggingConstant("s" + i, log, (_, _) => ++calls % modulo == 0), subtraction ? Expression.Subtract : Expression.Add));

        Assert.AreEqual(expectedCalls, log.Count);
        Assert.AreEqual(expectedDigest, Digest(log));
    }

    /// <summary>An <see cref="object.Equals(object?)"/> throwing on its k-th call throws on exactly the same call.</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="firstSortsLast">Whether the first term sorts last.</param>
    /// <param name="throwAt">The throwing call.</param>
    /// <param name="expectedMessage">The baseline message: <c>self~other#call</c>.</param>
    [TestMethod]
    [DataRow(6, false, 17, "t2~t3#17")]
    [DataRow(8, true, 40, "t0~t3#40")]
    [DataRow(8, true, 1, "t0~t1#1")]
    public void ThrowingEquals_ThrowsOnTheSameCall(int n, bool firstSortsLast, int throwAt, string expectedMessage)
    {
        var log = new List<string>();
        int calls = 0;
        Expression source = OpaqueChain(n, firstSortsLast, i => new LoggingConstant("t" + i, log, (self, other) =>
            ++calls == throwAt ? throw new LoggingConstantException($"{self.Id}~{(other as LoggingConstant)?.Id}#{calls}") : false), Expression.Subtract);

        Exception exception = Innermost(() => Simplify(source));

        Assert.IsInstanceOfType(exception, typeof(LoggingConstantException));
        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.AreEqual(throwAt, log.Count);
    }

    /// <summary>
    /// The long mixed chain (opaque first term sorted last, second opaque term) reaches the comparison-memo session and
    /// still calls user code exactly three times; a throwing second call propagates.
    /// </summary>
    [TestMethod]
    public void MixedChain_StatefulAndThrowingEquals_KeepBaselineCalls()
    {
        var log = new List<string>();
        int calls = 0;
        (Expression stateful, _) = MixedChain(10, new LoggingConstant("m0", log, (_, _) => ++calls % 2 == 0), new LoggingConstant("m1", log, (_, _) => ++calls % 2 == 0));
        Simplify(stateful);
        CollectionAssert.AreEqual(new[] { "m0~m1", "m0~m1", "m0~m1" }, log);

        log.Clear();
        calls = 0;
        (Expression throwing, _) = MixedChain(10,
            new LoggingConstant("m0", log, (self, _) => ++calls == 2 ? throw new LoggingConstantException(self.Id + "#" + calls) : false),
            new LoggingConstant("m1", log, (self, _) => ++calls == 2 ? throw new LoggingConstantException(self.Id + "#" + calls) : false));
        Exception exception = Innermost(() => Simplify(throwing));

        Assert.AreEqual("m0#2", exception.Message);
        CollectionAssert.AreEqual(new[] { "m0~m1", "m0~m1" }, log);
    }

    /// <summary>A comparer call made from user code (re-entrant comparison) is made exactly as often as on the baseline.</summary>
    [TestMethod]
    public void ReentrantComparerFromUserEquals_KeepsBaselineCalls()
    {
        var log = new List<string>();
        int innerComparisons = 0;
        (Expression innerLeft, _) = MixedChain(4, new object(), null);
        (Expression innerRight, _) = MixedChain(4, new object(), null);
        Expression source = OpaqueChain(7, true, i => new LoggingConstant("rc" + i, log, (_, _) =>
        {
            if (ExpressionComparer.Default.Equals(innerLeft, innerRight)) Assert.Fail("Distinct opaque constants must not compare equal.");
            innerComparisons++;
            return false;
        }), Expression.Subtract);

        Simplify(source);

        Assert.AreEqual(1563, log.Count);
        Assert.AreEqual(1563, innerComparisons);
    }

    /// <summary>A simplification started from user code, itself reaching user code, keeps its baseline calls.</summary>
    [TestMethod]
    public void ReentrantSimplifierFromUserEquals_KeepsBaselineCalls()
    {
        var log = new List<string>();
        int nested = 0;
        Expression source = OpaqueChain(6, false, i => new LoggingConstant("rs" + i, log, (self, _) =>
        {
            Expression inner = Simplify(OpaqueChain(3, false, j => new LoggingConstant(self.Id + "n" + j, log), Expression.Subtract));
            Assert.IsNotNull(inner);
            nested++;
            return false;
        }), Expression.Subtract);

        Simplify(source);

        Assert.AreEqual(111, nested);
        Assert.AreEqual(444, log.Count);
    }

    /// <summary>An always-true <see cref="object.Equals(object?)"/> lets factoring merge every term, with the baseline calls.</summary>
    [TestMethod]
    public void AlwaysEqualConstants_MergeWithBaselineCalls()
    {
        var log = new List<string>();
        Expression result = Simplify(OpaqueChain(6, true, i => new LoggingConstant("q" + i, log, (_, _) => true), Expression.Subtract));

        CollectionAssert.AreEqual(new[] { "q0~q1", "q0~q2", "q0~q3", "q0~q4", "q0~q5" }, log);
        Assert.IsInstanceOfType(result, typeof(BinaryExpression));
        var product = (BinaryExpression)result;
        Assert.AreEqual(ExpressionType.Multiply, product.NodeType);
        Assert.AreEqual(75.0, ((ConstantExpression)product.Left).Value);
    }

    /// <summary>The public comparer, called directly on two rebuilt opaque chains, keeps its baseline calls and answer.</summary>
    [TestMethod]
    public void PublicComparer_OnRebuiltOpaqueChains_KeepsBaselineCalls()
    {
        var log = new List<string>();
        bool distinct = ExpressionComparer.Default.Equals(
            OpaqueChain(7, true, i => new LoggingConstant("a" + i, log), Expression.Subtract),
            OpaqueChain(7, true, i => new LoggingConstant("b" + i, log), Expression.Subtract));
        int distinctCalls = log.Count;

        log.Clear();
        var values = Enumerable.Range(0, 7).Select(i => new LoggingConstant("c" + i, log)).ToArray();
        bool same = ExpressionComparer.Default.Equals(
            OpaqueChain(7, true, i => values[i], Expression.Subtract),
            OpaqueChain(7, true, i => values[i], Expression.Subtract));

        Assert.IsFalse(distinct, "Distinct reference-equality constants never compare equal.");
        Assert.AreEqual(3127, distinctCalls);
        Assert.IsTrue(same, "The same constants on both sides compare equal.");
        Assert.AreEqual(3133, log.Count);
    }

    /// <summary>
    /// An opaque constant that is never compared (a single opaque term) runs no user code, and its chain simplifies
    /// to the same tree as the same chain over a known-safe constant; free and bound-lambda forms agree.
    /// </summary>
    [TestMethod]
    public void UncomparedOpaqueTerm_RunsNoUserCodeAndMatchesSafeControl()
    {
        var log = new List<string>();
        var opaque = new LoggingConstant("z", log);
        (Expression opaqueChain, ParameterExpression[] parameters) = MixedChain(8, opaque, null);
        (Expression safeChain, ParameterExpression[] safeParameters) = MixedChain(8, "safe", null);

        Expression opaqueResult = Simplify(opaqueChain);
        Expression safeResult = Simplify(safeChain);
        var lambda = (LambdaExpression)Simplify(Expression.Lambda(opaqueChain, parameters));

        Assert.AreEqual(0, log.Count);
        Assert.AreEqual(
            safeResult.ToString().Replace("\"safe\"", "value(UtilsTest.Mathematics.Expressions.ExpressionSimplifierOpaqueConstantInvariantTests+LoggingConstant)"),
            opaqueResult.ToString());
        Assert.AreEqual(opaqueResult.ToString(), lambda.Body.ToString());
        CollectionAssert.AreEqual(parameters, lambda.Parameters.ToArray());
        Assert.AreEqual(safeParameters.Length, parameters.Length);
    }

    /// <summary>
    /// The same opaque term instance at two positions, and two distinct terms over one opaque value, keep their
    /// baseline calls.
    /// </summary>
    [TestMethod]
    public void SharedAndTwinOpaqueTerms_KeepBaselineCalls()
    {
        var log = new List<string>();
        var shared = new LoggingConstant("sh", log);
        Expression term = OpaqueTerm(5, shared);
        ParameterExpression[] xs = Enumerable.Range(0, 6).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression[] terms = xs.Select((x, i) => (Expression)Expression.Multiply(Expression.Constant((double)(i + 2)), x)).ToArray();
        terms[0] = term;
        terms[^1] = term;
        Expression sharedResult = Simplify(LeftChain(terms, Expression.Subtract));
        int sharedCalls = log.Count;

        log.Clear();
        terms[0] = OpaqueTerm(5, shared);
        terms[^1] = OpaqueTerm(5, shared);
        Expression twinResult = Simplify(LeftChain(terms, Expression.Add));

        Assert.AreEqual(0, sharedCalls);
        Assert.AreEqual(0, log.Count);
        Assert.AreEqual(sharedResult.ToString(), Simplify(LeftChain([term, .. terms[1..^1], term], Expression.Subtract)).ToString());
        Assert.IsNotNull(twinResult);
    }

    /// <summary>
    /// An extension node whose virtual <see cref="Expression.NodeType"/> and <see cref="Expression.Type"/> getters are
    /// user code: every read is logged, the <see cref="Expression.NodeType"/> getter can throw on its k-th read or run a
    /// callback (PR #667 review P2: the factor probe must not skip a simplification that reads these getters).
    /// </summary>
    private sealed class LoggingExtension : Expression
    {
        /// <summary>The identifier written to the log.</summary>
        private readonly string _id;

        /// <summary>The shared read log.</summary>
        private readonly List<string> _log;

        /// <summary>The <see cref="NodeType"/> read that throws, or 0.</summary>
        private readonly int _throwAt;

        /// <summary>A callback run on every <see cref="NodeType"/> read, or <see langword="null"/>.</summary>
        private readonly Action? _onNodeType;

        /// <summary>The number of <see cref="NodeType"/> reads.</summary>
        private int _nodeTypeReads;

        /// <summary>Initializes the node.</summary>
        /// <param name="id">The identifier written to the log.</param>
        /// <param name="log">The shared read log.</param>
        /// <param name="throwAt">The <see cref="NodeType"/> read that throws, or 0.</param>
        /// <param name="onNodeType">A callback run on every <see cref="NodeType"/> read, or <see langword="null"/>.</param>
        public LoggingExtension(string id, List<string> log, int throwAt = 0, Action? onNodeType = null)
        {
            _id = id;
            _log = log;
            _throwAt = throwAt;
            _onNodeType = onNodeType;
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType
        {
            get
            {
                _nodeTypeReads++;
                _log.Add("N:" + _id);
                _onNodeType?.Invoke();
                if (_nodeTypeReads == _throwAt) throw new LoggingConstantException("N:" + _id + "#" + _nodeTypeReads);
                return ExpressionType.Extension;
            }
        }

        /// <inheritdoc/>
        public override Type Type
        {
            get
            {
                _log.Add("T:" + _id);
                return typeof(double);
            }
        }
    }

    /// <summary>
    /// Builds <c>c * ext - x1*3 - 4*x2 - ...</c> (c = 100 when the extension term sorts last, else 2), optionally with
    /// <c>5 * extra</c> as the last term. The log is cleared after construction, so it only records simplification.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="sortedLast">Whether the extension term sorts last.</param>
    /// <param name="extension">The extension node of the first term.</param>
    /// <param name="extra">The extension node of the last term, or <see langword="null"/>.</param>
    /// <param name="log">The shared log, cleared after construction.</param>
    /// <returns>The chain.</returns>
    private static Expression ExtensionChain(int n, bool sortedLast, Expression extension, Expression? extra, List<string> log)
    {
        ParameterExpression[] xs = Enumerable.Range(0, n).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression[] terms = xs.Select((x, i) => i % 2 == 0
            ? (Expression)Expression.Multiply(Expression.Constant((double)(i + 2)), x)
            : Expression.Multiply(x, Expression.Constant((double)(i + 2)))).ToArray();
        terms[0] = Expression.Multiply(Expression.Constant(sortedLast ? 100.0 : 2.0), extension);
        if (extra is not null) terms[^1] = Expression.Multiply(Expression.Constant(5.0), extra);
        Expression chain = LeftChain(terms, Expression.Subtract);
        log.Clear();
        return chain;
    }

    /// <summary>
    /// Every read of an extension node's virtual getters is pinned. Before ETC R1b the extension term was compared,
    /// grouped and reordered like an ordinary term, which read the getters <paramref name="baselineReads"/> times (the
    /// pre-P15 baseline); since R1b (C-06) an extension node is unknown code that is never rewritten, so only the reads made
    /// to rebuild and finalize the term remain: the count no longer grows with the chain and never exceeds the former one.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="sortedLast">Whether the extension term sorts last.</param>
    /// <param name="expectedReads">The number of getter reads.</param>
    /// <param name="expectedDigest">The digest of the read log.</param>
    /// <param name="baselineReads">The number of reads before ETC R1b, an upper bound.</param>
    [TestMethod]
    [DataRow(4, false, 6, 10464847950183532971UL, 158)]
    [DataRow(4, true, 6, 10464847950183532971UL, 494)]
    [DataRow(6, true, 6, 10464847950183532971UL, 12346)]
    [DataRow(8, true, 6, 10464847950183532971UL, 308598)]
    public void ExtensionNodeGetters_AreReadAsOnTheBaseline(int n, bool sortedLast, int expectedReads, ulong expectedDigest, int baselineReads)
    {
        var log = new List<string>();
        Simplify(ExtensionChain(n, sortedLast, new LoggingExtension("e", log), null, log));

        Assert.IsTrue(log.Count <= baselineReads, $"{log.Count} reads, more than the {baselineReads} of the baseline.");
        Assert.AreEqual(expectedReads, log.Count, string.Join(" ", log));
        Assert.AreEqual(expectedDigest, Digest(log));
    }

    /// <summary>
    /// A <see cref="Expression.NodeType"/> getter throwing on its k-th read throws on exactly that read, and the exception
    /// propagates unchanged. Since ETC R1b the extension term is no longer compared (see
    /// <see cref="ExtensionNodeGetters_AreReadAsOnTheBaseline"/>), so the throwing reads are chosen among the reads that
    /// remain (the former rows threw on reads 12 and 30, which no longer happen). The first <see cref="Expression.NodeType"/>
    /// read is made by the factory that builds the chain, before the log is cleared, so the reads made by the
    /// simplification are the second and later ones.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="throwAt">The throwing read.</param>
    /// <param name="expectedReads">The number of logged reads when it throws.</param>
    [TestMethod]
    [DataRow(4, 2, 1)]
    [DataRow(6, 4, 3)]
    [DataRow(8, 5, 5)]
    public void ThrowingExtensionGetter_ThrowsOnTheSameRead(int n, int throwAt, int expectedReads)
    {
        var log = new List<string>();
        Expression source = ExtensionChain(n, true, new LoggingExtension("e", log, throwAt), null, log);

        Exception exception = Innermost(() => Simplify(source));

        Assert.IsInstanceOfType(exception, typeof(LoggingConstantException));
        Assert.AreEqual("N:e#" + throwAt, exception.Message);
        Assert.AreEqual(expectedReads, log.Count);
    }

    /// <summary>
    /// A re-entrant extension getter (it calls the public comparer), one extension instance shared by two terms, and
    /// two structurally similar but distinct instances, keep their reads pinned. Since ETC R1b the extension terms are never
    /// compared, grouped or reordered, so the counts are those of the rebuild and finalization only, bounded by the former
    /// baseline values (12346, 9143, 12364, 12364).
    /// </summary>
    [TestMethod]
    public void ReentrantSharedAndTwinExtensions_KeepBaselineReads()
    {
        var log = new List<string>();
        int inner = 0;
        (Expression innerLeft, _) = MixedChain(4, new object(), null);
        (Expression innerRight, _) = MixedChain(4, new object(), null);
        var reentrant = new LoggingExtension("r", log, onNodeType: () =>
        {
            if (ExpressionComparer.Default.Equals(innerLeft, innerRight)) Assert.Fail("Distinct opaque constants must not compare equal.");
            inner++;
        });
        Simplify(ExtensionChain(6, true, reentrant, null, log));
        int reentrantReads = log.Count;

        var shared = new LoggingExtension("s", log);
        Simplify(ExtensionChain(6, true, shared, shared, log));
        int sharedReads = log.Count;

        Simplify(ExtensionChain(6, true, new LoggingExtension("t0", log), new LoggingExtension("t1", log), log));
        int twinReads = log.Count;

        Assert.AreEqual(6, reentrantReads, "reentrant");
        Assert.AreEqual(5, inner, "inner");
        Assert.AreEqual(12, sharedReads, "shared");
        Assert.AreEqual(12, twinReads, "twin");
    }

    /// <summary>
    /// A custom operator and an unsupported node next to an opaque term keep their baseline trees. Since ETC R1b (C-06) the
    /// custom operator is user code: it and every term reading a parameter keep their source order, and only the inert
    /// opaque term (constants only) is moved before them. The conditional chain has no such term and keeps the baseline
    /// tree (re-measured on <c>master</c> with the carrier of <see cref="SafeOpaqueTerm"/>).
    /// </summary>
    [TestMethod]
    public void CustomOperatorAndUnsupportedNode_KeepBaselineTrees()
    {
        ParameterExpression[] xs = Enumerable.Range(0, 6).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        Expression[] terms = xs.Select((x, i) => (Expression)Expression.Multiply(Expression.Constant((double)(i + 2)), x)).ToArray();
        terms[1] = Expression.Multiply(Expression.Constant(3.0), xs[1], typeof(ExpressionSimplifierOpaqueConstantInvariantTests).GetMethod(nameof(CustomMultiply)));
        terms[^1] = OpaqueTerm(100, new object());
        Expression custom = Simplify(LeftChain(terms, Expression.Subtract));

        terms[1] = Expression.Condition(Expression.GreaterThan(xs[1], Expression.Constant(0.0)), xs[1], Expression.Constant(2.0));
        Expression conditional = Simplify(LeftChain(terms, Expression.Subtract));

        Assert.AreEqual("((0 - (100 * Truncate(((value(System.Object) As Nullable`1) ?? 1)))) + ((2 * x0) + ((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + (0 - (6 * x4)))))))", custom.ToString());
        Assert.AreEqual("((2 * x0) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + ((0 - (100 * Truncate(((value(System.Object) As Nullable`1) ?? 1)))) + (0 - IIF((x1 > 0), x1, 2)))))))", conditional.ToString());
    }
}
