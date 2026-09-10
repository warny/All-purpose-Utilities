using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization coverage for <c>ExpressionSimplifier.Math.TransformCall</c>, the private helper
/// shared by every <c>double</c> static-method conversion rule (<c>Sqrt</c>, <c>Max</c>, <c>Clamp</c>,
/// and about twenty others). It currently builds its method-lookup signature via
/// <c>Enumerable.Repeat(...).ToArray()</c> and converts its arguments via <c>Select(...).ToArray()</c>.
/// These tests lock in every observable behavior of that helper (argument order, argument-instance
/// reuse for already-<c>double</c> expressions, non-mutation of the caller's array, exact validation
/// and exception ordering) so that a later LINQ-removal change can be verified not to alter anything
/// beyond the removed iterator/delegate machinery.
/// </summary>
[TestClass]
public class ExpressionSimplifierMathTransformCallTests
{
    /// <summary>
    /// Exposes a handful of <see cref="ExpressionSimplifier"/>'s protected <c>double</c>-conversion
    /// rules as public wrappers, without changing their accessibility on the production type. Covers
    /// one rule of each arity actually used by <c>TransformCall</c>: <c>Sqrt</c> (1 argument),
    /// <c>Max</c> (2 arguments), and <c>Clamp</c> (3 arguments).
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
}
