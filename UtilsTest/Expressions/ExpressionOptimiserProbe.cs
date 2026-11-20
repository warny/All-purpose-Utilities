using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Observable runtime helpers of the <see cref="ExpressionOptimiser"/> differential tests: every helper records its evaluation
/// in a per-thread log, so the number and order of the evaluations of a compiled tree can be compared with those of the source.
/// </summary>
public static class OptimiserProbe
{
    /// <summary>The events recorded on the current thread.</summary>
    [ThreadStatic]
    private static List<string>? _log;

    /// <summary>A mutable static field read and written by the trees under test.</summary>
    [ThreadStatic]
    public static int MutableField;

    /// <summary>Gets the events recorded on the current thread since the last <see cref="Reset"/>.</summary>
    public static List<string> Log => _log ??= [];

    /// <summary>Clears the log and the mutable field of the current thread.</summary>
    public static void Reset()
    {
        Log.Clear();
        MutableField = 0;
    }

    /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The returned value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static int Count(string name, int value)
    {
        Log.Add(name);
        return value;
    }

    /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The returned value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double CountDouble(string name, double value)
    {
        Log.Add(name);
        return value;
    }

    /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The returned value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static bool CountBool(string name, bool value)
    {
        Log.Add(name);
        return value;
    }

    /// <summary>Records <paramref name="name"/> and throws an <see cref="InvalidOperationException"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <returns>Never returns.</returns>
    public static int Fail(string name)
    {
        Log.Add(name);
        throw new InvalidOperationException(name);
    }

    /// <summary>A custom multiplication operator method: records its call and multiplies.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    public static int MultiplyCounted(int left, int right)
    {
        Log.Add("op*");
        return left * right;
    }

    /// <summary>A custom addition operator method: records its call and adds.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum.</returns>
    public static int AddCounted(int left, int right)
    {
        Log.Add("op+");
        return left + right;
    }

    /// <summary>A custom subtraction operator method: records its call and subtracts.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference.</returns>
    public static int SubtractCounted(int left, int right)
    {
        Log.Add("op-");
        return left - right;
    }

    /// <summary>A custom division operator method: records its call and divides.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The quotient.</returns>
    public static int DivideCounted(int left, int right)
    {
        Log.Add("op/");
        return left / right;
    }

    /// <summary>A custom negation operator method: records its call and negates.</summary>
    /// <param name="value">The operand.</param>
    /// <returns>The negation.</returns>
    public static int NegateCounted(int value)
    {
        Log.Add("op-u");
        return -value;
    }

    /// <summary>A custom conversion method from <see cref="int"/> to <see cref="int"/>: records its call and returns its argument.</summary>
    /// <param name="value">The operand.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static int IdentityCounted(int value)
    {
        Log.Add("convert");
        return value;
    }

    /// <summary>A custom multiplication of an enumeration: records its call and multiplies the underlying values.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    public static ProbeLevel MultiplyLevels(ProbeLevel left, ProbeLevel right)
    {
        Log.Add("level*");
        return (ProbeLevel)((int)left * (int)right);
    }

    /// <summary>Gets a public static method of this class by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    public static MethodInfo Method(string name) => typeof(OptimiserProbe).GetMethod(name)!;

    /// <summary>Builds a call to <see cref="Count"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The value expression.</param>
    /// <returns>The call.</returns>
    public static Expression CountCall(string name, Expression value) => Expression.Call(Method(nameof(Count)), Expression.Constant(name), value);

    /// <summary>Builds a call to <see cref="CountDouble"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The value expression.</param>
    /// <returns>The call.</returns>
    public static Expression CountDoubleCall(string name, Expression value) => Expression.Call(Method(nameof(CountDouble)), Expression.Constant(name), value);

    /// <summary>Builds a call to <see cref="CountBool"/>.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="value">The value expression.</param>
    /// <returns>The call.</returns>
    public static Expression CountBoolCall(string name, Expression value) => Expression.Call(Method(nameof(CountBool)), Expression.Constant(name), value);
}

/// <summary>An enumeration multiplied by a custom operator method (<see cref="OptimiserProbe.MultiplyLevels"/>).</summary>
public enum ProbeLevel
{
    /// <summary>Zero.</summary>
    Zero = 0,

    /// <summary>One.</summary>
    One = 1,

    /// <summary>Three.</summary>
    Three = 3,
}

/// <summary>A value with custom arithmetic operators that record their calls.</summary>
/// <param name="Cents">The amount.</param>
public readonly record struct ProbeMoney(int Cents)
{
    /// <summary>Multiplies two amounts and records the call.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    public static ProbeMoney operator *(ProbeMoney left, ProbeMoney right)
    {
        OptimiserProbe.Log.Add("money*");
        return new ProbeMoney(left.Cents * right.Cents);
    }

    /// <summary>Negates an amount and records the call.</summary>
    /// <param name="value">The operand.</param>
    /// <returns>The negation.</returns>
    public static ProbeMoney operator -(ProbeMoney value)
    {
        OptimiserProbe.Log.Add("money-u");
        return new ProbeMoney(-value.Cents);
    }
}

/// <summary>A class whose constructor records each allocation.</summary>
public sealed class ProbeAllocation
{
    /// <summary>Initializes an instance and records the allocation.</summary>
    public ProbeAllocation()
    {
        OptimiserProbe.Log.Add("new");
    }

    /// <summary>A value read after the allocation.</summary>
    public int Value = 5;
}

/// <summary>
/// Differential comparison of a source lambda and its optimized form: exact delegate and body types, then, for every input,
/// the result (binary representation for floating-point and decimal values), the exception (type and message), the
/// evaluation log of <see cref="OptimiserProbe"/> and the mutable field.
/// </summary>
internal static class OptimiserDifferential
{
    /// <summary>Optimizes <paramref name="source"/> with default limits.</summary>
    /// <param name="source">The source lambda.</param>
    /// <returns>The optimized lambda.</returns>
    public static LambdaExpression Optimize(LambdaExpression source) => (LambdaExpression)new ExpressionOptimiser().Optimize(source);

    /// <summary>
    /// Asserts that the optimized form of <paramref name="source"/> has exactly the same delegate and body types and behaves
    /// exactly as the source for every input.
    /// </summary>
    /// <param name="source">The source lambda.</param>
    /// <param name="inputs">The argument lists.</param>
    /// <returns>The optimized lambda, for structural assertions.</returns>
    public static LambdaExpression AssertEquivalent(LambdaExpression source, params object?[][] inputs)
    {
        LambdaExpression optimized = Optimize(source);
        Assert.AreSame(source.Type, optimized.Type, "delegate type");
        Assert.AreSame(source.Body.Type, optimized.Body.Type, "body type");

        Delegate original = source.Compile();
        Delegate rewritten = optimized.Compile();
        if (inputs.Length == 0) inputs = [[]];
        foreach (object?[] input in inputs)
        {
            string label = "input (" + string.Join(", ", input.Select(Describe)) + ")";
            Assert.AreEqual(Run(original, input), Run(rewritten, input), label);
        }

        return optimized;
    }

    /// <summary>Invokes <paramref name="compiled"/> and describes everything it did.</summary>
    /// <param name="compiled">The compiled delegate.</param>
    /// <param name="input">The arguments.</param>
    /// <returns>A description of the result or exception, the evaluation log and the mutable field.</returns>
    private static string Run(Delegate compiled, object?[] input)
    {
        OptimiserProbe.Reset();
        string outcome;
        try
        {
            outcome = "value " + Describe(compiled.DynamicInvoke(input));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            outcome = "throws " + inner.GetType().FullName + ": " + inner.Message;
        }

        return outcome + " | log [" + string.Join(", ", OptimiserProbe.Log) + "] | field " + OptimiserProbe.MutableField;
    }

    /// <summary>Describes a value exactly: binary representation for floating-point and decimal values.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The description.</returns>
    public static string Describe(object? value) => value switch
    {
        null => "null",
        double d => "double 0x" + BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture),
        float f => "float 0x" + BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture),
        decimal m => "decimal [" + string.Join(",", decimal.GetBits(m)) + "]",
        _ => value.GetType().Name + " " + Convert.ToString(value, CultureInfo.InvariantCulture),
    };
}
