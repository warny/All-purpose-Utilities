using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>User methods of the S5 P18 Architecture A tests, annotated with <see cref="DeterministicAttribute"/> or not.</summary>
public static class BehaviorMethods
{
    /// <summary>The number of calls of <see cref="LyingCounter"/>.</summary>
    [ThreadStatic]
    private static int _lyingCalls;

    /// <summary>Gets or sets the number of calls of <see cref="LyingCounter"/> on this thread.</summary>
    public static int LyingCalls
    {
        get => _lyingCalls;
        set => _lyingCalls = value;
    }

    /// <summary>A mutable static counter, written by <see cref="Unannotated"/>.</summary>
    public static int Counter;

    /// <summary>A declared deterministic function.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><c>2 * x</c>.</returns>
    [Deterministic]
    public static double Scale(double x) => 2 * x;

    /// <summary>The same function without declaration (it also counts its calls).</summary>
    /// <param name="x">The argument.</param>
    /// <returns><c>2 * x</c>.</returns>
    public static double Unannotated(double x)
    {
        Counter++;
        return 2 * x;
    }

    /// <summary>A method whose declaration lies: it counts its calls, a side effect.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><paramref name="x"/>.</returns>
    [Deterministic]
    public static double LyingCounter(double x)
    {
        _lyingCalls++;
        return x;
    }

    /// <summary>Marked <see cref="PureAttribute"/> only, which the analysis does not trust.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><paramref name="x"/>.</returns>
    [Pure]
    public static double PureOnly(double x) => x;

    /// <summary>A declared function reading an array.</summary>
    /// <param name="values">The array.</param>
    /// <returns>Its first element.</returns>
    [Deterministic]
    public static double First(double[] values) => values[0];

    /// <summary>A declared function returning a string.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>Its invariant text.</returns>
    [Deterministic]
    public static string Text(double x) => x.ToString(CultureInfo.InvariantCulture);

    /// <summary>A declared function taking a callback, which the declaration cannot speak for.</summary>
    /// <param name="function">The callback.</param>
    /// <param name="x">The argument.</param>
    /// <returns><c>function(x)</c>.</returns>
    [Deterministic]
    public static double Apply(Func<double, double> function, double x) => function(x);

    /// <summary>A declared function taking a reference, which it may write.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><paramref name="x"/>.</returns>
    [Deterministic]
    public static double ByRef(ref double x) => x;

    /// <summary>A declared generic identity.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    [Deterministic]
    public static T Identity<T>(T value) => value;

    /// <summary>A user function named like a trusted numeric function.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>A value unrelated to the sine.</returns>
    public static double Sin(double x) => x + Counter++;

    /// <summary>A user extension method named like a trusted numeric function.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>A value unrelated to the square root.</returns>
    public static double Sqrt(this double x) => x + Counter++;

    /// <summary>The method of <paramref name="name"/>.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    public static MethodInfo Method(string name) => typeof(BehaviorMethods).GetMethod(name)!;
}

/// <summary>A user value type with immutable fields: no mutable state is reachable from a value.</summary>
public readonly struct Money
{
    /// <summary>Initializes an amount.</summary>
    /// <param name="amount">The amount.</param>
    public Money(decimal amount) => Amount = amount;

    /// <summary>Gets the amount.</summary>
    public decimal Amount { get; }

    /// <summary>A declared custom operator.</summary>
    /// <param name="left">The left amount.</param>
    /// <param name="right">The right amount.</param>
    /// <returns>The sum.</returns>
    [Deterministic]
    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    /// <summary>An undeclared custom operator.</summary>
    /// <param name="left">The left amount.</param>
    /// <param name="right">The right amount.</param>
    /// <returns>The difference.</returns>
    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    /// <summary>A declared custom conversion.</summary>
    /// <param name="amount">The amount.</param>
    [Deterministic]
    public static implicit operator Money(decimal amount) => new(amount);

    /// <summary>An undeclared custom conversion.</summary>
    /// <param name="money">The money.</param>
    public static explicit operator decimal(Money money) => money.Amount;
}

/// <summary>
/// A user implementation of static numeric interfaces (<see cref="IAdditionOperators{TSelf, TOther, TResult}"/>,
/// <see cref="IMultiplyOperators{TSelf, TOther, TResult}"/>) with members named like trusted functions: none is trusted.
/// </summary>
public readonly struct UserNumber : IAdditionOperators<UserNumber, UserNumber, UserNumber>, IMultiplyOperators<UserNumber, UserNumber, UserNumber>
{
    /// <summary>Initializes a number.</summary>
    /// <param name="value">The value.</param>
    public UserNumber(double value) => Value = value;

    /// <summary>Gets the value.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public static UserNumber operator +(UserNumber left, UserNumber right) => new(left.Value + right.Value + BehaviorMethods.Counter++);

    /// <inheritdoc/>
    public static UserNumber operator *(UserNumber left, UserNumber right) => new(left.Value * right.Value);

    /// <summary>Named like <see cref="Math.Abs(double)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A value.</returns>
    public static UserNumber Abs(UserNumber value) => new(Math.Abs(value.Value));
}

/// <summary>A reference type with an annotated getter, an unannotated getter and fields.</summary>
public sealed class BehaviorCell
{
    /// <summary>A static read-only field.</summary>
    public static readonly double StaticFixed = 3.0;

    /// <summary>A mutable static field.</summary>
    public static double StaticMutable = 3.0;

    /// <summary>A mutable field.</summary>
    public double Field = 1.0;

    /// <summary>A read-only field.</summary>
    public readonly double Fixed = 2.0;

    /// <summary>A constant.</summary>
    public const double Constant = 5.0;

    /// <summary>Gets <see cref="Field"/> through a declared getter.</summary>
    public double Declared
    {
        [Deterministic]
        get => Field;
    }

    /// <summary>Gets <see cref="Field"/> through an undeclared getter.</summary>
    public double Undeclared => Field;
}

/// <summary>A non-sealed class with virtual and non-virtual declared methods.</summary>
public class BehaviorBase
{
    /// <summary>A declared virtual method, which an override could replace.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><paramref name="x"/>.</returns>
    [Deterministic]
    public virtual double Virtual(double x) => x;

    /// <summary>A declared non-virtual method.</summary>
    /// <param name="x">The argument.</param>
    /// <returns><paramref name="x"/>.</returns>
    [Deterministic]
    public double NonVirtual(double x) => x;
}

/// <summary>A sealed override, bound to the code that runs.</summary>
public sealed class BehaviorDerived : BehaviorBase
{
    /// <inheritdoc/>
    [Deterministic]
    public override double Virtual(double x) => x;
}

