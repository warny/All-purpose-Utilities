using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization coverage for the private static <c>ExpressionSimplifier.GetAdditiveGroupingKey</c>
/// helper. #589 replaces the <c>Arguments.Select(GetCanonicalExpressionKey)</c> adapter inside its two
/// <see cref="MethodCallExpression"/> branches with a direct generic <c>string.Join&lt;Expression&gt;</c>
/// call. These tests lock the exact baseline: the resulting key string for every argument-count shape,
/// the per-argument <see cref="Expression.ToString()"/> call count/order (including for custom
/// <see cref="Expression"/> subclasses whose <c>ToString()</c> logs, returns a value containing the "|"
/// separator, returns <see langword="null"/>, or throws), so the traversal rewrite can be verified not to
/// alter any observable behavior.
/// </summary>
[TestClass]
public class ExpressionSimplifierAdditiveGroupingKeyTests
{
    /// <summary>
    /// Reflection-resolved delegate wrapping the private static
    /// <c>ExpressionSimplifier.GetAdditiveGroupingKey(Expression)</c> method. Resolved once via
    /// <see cref="BindingFlags.NonPublic"/> | <see cref="BindingFlags.Static"/> so tests do not pay
    /// reflection-invoke overhead and do not require any change to production accessibility.
    /// </summary>
    private static readonly Func<Expression, string> GetAdditiveGroupingKey = CreateGetAdditiveGroupingKeyDelegate();

    private static readonly MethodInfo SinMethod =
        typeof(double).GetMethod(nameof(double.Sin), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

    private static readonly MethodInfo MaxMethod =
        typeof(double).GetMethod(nameof(double.Max), BindingFlags.Public | BindingFlags.Static, [typeof(double), typeof(double)])!;

    private static readonly MethodInfo ZeroArgMethod =
        typeof(ExpressionSimplifierAdditiveGroupingKeyTests).GetMethod(
            nameof(ZeroArgumentFunction), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo TwoArgMethod =
        typeof(ExpressionSimplifierAdditiveGroupingKeyTests).GetMethod(
            nameof(TwoArgumentFunction), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Resolves a delegate over the private static grouping-key helper without changing its accessibility.</summary>
    /// <returns>A callable delegate equivalent to invoking <c>GetAdditiveGroupingKey</c> directly.</returns>
    private static Func<Expression, string> CreateGetAdditiveGroupingKeyDelegate()
    {
        MethodInfo method = typeof(ExpressionSimplifier).GetMethod(
            "GetAdditiveGroupingKey",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(Expression)])!;
        return (Func<Expression, string>)Delegate.CreateDelegate(typeof(Func<Expression, string>), method);
    }

    /// <summary>Test-only static method with no parameters, used to characterize the zero-argument call shape.</summary>
    /// <returns>An arbitrary constant.</returns>
    private static double ZeroArgumentFunction() => 1d;

    /// <summary>Test-only static method with two parameters, used as a stable two-argument call shape.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>An arbitrary combination of the operands.</returns>
    private static double TwoArgumentFunction(double a, double b) => a + b;

    /// <summary>
    /// A test-only <see cref="Expression"/> subclass whose <see cref="ToString"/> is instrumented: it
    /// records into a shared log and returns a caller-supplied value (or throws a caller-supplied
    /// exception), letting tests observe exactly how many times, in what order, and with what arguments
    /// <c>GetAdditiveGroupingKey</c> invokes <see cref="Expression.ToString()"/> on its arguments.
    /// </summary>
    private sealed class LoggingExpression : Expression
    {
        private readonly List<string> _log;
        private readonly string _label;
        private readonly Func<string?>? _result;
        private readonly Exception? _throw;

        /// <summary>Initializes an instance that logs <paramref name="label"/> and returns <paramref name="result"/>.</summary>
        /// <param name="log">Shared list receiving one entry per <see cref="ToString"/> invocation.</param>
        /// <param name="label">Label recorded into <paramref name="log"/> when <see cref="ToString"/> runs.</param>
        /// <param name="result">Value returned by <see cref="ToString"/>; may be <see langword="null"/>.</param>
        public LoggingExpression(List<string> log, string label, string? result)
        {
            _log = log;
            _label = label;
            _result = () => result;
        }

        /// <summary>Initializes an instance that logs <paramref name="label"/> then throws <paramref name="throwOnToString"/>.</summary>
        /// <param name="log">Shared list receiving one entry per <see cref="ToString"/> invocation.</param>
        /// <param name="label">Label recorded into <paramref name="log"/> before throwing.</param>
        /// <param name="throwOnToString">Exception thrown by <see cref="ToString"/> after logging.</param>
        public LoggingExpression(List<string> log, string label, Exception throwOnToString)
        {
            _log = log;
            _label = label;
            _throw = throwOnToString;
        }

        /// <inheritdoc/>
        public override Type Type => typeof(double);

        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override bool CanReduce => false;

        /// <summary>
        /// Logs this instance's label and returns the configured result, or throws the configured exception.
        /// The null-forgiving operator only suppresses the compile-time nullability warning on the override;
        /// the configured result can still be <see langword="null"/> at runtime, which is exactly what
        /// characterization 8 exercises.
        /// </summary>
        /// <returns>The configured result string; may be <see langword="null"/> at runtime.</returns>
        public override string ToString()
        {
            _log.Add(_label);
            if (_throw is not null)
            {
                throw _throw;
            }

            return _result!()!;
        }
    }

    /// <summary>Marker exception used to verify that a <see cref="ToString"/> failure propagates unwrapped.</summary>
    private sealed class GroupingKeyProbeException : Exception
    {
    }

    /// <summary>Creates a double parameter expression named <c>x</c> for grouping-key characterization tests.</summary>
    /// <returns>A double parameter expression named <c>x</c>.</returns>
    private static ParameterExpression X() => Expression.Parameter(typeof(double), "x");

    // ------------------------------------------------------------------------------------------
    // Characterization 1 — ordinary expression branch (negative control for #589)
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 1: a plain parameter takes the fallback "expr:" branch, never touched by #589.</summary>
    [TestMethod]
    public void Parameter_UsesExprBranch_MatchesCanonicalToString()
    {
        ParameterExpression x = X();
        string key = GetAdditiveGroupingKey(x);
        Assert.AreEqual($"expr:{x}", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 2 — unary MethodCall
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 2: a single-argument function call produces a "func:" key with a one-element argument join.</summary>
    [TestMethod]
    public void UnaryMethodCall_Sin_MatchesBaselineKey()
    {
        MethodCallExpression call = (MethodCallExpression)Expression.Call(SinMethod, X());
        string key = GetAdditiveGroupingKey(call);
        Assert.AreEqual("func:x:0", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 3 — binary MethodCall (separator handling)
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 3: a two-argument function call joins both argument keys with "|", in declaration order.</summary>
    [TestMethod]
    public void BinaryMethodCall_Max_MatchesBaselineKeyWithSeparator()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        MethodCallExpression call = (MethodCallExpression)Expression.Call(MaxMethod, x, y);
        string key = GetAdditiveGroupingKey(call);
        Assert.AreEqual("func:x|y:3", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 4 — zero-argument MethodCall
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 4: a zero-argument function call produces an empty argument-list join.</summary>
    [TestMethod]
    public void ZeroArgumentMethodCall_MatchesBaselineKey()
    {
        MethodCallExpression call = (MethodCallExpression)Expression.Call(ZeroArgMethod);
        string key = GetAdditiveGroupingKey(call);
        Assert.AreEqual("func::3", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 5 — power-wrapped MethodCall
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Item 5: for <c>Power(MethodCall, exponent)</c>, the grouping key is derived from the left
    /// method call's own arguments and function family, never from the exponent.
    /// </summary>
    [TestMethod]
    public void PowerWrappedMethodCall_UsesLeftCallArguments_IgnoresExponent()
    {
        MethodCallExpression sin = (MethodCallExpression)Expression.Call(SinMethod, X());
        BinaryExpression power = Expression.Power(sin, Expression.Constant(3.0));
        string key = GetAdditiveGroupingKey(power);
        Assert.AreEqual("func:x:0", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 6 — custom Expression ToString invocation order
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Item 6: for a two-argument call, each argument's <see cref="Expression.ToString()"/> is invoked
    /// exactly once, left-to-right, before the key is assembled.
    /// </summary>
    [TestMethod]
    public void CustomExpressionArguments_ToStringInvokedOnceInOrder()
    {
        var log = new List<string>();
        var a = new LoggingExpression(log, "A", "a");
        var b = new LoggingExpression(log, "B", "b");
        MethodCallExpression call = (MethodCallExpression)Expression.Call(TwoArgMethod, a, b);

        string key = GetAdditiveGroupingKey(call);

        CollectionAssert.AreEqual(new[] { "A", "B" }, log);
        Assert.AreEqual("func:a|b:3", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 7 — ToString values containing the "|" separator
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 7: no escaping is applied; a "|" inside an argument's own key flows through unchanged.</summary>
    [TestMethod]
    public void CustomExpressionArguments_SeparatorCharacterIsNotEscaped()
    {
        var log = new List<string>();
        var a = new LoggingExpression(log, "A", "a|b");
        var b = new LoggingExpression(log, "B", "c");
        MethodCallExpression call = (MethodCallExpression)Expression.Call(TwoArgMethod, a, b);

        string key = GetAdditiveGroupingKey(call);

        Assert.AreEqual("func:a|b|c:3", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 8 — ToString returning null
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 8: an argument whose <see cref="Expression.ToString()"/> returns <see langword="null"/> joins as an empty segment.</summary>
    [TestMethod]
    public void CustomExpressionArguments_NullToString_JoinsAsEmptySegment()
    {
        var log = new List<string>();
        var a = new LoggingExpression(log, "A", (string?)null);
        var b = new LoggingExpression(log, "B", "b");
        MethodCallExpression call = (MethodCallExpression)Expression.Call(TwoArgMethod, a, b);

        string key = GetAdditiveGroupingKey(call);

        CollectionAssert.AreEqual(new[] { "A", "B" }, log);
        Assert.AreEqual("func:|b:3", key);
    }

    // ------------------------------------------------------------------------------------------
    // Characterization 9 — exception propagation / order
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Item 9: when an argument's <see cref="Expression.ToString()"/> throws, the exception propagates
    /// unwrapped, evaluation stops immediately (later arguments are never touched), and earlier
    /// arguments have already been evaluated exactly once.
    /// </summary>
    [TestMethod]
    public void CustomExpressionArguments_ToStringThrows_PropagatesAndStopsEvaluation()
    {
        var log = new List<string>();
        var probe = new GroupingKeyProbeException();
        var a = new LoggingExpression(log, "A", "a");
        var b = new LoggingExpression(log, "B", probe);
        var c = new LoggingExpression(log, "C", "c");
        MethodInfo threeArgMethod = typeof(ExpressionSimplifierAdditiveGroupingKeyTests).GetMethod(
            nameof(ThreeArgumentFunction), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodCallExpression call = (MethodCallExpression)Expression.Call(threeArgMethod, a, b, c);

        var thrown = Assert.ThrowsExactly<GroupingKeyProbeException>(() => GetAdditiveGroupingKey(call));

        Assert.AreSame(probe, thrown);
        CollectionAssert.AreEqual(new[] { "A", "B" }, log);
    }

    /// <summary>Test-only static method with three parameters, used only by the exception-propagation characterization.</summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <param name="c">Third operand.</param>
    /// <returns>An arbitrary combination of the operands.</returns>
    private static double ThreeArgumentFunction(double a, double b, double c) => a + b + c;

    // ------------------------------------------------------------------------------------------
    // End-to-end compatibility — focused additive canonicalization cases
    // ------------------------------------------------------------------------------------------

    /// <summary>End-to-end: two unary function terms canonicalize identically regardless of source order.</summary>
    [TestMethod]
    public void EndToEnd_SinPlusCos_CanonicalizesRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double>> source = x => double.Sin(x) + double.Cos(x);
        Expression<Func<double, double>> expected = x => double.Cos(x) + double.Sin(x);

        var simplified = simplifier.Simplify(source);

        Assert.AreEqual(expected, simplified, ExpressionComparer.Default);
    }

    /// <summary>End-to-end: a binary-argument function term (Max) participates correctly in additive canonicalization.</summary>
    [TestMethod]
    public void EndToEnd_MaxPlusMin_CanonicalizesRegardlessOfSourceOrder()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source = (x, y) => double.Min(x, y) + double.Max(x, y);
        Expression<Func<double, double, double>> expected = (x, y) => double.Max(x, y) + double.Min(x, y);

        var simplified = simplifier.Simplify(source);

        Assert.AreEqual(expected, simplified, ExpressionComparer.Default);
    }

    /// <summary>End-to-end: power-wrapped function terms canonicalize by their base call, not by the exponent.</summary>
    [TestMethod]
    public void EndToEnd_PowerWrappedFunctionTerms_CanonicalizeBySourceCallArguments()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double, double>> source =
            (x, y) => double.Pow(double.Cos(y), 4) + double.Pow(double.Cos(x), 4);
        Expression<Func<double, double, double>> expected =
            (x, y) => double.Pow(double.Cos(x), 4) + double.Pow(double.Cos(y), 4);

        var simplified = simplifier.Simplify(source);

        Assert.AreEqual(expected, simplified, ExpressionComparer.Default);
    }

    /// <summary>End-to-end: subtraction of function-call terms retains correct sign handling after canonicalization.</summary>
    [TestMethod]
    public void EndToEnd_SinMinusCos_RetainsSignAfterCanonicalization()
    {
        var simplifier = new ExpressionSimplifier();
        Expression<Func<double, double>> source = x => double.Sin(x) - double.Cos(x);

        var simplified = simplifier.Simplify(source);
        var compiled = ((Expression<Func<double, double>>)simplified).Compile();

        Assert.AreEqual(double.Sin(0.4) - double.Cos(0.4), compiled(0.4), 1e-9);
    }
}
