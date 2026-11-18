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
/// S5A-02 (mission "S5 residual correctness"): the <c>*ConversionMath</c> rules of <c>ExpressionSimplifier.Math.cs</c> match
/// every public static method of <see cref="double"/> with their function name, whatever its parameters. These tests check every
/// such overload of the running framework: a simplification never throws, an all-<see cref="double"/> overload is rebuilt with
/// its own method and the other overloads (<see cref="double.Round(double, int)"/>,
/// <see cref="double.Round(double, MidpointRounding)"/>, <see cref="double.Round(double, int, MidpointRounding)"/>) are kept,
/// with the same values, exceptions, argument evaluations, comparer and hash behavior, for the built-in and a derived simplifier.
/// </summary>
[TestClass]
public class ExpressionSimplifierRoundOverloadTests
{
    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>digits</c>.</summary>
    private static readonly ParameterExpression Digits = Expression.Parameter(typeof(int), "digits");

    /// <summary>The parameter <c>mode</c>.</summary>
    private static readonly ParameterExpression Mode = Expression.Parameter(typeof(MidpointRounding), "mode");

    /// <summary>The four <see cref="double.Round(double)"/> overloads.</summary>
    private static readonly MethodInfo RoundValue = Round(typeof(double)),
        RoundDigits = Round(typeof(double), typeof(int)),
        RoundMode = Round(typeof(double), typeof(MidpointRounding)),
        RoundDigitsMode = Round(typeof(double), typeof(int), typeof(MidpointRounding));

    /// <summary>The <see cref="double"/> values every overload is evaluated on: midpoints, signs, signed zero, NaN, infinities.</summary>
    private static readonly double[] Values = [2.5, -2.5, 3.5, 0.125, -0.125, 1.005, 0.5, -0.5, 0.0, -0.0, 123456.789, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, double.MaxValue];

    /// <summary>The <see cref="int"/> values: valid digit counts and out-of-range ones (the method throws on those).</summary>
    private static readonly int[] Integers = [0, 1, 2, 15, -1, 16];

    /// <summary>A derived simplifier without any member.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Resolves a public static <c>double.Round</c> overload.</summary>
    /// <param name="parameters">Its parameter types.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Round(params Type[] parameters)
        => typeof(double).GetMethod(nameof(double.Round), BindingFlags.Public | BindingFlags.Static, parameters)!;

    /// <summary>The simplifiers under test: the built-in type and a derived one.</summary>
    private static IEnumerable<ExpressionSimplifier> Simplifiers() => [new ExpressionSimplifier(), new DerivedSimplifier()];

    /// <summary>Returns the innermost exception thrown by <paramref name="action"/>, or <see langword="null"/>.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The innermost exception, or <see langword="null"/>.</returns>
    private static Exception? Innermost(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
            return exception;
        }
    }

    /// <summary>Invokes a compiled delegate and returns its value, or the type name of the exception it raises.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The outcome, formatted with the invariant culture (the round-trip format for floating-point values).</returns>
    private static string Outcome(Delegate function, object[] arguments)
    {
        try
        {
            object? value = function.DynamicInvoke(arguments);
            return value switch
            {
                double d => d.ToString("R", CultureInfo.InvariantCulture) + (d == 0 && double.IsNegative(d) ? " (-0)" : ""),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? "null",
            };
        }
        catch (Exception exception)
        {
            while (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
            return exception.GetType().Name;
        }
    }

    /// <summary>Returns the sample values of a parameter type.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns>The sample values.</returns>
    private static object[] Samples(Type type)
    {
        if (type == typeof(double)) return Values.Cast<object>().ToArray();
        if (type == typeof(int)) return Integers.Cast<object>().ToArray();
        if (type == typeof(MidpointRounding)) return Enum.GetValues<MidpointRounding>().Cast<object>().Append((MidpointRounding)42).ToArray();
        throw new AssertInconclusiveException("no sample values for " + type);
    }

    /// <summary>Enumerates the cartesian product of the sample values of <paramref name="types"/>, truncated to keep the test fast.</summary>
    /// <param name="types">The parameter types.</param>
    /// <returns>The argument tuples.</returns>
    private static IEnumerable<object[]> Arguments(Type[] types)
    {
        IEnumerable<object[]> tuples = [[]];
        foreach (Type type in types)
        {
            object[] samples = Samples(type);
            tuples = tuples.SelectMany(tuple => samples.Select(sample => tuple.Append(sample).ToArray()));
        }

        return tuples.Take(2_000);
    }

    /// <summary>
    /// The function names the <c>*ConversionMath</c> rules declare on <see cref="double"/> (by reflection over the rule
    /// attributes, so that a new rule is audited too).
    /// </summary>
    /// <returns>The function names.</returns>
    private static string[] ConversionFunctionNames()
        => typeof(ExpressionSimplifier).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static method => method.Name.EndsWith("ConversionMath", StringComparison.Ordinal))
            .Select(static method => method.GetCustomAttribute<ExpressionCallSignatureAttribute>())
            .Where(static attribute => attribute is not null && attribute.Types.Contains(typeof(double)))
            .Select(static attribute => attribute!.FunctionName)
            .Distinct()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// The audit inventory: the function names matched on <see cref="double"/> and the overloads of each that are not
    /// all-<see cref="double"/> (only <c>Round</c> has some). A new overload in a future framework fails this test, so that it is
    /// audited before it is relied on.
    /// </summary>
    [TestMethod]
    public void Inventory_OnlyRoundHasOverloadsWithOtherParameterTypes()
    {
        string[] names = ConversionFunctionNames();
        CollectionAssert.AreEquivalent(
            new[] { "Abs", "Acos", "Acosh", "Asin", "Asinh", "Atan", "Atanh", "Cbrt", "Ceiling", "Clamp", "Cos", "Cosh", "Floor", "Log", "Log10", "Log2", "Max", "Min", "Pow", "Round", "Sign", "Sin", "Sinh", "Sqrt", "Tan", "Tanh" },
            names);

        string[] heterogeneous = names
            .SelectMany(static name => typeof(double).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == name))
            .Where(static method => method.GetParameters().Any(static parameter => parameter.ParameterType != typeof(double)))
            .Select(static method => method.Name + "(" + string.Join(", ", method.GetParameters().Select(static parameter => parameter.ParameterType.Name)) + ")")
            .OrderBy(static text => text, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "Round(Double, Int32)", "Round(Double, Int32, MidpointRounding)", "Round(Double, MidpointRounding)" }, heterogeneous);
    }

    /// <summary>
    /// Every public static <see cref="double"/> overload a conversion rule matches simplifies without exception, keeps its own
    /// method (or becomes a power node for <c>Pow</c>), keeps its type, and computes the same value or raises the same exception as
    /// the source on every sample, for the built-in and a derived simplifier.
    /// </summary>
    [TestMethod]
    public void EveryMatchedOverload_KeepsItsMethodAndItsValues()
    {
        int overloads = 0;
        foreach (string name in ConversionFunctionNames())
        {
            foreach (MethodInfo method in typeof(double).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == name && !method.IsGenericMethodDefinition))
            {
                Type[] types = method.GetParameters().Select(static parameter => parameter.ParameterType).ToArray();
                ParameterExpression[] parameters = types.Select(static (type, i) => Expression.Parameter(type, "p" + i)).ToArray();
                LambdaExpression source = Expression.Lambda(Expression.Call(method, parameters), parameters);
                Delegate expected = source.Compile();
                overloads++;

                foreach (ExpressionSimplifier simplifier in Simplifiers())
                {
                    string label = simplifier.GetType().Name + " " + method;
                    Exception? failure = Innermost(() => simplifier.Simplify(source));
                    Assert.IsNull(failure, label + ": " + failure);

                    var simplified = (LambdaExpression)simplifier.Simplify(source);
                    Assert.AreEqual(method.ReturnType, simplified.Body.Type, label);
                    if (name == nameof(double.Pow))
                    {
                        Assert.AreEqual(ExpressionType.Power, simplified.Body.NodeType, label);
                    }
                    else
                    {
                        var call = (MethodCallExpression)simplified.Body;
                        Assert.AreSame(method, call.Method, label);
                        Assert.AreEqual(parameters.Length, call.Arguments.Count, label);
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Assert.AreSame(simplified.Parameters[i], call.Arguments[i], label + " argument " + i);
                        }
                    }

                    Delegate actual = simplified.Compile();
                    foreach (object[] arguments in Arguments(types))
                    {
                        Assert.AreEqual(Outcome(expected, arguments), Outcome(actual, arguments), label + " at (" + string.Join(", ", arguments) + ")");
                    }
                }
            }
        }

        Assert.IsTrue(overloads >= 30, overloads + " overloads");
    }

    /// <summary>
    /// The three overloads with other parameter types are kept exactly (same method, arguments neither converted nor dropped),
    /// their arguments are still simplified, and midpoint rounding keeps the source's mode.
    /// </summary>
    [TestMethod]
    public void HeterogeneousRoundOverloads_AreKeptWithSimplifiedArguments()
    {
        Expression xPlusZero = Expression.Add(X, Expression.Constant(0.0));
        Expression twoPlusZero = Expression.Add(Expression.Constant(2), Expression.Constant(0));
        (Expression Source, MethodInfo Method, string Expected)[] cases =
        [
            (Expression.Call(RoundDigits, xPlusZero, twoPlusZero), RoundDigits, "Double.Round(x, 2)"),
            (Expression.Call(RoundMode, xPlusZero, Mode), RoundMode, "Double.Round(x, mode)"),
            (Expression.Call(RoundDigitsMode, xPlusZero, Digits, Mode), RoundDigitsMode, "Double.Round(x, digits, mode)"),
        ];

        foreach ((Expression source, MethodInfo method, string expected) in cases)
        {
            foreach (ExpressionSimplifier simplifier in Simplifiers())
            {
                var call = (MethodCallExpression)simplifier.Simplify(source);
                Assert.AreSame(method, call.Method, expected);
                Assert.AreEqual(expected, ExpressionTraceFormatter.Format(call));
                CollectionAssert.AreEqual(method.GetParameters().Select(static parameter => parameter.ParameterType).ToArray(), call.Arguments.Select(static argument => argument.Type).ToArray(), expected);
                Assert.IsFalse(call.Arguments.Any(static argument => argument.NodeType == ExpressionType.Convert), expected);
            }
        }

        var midpoint = Expression.Lambda<Func<double, int, MidpointRounding, double>>(Expression.Call(RoundDigitsMode, X, Digits, Mode), X, Digits, Mode);
        Func<double, int, MidpointRounding, double> rounded = ((Expression<Func<double, int, MidpointRounding, double>>)new ExpressionSimplifier().Simplify(midpoint)).Compile();
        Assert.AreEqual(0.13, rounded(0.125, 2, MidpointRounding.AwayFromZero));
        Assert.AreEqual(0.12, rounded(0.125, 2, MidpointRounding.ToEven));
        Assert.AreEqual(-0.13, rounded(-0.125, 2, MidpointRounding.AwayFromZero));
        Assert.AreEqual(-0.12, rounded(-0.125, 2, MidpointRounding.ToZero));
    }

    /// <summary>
    /// The control: the one-argument overload is rebuilt with its own method, and <see cref="Math.Round(double, int)"/>, which no
    /// rule matches, is kept.
    /// </summary>
    [TestMethod]
    public void Controls_OneArgumentOverloadAndMathRound()
    {
        var call = (MethodCallExpression)new ExpressionSimplifier().Simplify(Expression.Call(RoundValue, Expression.Add(X, Expression.Constant(0.0))));
        Assert.AreSame(RoundValue, call.Method);
        Assert.AreSame(X, call.Arguments[0]);

        MethodInfo mathRound = typeof(Math).GetMethod(nameof(Math.Round), [typeof(double), typeof(int)])!;
        Assert.AreSame(mathRound, ((MethodCallExpression)new ExpressionSimplifier().Simplify(Expression.Call(mathRound, X, Expression.Constant(2)))).Method);
    }

    /// <summary>An argument with an effect is evaluated as often and in the same order by the simplified tree as by the source.</summary>
    [TestMethod]
    public void HeterogeneousRoundOverloads_KeepTheArgumentEvaluations()
    {
        var log = new EvaluationLog();
        Expression value = Expression.Call(Expression.Constant(log), nameof(EvaluationLog.Value), null, Expression.Constant(2.345));
        Expression digits = Expression.Call(Expression.Constant(log), nameof(EvaluationLog.Digits), null, Expression.Constant(2));
        Expression mode = Expression.Call(Expression.Constant(log), nameof(EvaluationLog.Mode), null, Expression.Constant(MidpointRounding.AwayFromZero));
        Expression[] sources =
        [
            Expression.Call(RoundDigits, value, digits),
            Expression.Call(RoundMode, value, mode),
            Expression.Call(RoundDigitsMode, value, digits, mode),
            Expression.Add(Expression.Call(RoundDigits, value, digits), Expression.Multiply(Expression.Constant(0.0), Expression.Call(RoundMode, value, mode))),
        ];

        foreach (Expression source in sources)
        {
            foreach (ExpressionSimplifier simplifier in Simplifiers())
            {
                Expression simplified = simplifier.Simplify(source);
                log.Entries.Clear();
                double expected = Expression.Lambda<Func<double>>(source).Compile()();
                string expectedLog = string.Join(",", log.Entries);
                log.Entries.Clear();
                double actual = Expression.Lambda<Func<double>>(simplified).Compile()();
                Assert.AreEqual(expected, actual, ExpressionTraceFormatter.Format(source));
                Assert.AreEqual(expectedLog, string.Join(",", log.Entries), ExpressionTraceFormatter.Format(source));
            }
        }
    }

    /// <summary>The comparer and its hash treat the kept overloads like any call: equal to a rebuilt copy, distinct from another argument.</summary>
    [TestMethod]
    public void HeterogeneousRoundOverloads_ComparerAndHash()
    {
        Expression two = Expression.Call(RoundDigits, X, Expression.Constant(2));
        Expression twoCopy = Expression.Call(RoundDigits, X, Expression.Add(Expression.Constant(1), Expression.Constant(1)));
        Expression three = Expression.Call(RoundDigits, X, Expression.Constant(3));
        Expression away = Expression.Call(RoundMode, X, Expression.Constant(MidpointRounding.AwayFromZero));
        Expression even = Expression.Call(RoundMode, X, Expression.Constant(MidpointRounding.ToEven));

        Assert.IsTrue(ExpressionComparer.Default.Equals(two, twoCopy));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(two), ExpressionComparer.Default.GetHashCode(twoCopy));
        Assert.IsFalse(ExpressionComparer.Default.Equals(two, three));
        Assert.IsFalse(ExpressionComparer.Default.Equals(away, even));
        Assert.IsFalse(ExpressionComparer.Default.Equals(two, away));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Subtract(two, twoCopy), Expression.Constant(0.0)), "x - x cancels on the kept call");
    }

    /// <summary>Records the evaluations of the arguments of a call.</summary>
    public sealed class EvaluationLog
    {
        /// <summary>Gets the evaluations, in order.</summary>
        public List<string> Entries { get; } = [];

        /// <summary>Logs and returns a value.</summary>
        /// <param name="value">The value.</param>
        /// <returns><paramref name="value"/>.</returns>
        public double Value(double value)
        {
            Entries.Add("value");
            return value;
        }

        /// <summary>Logs and returns a digit count.</summary>
        /// <param name="digits">The digit count.</param>
        /// <returns><paramref name="digits"/>.</returns>
        public int Digits(int digits)
        {
            Entries.Add("digits");
            return digits;
        }

        /// <summary>Logs and returns a rounding mode.</summary>
        /// <param name="mode">The mode.</param>
        /// <returns><paramref name="mode"/>.</returns>
        public MidpointRounding Mode(MidpointRounding mode)
        {
            Entries.Add("mode");
            return mode;
        }
    }
}
