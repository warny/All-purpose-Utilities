using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// S5A-05 (finalization of rebuilt forms): the reproducers of the convergence corpus whose first result was not a fixed point
/// on <c>master</c> <c>65495e50</c>, and the two families behind them (<c>docs/expressions/SimplifierFlatten.md</c> §18).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A reassociation (<c>t - (a + b)</c>, <c>t - (a - b)</c>) over operands that are not inert finite arithmetic (a call, an
/// infinite constant) returned its rebuilt difference as built; it is now finalized by the additive canonicalization alone.</item>
/// <item>A product added twice whose common coefficient is symbolic (<c>x*(y - 1) + x*(y - 1)</c>) kept the coefficient
/// <c>x + x</c>; it is now the canonical product <c>2 * (x * (-1 + y))</c>.</item>
/// </list>
/// Each source is the exact tree of its <c>converge</c> case (seeds 4,000), rebuilt from the text of the audit harness; the
/// expected first result is the fixed point <c>master</c> reached after two to four calls.
/// </remarks>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierS5A05FinalizationTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>A derived simplifier without any member: it keeps the historical rules.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Counts its calls, so that an evaluation count can be compared.</summary>
    private static class Probe
    {
        /// <summary>The number of calls of <see cref="F"/>, on the current thread.</summary>
        [ThreadStatic]
        public static int Calls;

        /// <summary>An observable call: counts itself and returns 3.</summary>
        /// <returns>3.</returns>
        public static double F()
        {
            Calls++;
            return 3;
        }
    }

    /// <summary>Simplifies with a new built-in simplifier.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression S(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Formats with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>
    /// Asserts that one call gives <paramref name="expected"/>, that it is a structural fixed point and that it evaluates like
    /// the source (equal, or both <c>NaN</c>, within a relative 1e-9 for the rounding latitude of the symbolic contract).
    /// </summary>
    /// <param name="source">The source tree over x, y and z.</param>
    /// <param name="expected">The expected text of the first result.</param>
    private static void AssertFinalized(Expression source, string expected)
    {
        Expression once = S(source);
        Assert.AreEqual(expected, Text(once));
        Expression twice = S(once);
        Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, twice), $"Not a fixed point: {Text(once)} -> {Text(twice)}");

        Func<double, double, double, double> before = Expression.Lambda<Func<double, double, double, double>>(source, X, Y, Z).Compile();
        Func<double, double, double, double> after = Expression.Lambda<Func<double, double, double, double>>(once, X, Y, Z).Compile();
        // Positive samples: the logarithm identities of the symbolic contract (Log(y) + Log(y) -> Log(y ** 2)) widen the domain.
        foreach ((double x, double y, double z) in new[] { (0.5, 1.5, 2.5), (1.25, 3.0, 0.75), (2.0, 0.5, 1.5) })
        {
            double a = before(x, y, z), b = after(x, y, z);
            bool same = a.Equals(b) || Math.Abs(a - b) <= 1e-9 * Math.Max(Math.Abs(a), Math.Abs(b));
            Assert.IsTrue(same, $"{Text(source)} at ({x}, {y}, {z}): {a} against {b}");
        }
    }

    /// <summary><c>gen#2430</c>: <c>z - (x - R)</c> with a <c>Tan</c> call in <c>R</c> (fixed after two calls on the base).</summary>
    [TestMethod]
    public void Gen2430_ReassociationOverACall_IsFinalizedInOneCall()
        => AssertFinalized(
            Parse("((-2 + 2) - (-z)) - ((-(-x)) - ((Double.Tan(x) + (z / (1 / z))) - ((z * y) + ((1 - z) * y))))"),
            "(0 - x) + ((0 - y) + (z + ((z ** 2) + Double.Tan(x))))");

    /// <summary><c>gen#3191</c>: a reassociation over <c>Log</c> and <c>Sin</c> constant calls (two calls on the base).</summary>
    [TestMethod]
    public void Gen3191_ReassociationOverConstantCalls_IsFinalizedInOneCall()
        => AssertFinalized(
            Parse("(-((Double.Sin(-1) * (--1)) - ((Double.Log(-1) + Double.Log(2)) + Double.Log(10)))) + x"),
            "x + (Double.Log(-20) + (0 - Double.Sin(-1)))");

    /// <summary><c>gen#2330</c>: a product added twice with a symbolic coefficient (two calls on the base).</summary>
    [TestMethod]
    public void Gen2330_ProductAddedTwice_IsMergedInOneCall()
        => AssertFinalized(
            Parse("((--2) / (1 / (--2))) / ((x * (y + -1)) + (x * (y + -1)))"),
            "4 / (2 * (x * (-1 + y)))");

    /// <summary><c>gen#459</c>: nested reassociations over an infinite constant and a call (four calls on the base).</summary>
    [TestMethod]
    public void Gen459_NestedReassociations_AreFinalizedInOneCall()
        => AssertFinalized(
            Parse("(((((z ** -2) ** z) * ((Double.Sin(1) ** 2) + (Double.Cos(1) ** 2))) + ((z + y) * ((Double.Sin(1) ** 2) + (Double.Cos(1) ** 2)))) / ((-y) / (-y))) / (((-1 / 0) - ((x + 2) - (((-1 ** y) ** y) - -1))) - ((-2 - (y - (1 - x))) + (Double.Sin(y) + (y / z))))"),
            "(y * (z + (y + ((1 / (z ** 2)) ** z)))) / (y * (-Infinity + (y + ((0 - (y / z)) + (((-1 ** y) ** y) + (0 - Double.Sin(y)))))))");

    /// <summary><c>gen#743</c>: the control case S5A-10 already fixed (one call on the base); PR B folds its <c>-(10)</c>.</summary>
    [TestMethod]
    public void Gen743_Control_IsUnchanged()
        => AssertFinalized(
            Parse("y - (((-1 * (x / y)) - ((x ** x) * (x / y))) - (((--1) / ((x + 2) * (-(10)))) - ((((-1 ** 2) - (Double.Log(y) + Double.Log(1E+300))) - z) - Double.Log(y))))"),
            "-1 + (y + (z + ((1 / (-10 * (2 + x))) + (((1 + (x ** x)) * (x / y)) + Double.Log(1E+300 * (y ** 2))))))");

    /// <summary>
    /// The two residues PR B exposed, which took two calls until the C3c consistency fix (<c>SimplifierFlatten.md</c> §20, §21):
    /// <c>gen#1594</c>, a factored coefficient of two added terms left in source order (§21.5), and <c>gen#2973</c>, a chain the
    /// raw admission refused (the negation of a constant) whose level by level result is a factorable chain, now factored at its
    /// boundary (§21.3). Each first result is the fixed point the second call reached on <c>master</c> <c>523fc708</c>.
    /// </summary>
    [TestMethod]
    public void FormerResidues_TakeOneCall()
    {
        (string Source, string Expected)[] cases =
        [
            ("(((x ** y) * (((-1 - z) - z) - z)) + ((3 / z) * (((-1 - z) - z) - z))) / (--1)",
                "(-1 + (-3 * z)) * ((3 / z) + (x ** y))"),
            ("((-(1E+300)) + (-y)) - (((x * y) - (y * y)) - (3 + z))",
                "-1E+300 + (z + ((0 - (y * (1 + x))) + (y ** 2)))"),
        ];
        foreach ((string source, string expected) in cases)
        {
            Expression once = S(Parse(source));
            Assert.AreEqual(expected, Text(once), source);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(once, S(once)), expected);
        }
    }

    /// <summary>The equal-products family: symbolic coefficients are merged, constant coefficients keep the historical fold.</summary>
    [TestMethod]
    public void EqualProducts_AreMergedOnce()
    {
        AssertFinalized(Parse("(x * (y - 1)) + (x * (y - 1))"), "2 * (x * (-1 + y))");
        AssertFinalized(Parse("(x * Double.Sin(y)) + (x * Double.Sin(y))"), "2 * (x * Double.Sin(y))");
        AssertFinalized(Parse("(0.5 * x) + (0.5 * x)"), "x");
        AssertFinalized(Parse("(2 * x) + (2 * x)"), "4 * x");
        AssertFinalized(Parse("(x * y) + (x * y)"), "2 * (x * y)");
    }

    /// <summary>An observable operand keeps both evaluations: the effect gate refuses the factoring rule as before.</summary>
    [TestMethod]
    public void EqualProducts_WithAnObservableCall_KeepBothEvaluations()
    {
        MethodInfo f = typeof(Probe).GetMethod(nameof(Probe.F))!;
        Expression product = Expression.Multiply(Expression.Call(f), Expression.Subtract(Y, Expression.Constant(1.0)));
        Expression source = Expression.Add(product, Expression.Multiply(Expression.Call(f), Expression.Subtract(Y, Expression.Constant(1.0))));
        var simplified = (Expression<Func<double, double>>)S(Expression.Lambda<Func<double, double>>(source, Y));
        Probe.Calls = 0;
        double expected = Expression.Lambda<Func<double, double>>(source, Y).Compile()(4);
        Assert.AreEqual(2, Probe.Calls);
        Probe.Calls = 0;
        Assert.AreEqual(expected, simplified.Compile()(4));
        Assert.AreEqual(2, Probe.Calls, Text(simplified));
    }

    /// <summary>A reassociation with a <c>NaN</c> term keeps the node as built, for the policy-dependent <c>NaN</c> fold.</summary>
    [TestMethod]
    public void Reassociation_WithANaNTerm_IsLeftToTheNaNFold()
    {
        Expression source = Parse("z - (NaN - Double.Sin(x))");
        var permissive = new ExpressionSimplifier { Options = new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None } };
        Assert.IsTrue(permissive.Simplify(source) is ConstantExpression { Value: double value } && double.IsNaN(value));
    }

    /// <summary>A derived simplifier keeps the historical rebuilt nodes (the finalization belongs to the built-in type).</summary>
    [TestMethod]
    public void DerivedSimplifier_KeepsTheHistoricalNodes()
    {
        Expression source = Parse("((-2 + 2) - (-z)) - ((-(-x)) - ((Double.Tan(x) + (z / (1 / z))) - ((z * y) + ((1 - z) * y))))");
        Assert.AreNotEqual("(0 - x) + ((0 - y) + (z + ((z ** 2) + Double.Tan(x))))", Text(new DerivedSimplifier().Simplify(source)));
        Assert.AreNotEqual("2 * (x * (-1 + y))", Text(new DerivedSimplifier().Simplify(Parse("(x * (y - 1)) + (x * (y - 1))"))));
    }

    /// <summary>Parses the text the engine's formatter prints for a <see cref="double"/> tree over x, y and z.</summary>
    /// <param name="text">The text: numbers (a leading <c>-</c> before a digit is part of the constant; the formatter prints the
    /// negation of a constant <c>c</c> as <c>(-c)</c>, written <c>(-(c))</c> here), x, y, z, <c>NaN</c>,
    /// <c>Infinity</c>, <c>Double.Sin/Cos/Tan/Log(…)</c>, unary <c>-</c>, <c>+ - * / **</c> and parentheses.</param>
    /// <returns>The tree.</returns>
    private static Expression Parse(string text)
    {
        int position = 0;
        Expression result = ParseSum(text, ref position);
        SkipSpaces(text, ref position);
        if (position != text.Length) throw new FormatException("Unexpected text at " + position + ": " + text);
        return result;
    }

    /// <summary>Parses a left-associated sum or difference.</summary>
    private static Expression ParseSum(string text, ref int position)
    {
        Expression left = ParseProduct(text, ref position);
        while (true)
        {
            SkipSpaces(text, ref position);
            if (Accept(text, ref position, "+")) left = Expression.Add(left, ParseProduct(text, ref position));
            else if (Accept(text, ref position, "-")) left = Expression.Subtract(left, ParseProduct(text, ref position));
            else return left;
        }
    }

    /// <summary>Parses a left-associated product or quotient.</summary>
    private static Expression ParseProduct(string text, ref int position)
    {
        Expression left = ParsePower(text, ref position);
        while (true)
        {
            SkipSpaces(text, ref position);
            if (Peek(text, position, "**")) return left;
            if (Accept(text, ref position, "*")) left = Expression.Multiply(left, ParsePower(text, ref position));
            else if (Accept(text, ref position, "/")) left = Expression.Divide(left, ParsePower(text, ref position));
            else return left;
        }
    }

    /// <summary>Parses a power.</summary>
    private static Expression ParsePower(string text, ref int position)
    {
        Expression left = ParseUnary(text, ref position);
        SkipSpaces(text, ref position);
        return Accept(text, ref position, "**") ? Expression.Power(left, ParsePower(text, ref position)) : left;
    }

    /// <summary>Parses a negation, a negative constant or an atom.</summary>
    private static Expression ParseUnary(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '-')
        {
            if (position + 1 < text.Length && char.IsDigit(text[position + 1])) return ParseNumber(text, ref position);
            position++;
            return Expression.Negate(ParseUnary(text, ref position));
        }

        if (Accept(text, ref position, "("))
        {
            Expression inner = ParseSum(text, ref position);
            SkipSpaces(text, ref position);
            if (!Accept(text, ref position, ")")) throw new FormatException("Missing ) in " + text);
            return inner;
        }

        if (position < text.Length && char.IsDigit(text[position])) return ParseNumber(text, ref position);
        foreach ((string name, Expression value) in new (string, Expression)[] { ("x", X), ("y", Y), ("z", Z), ("NaN", Expression.Constant(double.NaN)), ("Infinity", Expression.Constant(double.PositiveInfinity)) })
        {
            if (Peek(text, position, name) && (position + name.Length == text.Length || !char.IsLetter(text[position + name.Length])))
            {
                position += name.Length;
                return value;
            }
        }

        if (Accept(text, ref position, "Double."))
        {
            int start = position;
            while (position < text.Length && char.IsLetter(text[position])) position++;
            MethodInfo method = typeof(double).GetMethod(text[start..position], [typeof(double)]) ?? throw new FormatException("Unknown function in " + text);
            if (!Accept(text, ref position, "(")) throw new FormatException("Missing ( in " + text);
            Expression argument = ParseSum(text, ref position);
            if (!Accept(text, ref position, ")")) throw new FormatException("Missing ) in " + text);
            return Expression.Call(method, argument);
        }

        throw new FormatException("Unexpected text at " + position + ": " + text);
    }

    /// <summary>Parses a number, with an optional leading minus sign and exponent.</summary>
    private static Expression ParseNumber(string text, ref int position)
    {
        int start = position;
        if (text[position] == '-') position++;
        while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.')) position++;
        if (position < text.Length && text[position] == 'E')
        {
            position++;
            if (text[position] is '+' or '-') position++;
            while (position < text.Length && char.IsDigit(text[position])) position++;
        }

        return Expression.Constant(double.Parse(text[start..position], CultureInfo.InvariantCulture));
    }

    /// <summary>Skips spaces.</summary>
    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && text[position] == ' ') position++;
    }

    /// <summary>Whether <paramref name="token"/> starts at <paramref name="position"/>.</summary>
    private static bool Peek(string text, int position, string token)
        => string.CompareOrdinal(text, position, token, 0, token.Length) == 0;

    /// <summary>Consumes <paramref name="token"/> when it starts at <paramref name="position"/>.</summary>
    private static bool Accept(string text, ref int position, string token)
    {
        SkipSpaces(text, ref position);
        if (!Peek(text, position, token)) return false;
        position += token.Length;
        return true;
    }
}
