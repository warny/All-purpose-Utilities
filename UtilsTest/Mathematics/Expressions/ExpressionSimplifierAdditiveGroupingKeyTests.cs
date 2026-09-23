using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// End-to-end additive-grouping coverage for <see cref="ExpressionSimplifier"/>'s canonicalization.
/// </summary>
/// <remarks>
/// This file previously also characterized the private static <c>ExpressionSimplifier.GetAdditiveGroupingKey(Expression)</c>
/// helper's exact textual key format (a <c>"func:...:categoryOrder"</c>/<c>"expr:..."</c> string built via
/// <see cref="Expression.ToString()"/>/<c>string.Join&lt;Expression&gt;</c>), including the exact
/// per-argument <see cref="Expression.ToString()"/> invocation count/order, unescaped "|" separator
/// handling, <see langword="null"/>-<c>ToString()</c> joining, and <c>ToString()</c>-exception propagation.
/// Stage S4 of <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c> deliberately replaces that
/// textual key with a structural one that never calls <see cref="Expression.ToString()"/> at all, so that
/// private method no longer exists and those characterization tests (which existed only to lock PR #589's
/// allocation-focused rewrite of the old textual key, not any user-observable behavior) have been removed
/// rather than kept as dead reflection-based assertions against a method that no longer exists. The
/// observable behavior those tests protected — Sin/Cos/Max/Min/power-wrapped-function grouping and ordering,
/// and subtraction sign handling — remains covered by the end-to-end tests below and by the dedicated S4
/// regression suite, <c>ExpressionSimplifierStructuralCanonicalizationTests</c>, which additionally proves
/// <see cref="Expression.ToString()"/> is never called during canonicalization (see its "ToString must not
/// execute" matrix).
/// </remarks>
[TestClass]
public class ExpressionSimplifierAdditiveGroupingKeyTests
{
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
