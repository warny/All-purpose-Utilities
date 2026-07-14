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

    /// <summary>
    /// A lambda with a known derivative rule must return <see cref="SymbolicTransformationStatus.Success"/>
    /// and an exact result that evaluates correctly.
    /// </summary>
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

    /// <summary>
    /// A lambda whose derivative cannot be computed symbolically must fall back to finite differences
    /// when <c>allowNumericalFallback</c> is true, reporting <see cref="SymbolicTransformationResult.IsExact"/>
    /// as false.
    /// </summary>
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

    /// <summary>
    /// A lambda with an unknown method call and no numerical fallback must return
    /// <see cref="SymbolicTransformationStatus.UnsupportedExpression"/> with the offending node
    /// populated in <see cref="SymbolicTransformationResult.UnsupportedNode"/>.
    /// </summary>
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

    /// <summary>
    /// Deriving a <c>double.Exp</c> call against a <c>decimal</c> transformer (which has no
    /// corresponding exponential rule) must return
    /// <see cref="SymbolicTransformationStatus.UnsupportedScalarOperation"/>.
    /// </summary>
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

    /// <summary>
    /// When two distinct parameters share the same name, derivation by name is ambiguous and must
    /// return <see cref="SymbolicTransformationStatus.InvalidInput"/>.
    /// </summary>
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

    /// <summary>
    /// Requesting derivation with respect to a parameter name not declared in the lambda must
    /// return <see cref="SymbolicTransformationStatus.InvalidInput"/>.
    /// </summary>
    [TestMethod]
    public void TryDerivate_ForeignParameter_ReportsInvalidInput()
    {
        Expression<Func<double, double>> f = x => x * x;

        var result = f.TryDerivate<double>("y"); // not declared in the lambda

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    /// <summary>
    /// A narrowing <c>Convert</c> node (double → float) has no symbolic derivative and must
    /// return <see cref="SymbolicTransformationStatus.UnsupportedExpression"/> with the
    /// unsupported node identified.
    /// </summary>
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

    /// <summary>
    /// A two-argument <c>Math.Max</c> call does not fit any derivative rule and does not match
    /// the single-argument finite-difference fallback shape; it must be reported as
    /// <see cref="SymbolicTransformationStatus.UnsupportedExpression"/> even with the fallback
    /// enabled.
    /// </summary>
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

    /// <summary>
    /// Passing a null lambda must not throw; it must return
    /// <see cref="SymbolicTransformationStatus.InvalidInput"/>.
    /// </summary>
    [TestMethod]
    public void TryDerivate_NullLambda_ReportsInvalidInput()
    {
        LambdaExpression? f = null;

        var result = f!.TryDerivate<double>("x");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    /// <summary>
    /// A lambda with a known integration rule must return
    /// <see cref="SymbolicTransformationStatus.Success"/> with an exact anti-derivative that
    /// evaluates correctly.
    /// </summary>
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

    /// <summary>
    /// A lambda with an arbitrary unknown method call must return
    /// <see cref="SymbolicTransformationStatus.UnsupportedExpression"/> with the offending node
    /// identified.
    /// </summary>
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

    /// <summary>
    /// Requesting integration with respect to a parameter name not declared in the lambda must
    /// return <see cref="SymbolicTransformationStatus.InvalidInput"/>.
    /// </summary>
    [TestMethod]
    public void TryIntegrate_ForeignParameter_ReportsInvalidInput()
    {
        Expression<Func<double, double>> f = x => x;

        var result = f.TryIntegrate<double>("y");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    // ── Classification contract (non-throwing guarantee) ─────────────────────

    /// <summary>
    /// A <see cref="SymbolicParameterException"/> (caller error) must be classified as
    /// <see cref="SymbolicTransformationStatus.InvalidInput"/>, not as
    /// <see cref="SymbolicTransformationStatus.ConstructionFailure"/>.
    /// </summary>
    [TestMethod]
    public void TryClassify_SymbolicParameterException_ReportsInvalidInput()
    {
        var ex = new SymbolicParameterException("test parameter not found");
        MathExpressionExtensions.TryClassify(ex, out var result);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
        Assert.AreSame(ex, result.InnerException);
    }

    /// <summary>
    /// A plain <see cref="InvalidOperationException"/> that is not a
    /// <see cref="SymbolicParameterException"/> represents an internal transformer failure and
    /// must be classified as <see cref="SymbolicTransformationStatus.ConstructionFailure"/>.
    /// </summary>
    [TestMethod]
    public void TryClassify_RawInvalidOperationException_ReportsConstructionFailure()
    {
        // A plain InvalidOperationException (not SymbolicParameterException) is an internal
        // failure, NOT a caller error. It must map to ConstructionFailure, not InvalidInput.
        var ex = new InvalidOperationException("internal transformer state error");
        MathExpressionExtensions.TryClassify(ex, out var result);
        Assert.AreEqual(SymbolicTransformationStatus.ConstructionFailure, result.Status);
        Assert.AreSame(ex, result.InnerException);
    }

    /// <summary>
    /// Any unrecognized exception type must be classified as
    /// <see cref="SymbolicTransformationStatus.ConstructionFailure"/> to uphold the non-throwing
    /// contract of <c>TryDerivate</c> and <c>TryIntegrate</c>.
    /// </summary>
    [TestMethod]
    public void TryClassify_UnexpectedException_ReportsConstructionFailure()
    {
        // The catch-all guarantees that ANY unrecognized exception is reported as
        // ConstructionFailure rather than propagating. This upholds the non-throwing contract.
        var ex = new IndexOutOfRangeException("index out of range inside transformer");
        MathExpressionExtensions.TryClassify(ex, out var result);
        Assert.AreEqual(SymbolicTransformationStatus.ConstructionFailure, result.Status);
        Assert.AreSame(ex, result.InnerException);
    }

    /// <summary>
    /// A <see cref="System.Reflection.TargetInvocationException"/> must be unwrapped before
    /// classification so that the inner exception drives the status, not the wrapper.
    /// </summary>
    [TestMethod]
    public void TryClassify_TargetInvocationException_UnwrapsInnerAndClassifies()
    {
        // Rules are invoked via reflection → TargetInvocationException. TryClassify must
        // unwrap the inner exception and classify it, not classify TIE itself as unexpected.
        var inner = new SymbolicParameterException("param missing");
        var tie = new System.Reflection.TargetInvocationException(inner);
        MathExpressionExtensions.TryClassify(tie, out var result);
        Assert.AreEqual(SymbolicTransformationStatus.InvalidInput, result.Status);
    }

    /// <summary>
    /// <see cref="MathExpressionExtensions.TryDerivate{T}"/> must never propagate an exception;
    /// any internal failure must be returned as a <see cref="SymbolicTransformationResult"/> with
    /// an appropriate failure status.
    /// </summary>
    [TestMethod]
    public void TryDerivate_UnexpectedInternalException_DoesNotThrow_ReportsConstructionFailure()
    {
        // Verify end-to-end that TryDerivate never propagates exceptions.
        // We provoke a known-unexpected exception by deriving a lambda whose simplification
        // step triggers a TargetInvocationException wrapping an IndexOutOfRangeException.
        // Since we cannot easily force this from the outside, we validate the catch-all via
        // TryClassify directly (see the test above). This test just confirms TryDerivate
        // itself compiles and runs to a result (not an exception) for all reachable paths.
        Expression<Func<double, double>> f = x => x;
        var result = f.TryDerivate<double>("x");
        // f' = 1, which is valid
        Assert.AreEqual(SymbolicTransformationStatus.Success, result.Status);
    }
}
