using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// A partial class of <see cref="ExpressionSimplifier"/> providing transformations
/// that convert <see cref="double"/> static numeric calls (e.g. <see cref="double.Pow"/>)
/// into corresponding expression nodes or interface-based calls
/// such as <see cref="INumber{T}"/> or <see cref="ITrigonometricFunctions{T}"/>.
/// </summary>
public partial class ExpressionSimplifier
{
    private static readonly Type FloatingPointType = typeof(double);

    #region Power

    /// <summary>
    /// Transforms a call to <see cref="double.Pow"/> into an <see cref="Expression.Power(Expression, Expression)"/> node.
    /// </summary>
    /// <param name="e">The original expression containing the call.</param>
    /// <param name="left">An <see cref="Expression"/> representing the base.</param>
    /// <param name="right">An <see cref="Expression"/> representing the exponent.</param>
    /// <returns>
    /// A new <see cref="Expression"/> node representing <c>left^right</c>, potentially simplified further.
    /// </returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Pow))]
    protected Expression PowerConversionMath(Expression e, Expression left, Expression right)
    {
        return Transform(Expression.Power(left, right));
    }

    #endregion

    #region TransformTo INumber functions

    /// <summary>
    /// Builds a method call on the <see cref="double"/> type for the given
    /// <paramref name="functionName"/>, passing <paramref name="expressions"/> as arguments.
    /// </summary>
    /// <param name="requiredInterface">
    /// Interface used to validate the floating-point type capabilities (for example <c>IFloatingPoint&lt;double&gt;</c>).
    /// </param>
    /// <param name="functionName">The name of the method to call.</param>
    /// <param name="e">The original expression node (for reference).</param>
    /// <param name="expressions">An array of argument expressions to pass into the method call.</param>
    /// <returns>
    /// A transformed <see cref="Expression.Call(MethodInfo, Expression[])"/> targeting the floating-point method.
    /// </returns>
    private Expression TransformCall(Type requiredInterface, string functionName, Expression e, Expression[] expressions)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(requiredInterface);
        ArgumentNullException.ThrowIfNull(expressions);

        if (!requiredInterface.IsAssignableFrom(FloatingPointType))
        {
            throw new InvalidOperationException($"{FloatingPointType} does not implement {requiredInterface}.");
        }

        Type[] signature = Enumerable.Repeat(FloatingPointType, expressions.Length).ToArray();
        MethodInfo? method = FloatingPointType.GetMethod(functionName, BindingFlags.Public | BindingFlags.Static, signature);
        if (method is null)
        {
            throw new InvalidOperationException($"The method {functionName} could not be located on {FloatingPointType}.");
        }

        Expression[] convertedExpressions = expressions
            .Select(static expression => expression.Type == FloatingPointType
                ? expression
                : Expression.Convert(expression, FloatingPointType))
            .ToArray();

        return Expression.Call(method, convertedExpressions);
    }

    /// <summary>
    /// Converts a call to <see cref="double.Sign(double)"/> into a call to <see cref="INumber{T}.Sign"/>.
    /// </summary>
    /// <param name="e">The original expression node containing the <c>double.Sign</c> call.</param>
    /// <param name="expressions">The argument expressions extracted from the call.</param>
    /// <returns>A call to <c>INumber&lt;double&gt;.Sign</c> wrapped in the transformation pipeline.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sign))]
    protected Expression SignConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Sign), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Min(double, double)"/> into a call to <see cref="INumber{T}.Min"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Min))]
    protected Expression MinConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Min), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Max(double, double)"/> into a call to <see cref="INumber{T}.Max"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Max))]
    protected Expression MaxConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Max), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Clamp(double, double, double)"/> into a call to <see cref="INumber{T}.Clamp"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Clamp))]
    protected Expression ClampConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Clamp), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Abs(double)"/> into a call to <see cref="T:IFloatingPoint{T}.Abs(T)"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Abs))]
    protected Expression AbsConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Abs), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Round(double)"/> into a call to <see cref="IFloatingPoint{T}.Round(T)"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Round))]
    protected Expression RoundConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Round), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Floor(double)"/> into a call to <see cref="IFloatingPoint{T}.Floor"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Floor))]
    protected Expression FloorConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Floor), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Ceiling(double)"/> into a call to <see cref="IFloatingPoint{T}.Ceiling"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Ceiling))]
    protected Expression CeilingConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Ceiling), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Sqrt(double)"/> into a call to <see cref="IRootFunctions{T}.Sqrt"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Sqrt))]
    protected Expression SqrtConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Sqrt), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Cbrt(double)"/> into a call to <see cref="IRootFunctions{T}.Cbrt"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Cbrt))]
    protected Expression CbrtConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Cbrt), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Log(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log(T)"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Log))]
    protected Expression LogConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ILogarithmicFunctions<double>), nameof(ILogarithmicFunctions<double>.Log), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Log2(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log2"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Log2))]
    protected Expression Log2ConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ILogarithmicFunctions<double>), nameof(ILogarithmicFunctions<double>.Log2), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Log10(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log10"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    protected Expression Log10ConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ILogarithmicFunctions<double>), nameof(ILogarithmicFunctions<double>.Log10), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Cos(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Cos"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    protected Expression CosConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Cos), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Sin(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Sin"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    protected Expression SinConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Sin), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Tan(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Tan"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    protected Expression TanConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Tan), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Acos(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Acos"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Acos))]
    protected Expression ACosConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Acos), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Asin(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Asin"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Asin))]
    protected Expression ASinConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Asin), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Atan(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Atan"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Atan))]
    protected Expression ATanConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Atan), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Cosh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Cosh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    protected Expression CoshConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Cosh), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Sinh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Sinh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    protected Expression SinhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Sinh), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Tanh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Tanh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    protected Expression TanhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Tanh), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Acosh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Acosh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Acosh))]
    protected Expression ACoshConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Acosh), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Asinh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Asinh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Asinh))]
    protected Expression ASinhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Asinh), e, expressions);

    /// <summary>
    /// Converts a call to <see cref="double.Atanh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Atanh"/>.
    /// </summary>
    [ExpressionCallSignature(typeof(double), nameof(double.Atanh))]
    protected Expression ATanhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Atanh), e, expressions);

    #endregion
}
