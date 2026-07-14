using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Coverage for the structured, non-throwing symbolic transformation results
/// (<see cref="MathExpressionExtensions.TryDerivate{T}"/> / <see cref="MathExpressionExtensions.TryIntegrate{T}"/>),
/// resolving TODO-2026-07-11-pass3.md item #42.
/// </summary>
[TestClass]
public class SymbolicTransformationResultTests
{
    /// <summary>An unknown single-argument double function with no registered derivative rule.</summary>
    private static double CustomUnknown(double x) => x * x * x + 1.0;

    [TestMethod]
    public void TryDerivate_ExactRule_ReportsSuccessAndExact()
    {
        Expression<Func<double, double>> f = x => x * x * x - 2 * x;

        var result = f.TryDerivate<double>("x");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.Success, result.Status);
        Assert.IsTrue(result.IsExact);
        Assert.IsNotNull(result.Expression);
        double value = (double)result.Expression.Compile().DynamicInvoke(3.0)!;
        Assert.AreEqual(25.0, value, 1e-9); // 3x^2 - 2 at x=3
    }

    [TestMethod]
    public void TryDerivate_NumericFallback_ReportsSuccessButNotExact()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Call(
            typeof(SymbolicTransformationResultTests).GetMethod(nameof(CustomUnknown), BindingFlags.NonPublic | BindingFlags.Static)!,
            x);
        var f = Expression.Lambda<Func<double, double>>(body, x);

        var result = f.TryDerivate<double>("x", allowNumericalFallback: true);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.IsExact, "A finite-difference fallback result must report IsExact=false.");
        Assert.IsNotNull(result.Expression);
    }

    [TestMethod]
    public void TryDerivate_UnknownMethodFallbackDisabled_ReportsUnsupportedExpression()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Call(
            typeof(SymbolicTransformationResultTests).GetMethod(nameof(CustomUnknown), BindingFlags.NonPublic | BindingFlags.Static)!,
            x);
        var f = Expression.Lambda<Func<double, double>>(body, x);

        var result = f.TryDerivate<double>("x"); // fallback disabled (default)

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.UnsupportedExpression, result.Status);
        Assert.IsFalse(result.IsExact);
        // UnsupportedNode is populated when known: the offending unknown method call (the transformer
        // works on a rebuilt copy of the node, so compare by method rather than by reference).
        Assert.IsInstanceOfType(result.UnsupportedNode, typeof(MethodCallExpression));
        Assert.AreEqual(nameof(CustomUnknown), ((MethodCallExpression)result.UnsupportedNode!).Method.Name);
    }

    [TestMethod]
    public void TryDerivate_MathFunctionUnavailableForDecimal_ReportsUnsupportedScalarOperation()
    {
        // decimal has no Exp; deriving a double.Exp call node against a decimal transformer.
        var x = Expression.Parameter(typeof(decimal), "x");
        var expCall = Expression.Call(typeof(double).GetMethod(nameof(double.Exp), new[] { typeof(double) })!,
            Expression.Convert(x, typeof(double)));
        var f = Expression.Lambda<Func<decimal, double>>(expCall, x);

        var result = f.TryDerivate<decimal>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.UnsupportedScalarOperation, result.Status);
        Assert.IsInstanceOfType(result.InnerException, typeof(UnsupportedScalarOperationException));
    }

    [TestMethod]
    public void TryDerivate_AmbiguousParameter_ReportsInvalidInput()
    {
        var x1 = Expression.Parameter(typeof(double), "x");
        var x2 = Expression.Parameter(typeof(double), "x");
        var f = Expression.Lambda<Func<double, double, double>>(Expression.Add(x1, x2), x1, x2);

        var result = f.TryDerivate<double>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    [TestMethod]
    public void TryDerivate_ForeignParameter_ReportsInvalidInput()
    {
        Expression<Func<double, double>> f = x => x * x;

        var result = f.TryDerivate<double>("y"); // not declared in the lambda

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    [TestMethod]
    public void TryDerivate_UnsupportedConversion_ReportsUnsupportedExpression()
    {
        // double -> float is a reducing conversion with no well-defined symbolic derivative.
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Convert(x, typeof(float));
        var f = Expression.Lambda<Func<double, float>>(body, x);

        var result = f.TryDerivate<double>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.UnsupportedExpression, result.Status);
        Assert.IsNotNull(result.UnsupportedNode);
    }

    [TestMethod]
    public void TryDerivate_UnknownExpressionType_ReportsUnsupportedExpression()
    {
        // Math.Max is a two-argument call: no derivative rule matches and it does not fit the
        // single-argument finite-difference fallback shape, so it is reported as unsupported.
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Call(
            typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(double), typeof(double) })!,
            x, Expression.Constant(1.0));
        var f = Expression.Lambda<Func<double, double>>(body, x);

        var result = f.TryDerivate<double>("x", allowNumericalFallback: true);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.UnsupportedExpression, result.Status);
        Assert.IsNotNull(result.UnsupportedNode);
    }

    [TestMethod]
    public void TryDerivate_NullLambda_ReportsInvalidInput()
    {
        LambdaExpression? f = null;

        var result = f!.TryDerivate<double>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    [TestMethod]
    public void TryIntegrate_ExactRule_ReportsSuccessAndExact()
    {
        Expression<Func<double, double>> f = x => 3 * x;

        var result = f.TryIntegrate<double>("x");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsExact);
        Assert.IsNotNull(result.Expression);
        double value = (double)result.Expression.Compile().DynamicInvoke(2.0)!;
        Assert.AreEqual(6.0, value, 1e-9); // integral of 3x = 1.5 x^2, at x=2 -> 6
    }

    [TestMethod]
    public void TryIntegrate_NoRule_ReportsUnsupportedExpression()
    {
        // No integration rule for an arbitrary unknown method call.
        var x = Expression.Parameter(typeof(double), "x");
        var body = Expression.Call(
            typeof(SymbolicTransformationResultTests).GetMethod(nameof(CustomUnknown), BindingFlags.NonPublic | BindingFlags.Static)!,
            x);
        var f = Expression.Lambda<Func<double, double>>(body, x);

        var result = f.TryIntegrate<double>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.UnsupportedExpression, result.Status);
        Assert.IsNotNull(result.UnsupportedNode);
    }

    [TestMethod]
    public void TryIntegrate_ForeignParameter_ReportsInvalidInput()
    {
        Expression<Func<double, double>> f = x => x;

        var result = f.TryIntegrate<double>("y");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }
}
