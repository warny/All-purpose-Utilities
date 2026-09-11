using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization coverage for <c>ExpressionSimplifier.Math.TransformCall</c>, the private helper
/// shared by every <c>double</c> static-method conversion rule (<c>Sqrt</c>, <c>Max</c>, <c>Clamp</c>,
/// and about twenty others). #583 removed this helper's original LINQ scaffolding
/// (<c>Enumerable.Repeat(...).ToArray()</c> / <c>Select(...).ToArray()</c>); #586 then cached the
/// all-<c>double</c> <c>Type[]</c> lookup signature for the three arities every shipped conversion rule
/// actually uses (1-3) — <see cref="Type.GetMethod(string, BindingFlags, Type[])"/> itself still runs on
/// every call; method-resolution results are never cached (a full <c>(functionName, arity) -&gt; MethodInfo</c>
/// cache was tried and measured to reproducibly regress end-to-end CPU time — see
/// <c>ExpressionSimplifier.Math.cs</c>'s remarks on <c>ResolveTransformCallSignature</c>). These tests
/// characterize the helper's observable behavior (argument order, argument-instance reuse for
/// already-<c>double</c> expressions, non-mutation of the caller's array, exact validation and exception
/// ordering, and — critically — that the same rule can resolve different overloads depending on how many
/// arguments it is called with, since <see cref="ExpressionCallSignatureAttribute"/> matches only the
/// declaring type and method name, not the full parameter signature) so that the signature-array cache
/// can be verified not to alter any of it.
/// </summary>
[TestClass]
public class ExpressionSimplifierMathTransformCallTests
{
    /// <summary>
    /// Exposes a handful of <see cref="ExpressionSimplifier"/>'s protected <c>double</c>-conversion
    /// rules as public wrappers, without changing their accessibility on the production type. Covers
    /// one rule of each arity actually used by <c>TransformCall</c>: <c>Sqrt</c> (1 argument),
    /// <c>Max</c> (2 arguments), and <c>Clamp</c> (3 arguments); plus <c>Abs</c>, <c>Log</c>, and
    /// <c>Round</c> for the resolution-cache characterization (same-name/different-arity overloads,
    /// and the deliberate non-reuse of a source call's own <see cref="MethodInfo"/>).
    /// </summary>
    private sealed class ExposedMathSimplifier : ExpressionSimplifier
    {
        /// <summary>Publicly exposes <c>SqrtConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">The single argument expression.</param>
        /// <returns>Whatever <c>SqrtConversionMath</c> returns.</returns>
        public Expression Sqrt(Expression e, Expression[] expressions) => SqrtConversionMath(e, expressions);

        /// <summary>Publicly exposes <c>MaxConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">The two argument expressions.</param>
        /// <returns>Whatever <c>MaxConversionMath</c> returns.</returns>
        public Expression Max(Expression e, Expression[] expressions) => MaxConversionMath(e, expressions);

        /// <summary>Publicly exposes <c>ClampConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">The three argument expressions.</param>
        /// <returns>Whatever <c>ClampConversionMath</c> returns.</returns>
        public Expression Clamp(Expression e, Expression[] expressions) => ClampConversionMath(e, expressions);

        /// <summary>Publicly exposes <c>AbsConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">The single argument expression.</param>
        /// <returns>Whatever <c>AbsConversionMath</c> returns.</returns>
        public Expression Abs(Expression e, Expression[] expressions) => AbsConversionMath(e, expressions);

        /// <summary>Publicly exposes <c>LogConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">One or two argument expressions (<c>double.Log(double)</c> or <c>double.Log(double,double)</c>).</param>
        /// <returns>Whatever <c>LogConversionMath</c> returns.</returns>
        public Expression Log(Expression e, Expression[] expressions) => LogConversionMath(e, expressions);

        /// <summary>Publicly exposes <c>RoundConversionMath</c>.</summary>
        /// <param name="e">The original expression node passed through to the protected rule.</param>
        /// <param name="expressions">The argument expressions.</param>
        /// <returns>Whatever <c>RoundConversionMath</c> returns.</returns>
        public Expression Round(Expression e, Expression[] expressions) => RoundConversionMath(e, expressions);
    }

    /// <summary>A placeholder "original expression" argument, since <c>TransformCall</c> only null-checks it.</summary>
    private static Expression DummySource => Expression.Constant(0.0);

    // ------------------------------------------------------------------------------------------
    // A/B/C: argument count, order, and method resolution per arity.
    // ------------------------------------------------------------------------------------------

    /// <summary>A single-argument rule (<c>Sqrt</c>) resolves <see cref="double.Sqrt(double)"/> and preserves the argument.</summary>
    [TestMethod]
    public void TransformCall_UnaryRule_ResolvesMethodAndPreservesArgument()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo expected = typeof(double).GetMethod(
            nameof(double.Sqrt), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        Expression result = simplifier.Sqrt(DummySource, [x]);

        Assert.IsInstanceOfType(result, typeof(MethodCallExpression));
        var call = (MethodCallExpression)result;
        Assert.AreSame(expected, call.Method);
        Assert.AreEqual(1, call.Arguments.Count);
        Assert.AreSame(x, call.Arguments[0]);
        Assert.AreEqual(typeof(double), call.Type);

        var compiled = Expression.Lambda<Func<double, double>>(call, x).Compile();
        Assert.AreEqual(3.0, compiled(9.0));
    }

    /// <summary>A two-argument rule (<c>Max</c>) resolves <see cref="double.Max(double, double)"/> and preserves argument order.</summary>
    [TestMethod]
    public void TransformCall_BinaryRule_ResolvesMethodAndPreservesArgumentOrder()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        MethodInfo expected = typeof(double).GetMethod(
            nameof(double.Max), BindingFlags.Public | BindingFlags.Static, [typeof(double), typeof(double)])!;

        Expression result = simplifier.Max(DummySource, [a, b]);

        var call = (MethodCallExpression)result;
        Assert.AreSame(expected, call.Method);
        Assert.AreEqual(2, call.Arguments.Count);
        Assert.AreSame(a, call.Arguments[0]);
        Assert.AreSame(b, call.Arguments[1]);

        var compiled = Expression.Lambda<Func<double, double, double>>(call, a, b).Compile();
        Assert.AreEqual(5.0, compiled(3.0, 5.0));
        Assert.AreEqual(5.0, compiled(5.0, 3.0));
    }

    /// <summary>A three-argument rule (<c>Clamp</c>) resolves <see cref="double.Clamp(double, double, double)"/> and preserves argument order.</summary>
    [TestMethod]
    public void TransformCall_TernaryRule_ResolvesMethodAndPreservesArgumentOrder()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression value = Expression.Parameter(typeof(double), "value");
        ParameterExpression min = Expression.Parameter(typeof(double), "min");
        ParameterExpression max = Expression.Parameter(typeof(double), "max");
        MethodInfo expected = typeof(double).GetMethod(
            nameof(double.Clamp), BindingFlags.Public | BindingFlags.Static,
            [typeof(double), typeof(double), typeof(double)])!;

        Expression result = simplifier.Clamp(DummySource, [value, min, max]);

        var call = (MethodCallExpression)result;
        Assert.AreSame(expected, call.Method);
        Assert.AreEqual(3, call.Arguments.Count);
        Assert.AreSame(value, call.Arguments[0]);
        Assert.AreSame(min, call.Arguments[1]);
        Assert.AreSame(max, call.Arguments[2]);

        var compiled = Expression.Lambda<Func<double, double, double, double>>(call, value, min, max).Compile();
        Assert.AreEqual(5.0, compiled(5.0, 0.0, 10.0));
        Assert.AreEqual(0.0, compiled(-5.0, 0.0, 10.0));
        Assert.AreEqual(10.0, compiled(15.0, 0.0, 10.0));
    }

    // ------------------------------------------------------------------------------------------
    // D: non-double arguments are wrapped in a Convert node.
    // ------------------------------------------------------------------------------------------

    /// <summary>Arguments whose type is not already <see cref="double"/> are wrapped in an <see cref="ExpressionType.Convert"/> node.</summary>
    [TestMethod]
    public void TransformCall_NonDoubleArguments_AreWrappedInConvertNodes()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression f = Expression.Parameter(typeof(float), "f");
        ParameterExpression i = Expression.Parameter(typeof(int), "i");

        Expression result = simplifier.Max(DummySource, [f, i]);

        var call = (MethodCallExpression)result;
        Assert.IsInstanceOfType(call.Arguments[0], typeof(UnaryExpression));
        var converted0 = (UnaryExpression)call.Arguments[0];
        Assert.AreEqual(ExpressionType.Convert, converted0.NodeType);
        Assert.AreEqual(typeof(double), converted0.Type);
        Assert.AreSame(f, converted0.Operand);

        Assert.IsInstanceOfType(call.Arguments[1], typeof(UnaryExpression));
        var converted1 = (UnaryExpression)call.Arguments[1];
        Assert.AreEqual(ExpressionType.Convert, converted1.NodeType);
        Assert.AreEqual(typeof(double), converted1.Type);
        Assert.AreSame(i, converted1.Operand);
    }

    // ------------------------------------------------------------------------------------------
    // E: already-double arguments are reused by reference, not cloned.
    // ------------------------------------------------------------------------------------------

    /// <summary>Already-<see cref="double"/>-typed argument expressions are reused by reference, not cloned, in the result.</summary>
    [TestMethod]
    public void TransformCall_DoubleArguments_ArePreservedByReference()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        var call = (MethodCallExpression)simplifier.Max(DummySource, [a, b]);

        Assert.AreSame(a, call.Arguments[0]);
        Assert.AreSame(b, call.Arguments[1]);
    }

    // ------------------------------------------------------------------------------------------
    // F: the caller-supplied array is never mutated in place.
    // ------------------------------------------------------------------------------------------

    /// <summary>The caller-supplied <c>expressions</c> array is never mutated in place, even when a conversion is required.</summary>
    [TestMethod]
    public void TransformCall_DoesNotMutateInputArray()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression f = Expression.Parameter(typeof(float), "f");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        Expression[] input = [f, a];
        Expression[] snapshot = [.. input];

        simplifier.Max(DummySource, input);

        Assert.AreSame(snapshot[0], input[0]);
        Assert.AreSame(snapshot[1], input[1]);
    }

    // ------------------------------------------------------------------------------------------
    // G: the result does not alias the caller's array; mutating it afterward does not affect the result.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The returned <see cref="MethodCallExpression"/> does not share storage with the caller's
    /// <c>expressions</c> array: replacing an element in the caller's array after the call must not
    /// affect the already-returned result. This deliberately forbids a future optimization from
    /// reusing the caller's array as the <see cref="MethodCallExpression.Arguments"/> storage merely
    /// because every element already has type <see cref="double"/>.
    /// </summary>
    [TestMethod]
    public void TransformCall_ResultDoesNotAliasInputArray()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        Expression[] input = [a, b];

        var call = (MethodCallExpression)simplifier.Max(DummySource, input);

        input[0] = Expression.Parameter(typeof(double), "replaced");

        Assert.AreSame(a, call.Arguments[0]);
        Assert.AreSame(b, call.Arguments[1]);
    }

    // ------------------------------------------------------------------------------------------
    // H/I: null-argument validation.
    // ------------------------------------------------------------------------------------------

    /// <summary>A null <c>e</c> throws <see cref="ArgumentNullException"/> naming parameter <c>e</c>.</summary>
    [TestMethod]
    public void TransformCall_NullSourceExpression_ThrowsArgumentNullException()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => simplifier.Sqrt(null!, [x]));
        Assert.AreEqual("e", exception.ParamName);
    }

    /// <summary>A null <c>expressions</c> array throws <see cref="ArgumentNullException"/> naming parameter <c>expressions</c>.</summary>
    [TestMethod]
    public void TransformCall_NullExpressionsArray_ThrowsArgumentNullException()
    {
        var simplifier = new ExposedMathSimplifier();

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => simplifier.Sqrt(DummySource, null!));
        Assert.AreEqual("expressions", exception.ParamName);
    }

    // ------------------------------------------------------------------------------------------
    // J/K: wrong arity fails at method resolution, before any argument is inspected.
    // ------------------------------------------------------------------------------------------

    /// <summary>Calling a rule with the wrong argument count fails method resolution with <see cref="InvalidOperationException"/>.</summary>
    [TestMethod]
    public void TransformCall_WrongArity_ThrowsInvalidOperationException()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Sqrt(DummySource, [a, b]));
    }

    /// <summary>
    /// Wrong arity fails method resolution before any array element is dereferenced: a <c>null</c>
    /// element combined with a wrong argument count still throws <see cref="InvalidOperationException"/>,
    /// not a <see cref="NullReferenceException"/> from touching the invalid element.
    /// </summary>
    [TestMethod]
    public void TransformCall_WrongArityWithInvalidElement_FailsBeforeInspectingElements()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression a = Expression.Parameter(typeof(double), "a");

        Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Sqrt(DummySource, [a, null!]));
    }

    // ------------------------------------------------------------------------------------------
    // L: a null element with a valid arity reaches argument conversion and fails there.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A <c>null</c> element at a valid arity passes method resolution and fails while inspecting the
    /// element's <see cref="Expression.Type"/> during argument conversion, throwing
    /// <see cref="NullReferenceException"/>.
    /// </summary>
    [TestMethod]
    public void TransformCall_NullElementAtValidArity_ThrowsNullReferenceException()
    {
        var simplifier = new ExposedMathSimplifier();

        Assert.ThrowsExactly<NullReferenceException>(() => simplifier.Sqrt(DummySource, [null!]));
    }

    // ------------------------------------------------------------------------------------------
    // Signature-array cache characterization (#586): TransformCall never caches a resolved
    // MethodInfo, and never caches by function name alone — the cached all-double Type[] lookup
    // signature depends only on argument count (arity), shared across every function name at that
    // arity, and Type.GetMethod itself still runs on every call regardless of whether that array
    // was cached or freshly built.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Two different single-argument rules (<c>Sqrt</c> and <c>Abs</c>) must resolve to their own,
    /// distinct methods — not collapse onto each other under a cache keyed only by argument count.
    /// </summary>
    [TestMethod]
    public void TransformCall_DifferentNamesSameArity_RemainDistinct()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo sqrtMethod = typeof(double).GetMethod(
            nameof(double.Sqrt), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;
        MethodInfo absMethod = typeof(double).GetMethod(
            nameof(double.Abs), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        var sqrt1 = (MethodCallExpression)simplifier.Sqrt(DummySource, [x]);
        var abs1 = (MethodCallExpression)simplifier.Abs(DummySource, [x]);
        var sqrt2 = (MethodCallExpression)simplifier.Sqrt(DummySource, [x]);

        Assert.AreSame(sqrtMethod, sqrt1.Method);
        Assert.AreSame(absMethod, abs1.Method);
        Assert.AreSame(sqrtMethod, sqrt2.Method, "A repeated Sqrt call must still resolve double.Sqrt, not the Abs method seen in between.");
    }

    /// <summary>
    /// The same rule name (<c>Log</c>) called with a different argument count must resolve a different
    /// overload each time: <see cref="ExpressionCallSignatureAttribute"/> matches only the declaring
    /// type and method name, so the same protected rule can legitimately receive either
    /// <see cref="double.Log(double)"/> or <see cref="double.Log(double, double)"/>'s arguments. A cache
    /// keyed only by name — or one <see cref="MethodInfo"/> cached per protected rule — would be wrong.
    /// </summary>
    [TestMethod]
    public void TransformCall_SameNameDifferentArity_ResolvesDistinctOverloads()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        MethodInfo logUnary = typeof(double).GetMethod(
            nameof(double.Log), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;
        MethodInfo logBinary = typeof(double).GetMethod(
            nameof(double.Log), BindingFlags.Public | BindingFlags.Static, [typeof(double), typeof(double)])!;

        var call1 = (MethodCallExpression)simplifier.Log(DummySource, [x]);
        var call2 = (MethodCallExpression)simplifier.Log(DummySource, [x, y]);
        var call3 = (MethodCallExpression)simplifier.Log(DummySource, [x]);

        Assert.AreSame(logUnary, call1.Method);
        Assert.AreEqual(1, call1.Arguments.Count);
        Assert.AreSame(logBinary, call2.Method);
        Assert.AreEqual(2, call2.Arguments.Count);
        Assert.AreSame(logUnary, call3.Method, "The third Log call (arity 1) must resolve double.Log(double) again, not the arity-2 overload seen in between.");
        Assert.AreEqual(1, call3.Arguments.Count);
    }

    /// <summary>
    /// The two-argument <see cref="double.Log(double, double)"/> overload resolved through <c>Log</c>
    /// must compute the expected base-change semantics once compiled and executed.
    /// </summary>
    [TestMethod]
    public void TransformCall_TwoArgumentLog_ComputesExpectedSemantics()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression value = Expression.Parameter(typeof(double), "value");
        ParameterExpression newBase = Expression.Parameter(typeof(double), "newBase");

        var call = (MethodCallExpression)simplifier.Log(DummySource, [value, newBase]);
        var compiled = Expression.Lambda<Func<double, double, double>>(call, value, newBase).Compile();

        Assert.AreEqual(Math.Log(8.0, 2.0), compiled(8.0, 2.0), 1e-9);
    }

    /// <summary>
    /// <c>TransformCall</c> must never reuse the source <see cref="MethodCallExpression.Method"/>: it
    /// always resolves from <c>functionName</c> plus an all-<see cref="double"/> signature built from
    /// <c>expressions.Length</c>. A real <see cref="double.Round(double, int)"/> source call passed
    /// through <c>Round</c> with two arguments still fails to resolve, because the lookup signature is
    /// <c>(double, double)</c>, not <c>(double, int)</c> — <see cref="double.Round(double, double)"/>
    /// does not exist. If a future optimization shortcut ever read <c>((MethodCallExpression)e).Method</c>
    /// instead, this exact case would silently start succeeding with the wrong (source) method — a
    /// historical-behavior change this test forbids.
    /// </summary>
    [TestMethod]
    public void TransformCall_RoundDoubleInt_DoesNotReuseSourceMethodInfo_ThrowsInvalidOperationException()
    {
        var simplifier = new ExposedMathSimplifier();
        MethodInfo roundDoubleInt = typeof(double).GetMethod(
            nameof(double.Round), BindingFlags.Public | BindingFlags.Static, [typeof(double), typeof(int)])!;
        ParameterExpression value = Expression.Parameter(typeof(double), "value");
        ParameterExpression digits = Expression.Parameter(typeof(int), "digits");
        MethodCallExpression sourceCall = Expression.Call(roundDoubleInt, value, digits);

        Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Round(sourceCall, [value, digits]));
    }

    /// <summary>
    /// A failed resolution (wrong arity) must not affect a later, valid resolution of the same
    /// function name, and must still fail again afterward. <c>TransformCall</c> never caches a
    /// resolved <see cref="MethodInfo"/> — only the shared, arity-only lookup-signature array,
    /// which never depends on whether resolution actually succeeds for any particular function
    /// name — so a failing call can never "poison" (or benefit from) a subsequent one.
    /// </summary>
    [TestMethod]
    public void TransformCall_FailedArityDoesNotPoisonLaterValidLookup()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");

        Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Sqrt(DummySource, [x, y]));

        var call = (MethodCallExpression)simplifier.Sqrt(DummySource, [x]);
        MethodInfo expected = typeof(double).GetMethod(
            nameof(double.Sqrt), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;
        Assert.AreSame(expected, call.Method);

        Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Sqrt(DummySource, [x, y]));
    }

    /// <summary>
    /// An arity outside the three <c>TransformCall</c> caches (1-3) must keep failing with the
    /// historical <see cref="InvalidOperationException"/> across many distinct out-of-range arities.
    /// <c>TransformCall</c>'s protected callers accept an arbitrary <see cref="Expression"/>[], so an
    /// external subclass could otherwise call e.g. <c>SqrtConversionMath</c> with an unbounded set of
    /// distinct malformed argument counts; a cache keyed by arity alone (an earlier design considered
    /// for #586) would have retained one array per distinct out-of-range arity for the remaining
    /// lifetime of the process. The shipped design caches only the three known arities as fixed static
    /// fields — structurally incapable of growing — so every other arity is always built fresh and
    /// never retained.
    /// </summary>
    [TestMethod]
    public void TransformCall_ManyDistinctOutOfRangeArities_AlwaysThrowInvalidOperationException()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        for (int arity = 4; arity < 54; arity++)
        {
            Expression[] tooMany = new Expression[arity];
            System.Array.Fill(tooMany, x);
            Assert.ThrowsExactly<InvalidOperationException>(() => simplifier.Sqrt(DummySource, tooMany));
        }
    }

    /// <summary>
    /// After a given arity's lookup-signature array has already been resolved once (and cached, for
    /// the arities <c>TransformCall</c> caches), a later call with an invalid element at that same
    /// arity must still pass method resolution and fail while inspecting the element during argument
    /// conversion — not surface a different, cache-related exception.
    /// </summary>
    [TestMethod]
    public void TransformCall_ValidCachedKeyThenInvalidElement_ThrowsNullReferenceException()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");

        simplifier.Sqrt(DummySource, [x]);

        Assert.ThrowsExactly<NullReferenceException>(() => simplifier.Sqrt(DummySource, [null!]));
    }

    /// <summary>
    /// Concurrent callers resolving the same <c>(functionName, arity)</c> key must all observe the
    /// correct <see cref="MethodInfo"/>. Deterministic and timing-independent: no timing assertions, no
    /// sleeps, no dependency on whether any cache was already warm.
    /// </summary>
    [TestMethod]
    public void TransformCall_ConcurrentInvocations_AllResolveExpectedMethod()
    {
        var simplifier = new ExposedMathSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo expected = typeof(double).GetMethod(
            nameof(double.Abs), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

        System.Threading.Tasks.Parallel.For(0, 200, _ =>
        {
            var call = (MethodCallExpression)simplifier.Abs(DummySource, [x]);
            Assert.AreSame(expected, call.Method);
        });
    }
}
