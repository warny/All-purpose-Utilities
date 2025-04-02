using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// A partial class of <see cref="ExpressionSimplifier"/> providing transformations
/// that convert certain <see cref="Math"/> method calls (e.g. <see cref="Math.Pow"/>)
/// into corresponding expression nodes or calls to other numeric interfaces
/// such as <see cref="INumber{T}"/> or <see cref="ITrigonometricFunctions{T}"/>.
/// </summary>
public partial class ExpressionSimplifier
{
	#region Power

	/// <summary>
	/// Transforms a call to <see cref="Math.Pow"/> into an <see cref="Expression.Power"/> node.
	/// </summary>
	/// <param name="e">The original expression containing the call.</param>
	/// <param name="left">An <see cref="Expression"/> representing the base.</param>
	/// <param name="right">An <see cref="Expression"/> representing the exponent.</param>
	/// <returns>
	/// A new <see cref="Expression"/> node representing <c>left^right</c>, potentially simplified further.
	/// </returns>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
	protected Expression PowerConversionMath(Expression e, Expression left, Expression right)
	{
		return Transform(Expression.Power(left, right));
	}

	#endregion

	#region TransformTo INumber functions

	/// <summary>
	/// Builds a method call on the specified <paramref name="type"/> for the given
	/// <paramref name="functionName"/>, passing <paramref name="expressions"/> as arguments.
	/// </summary>
	/// <param name="type">
	/// The <see cref="Type"/> declaring the target static method (e.g. <c>INumber&lt;double&gt;</c>).
	/// </param>
	/// <param name="functionName">The name of the method to call.</param>
	/// <param name="e">The original expression node (for reference).</param>
	/// <param name="expressions">An array of argument expressions to pass into the method call.</param>
	/// <returns>
	/// A transformed <see cref="Expression.Call"/> targeting the specified method.
	/// </returns>
	private Expression TransformCall(Type type, string functionName, Expression e, Expression[] expressions)
	{
		var f = type.GetMethod(functionName, BindingFlags.Public | BindingFlags.Static, [typeof(double)]);
		return Transform(Expression.Call(f, expressions));
	}

	/// <summary>
	/// Converts a call to <see cref="Math.Sign(double)"/> into a call to <see cref="INumber{T}.Sign"/>.
	/// </summary>
	/// <param name="e">The original expression node containing the <c>Math.Sign</c> call.</param>
	/// <param name="expressions">The argument expressions extracted from the call.</param>
	/// <returns>A call to <c>INumber&lt;double&gt;.Sign</c> wrapped in the transformation pipeline.</returns>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Sign))]
	protected Expression SignConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(INumber<double>), nameof(INumber<double>.Sign), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Min(double, double)"/> into a call to <see cref="INumber{T}.Min"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Min))]
	protected Expression MinConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(INumber<double>), nameof(INumber<double>.Min), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Max(double, double)"/> into a call to <see cref="INumber{T}.Max"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Max))]
	protected Expression MaxConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(INumber<double>), nameof(INumber<double>.Max), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Clamp(double, double, double)"/> into a call to <see cref="INumber{T}.Clamp"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Clamp))]
	protected Expression ClampConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(INumber<double>), nameof(INumber<double>.Clamp), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Abs(double)"/> into a call to <see cref="IFloatingPoint{T}.Abs"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Abs))]
	protected Expression AbsConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Abs), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Round(double)"/> into a call to <see cref="IFloatingPoint{T}.Round"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Round))]
	protected Expression RoundConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Round), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Floor(double)"/> into a call to <see cref="IFloatingPoint{T}.Floor"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Floor))]
	protected Expression FloorConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Floor), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Ceiling(double)"/> into a call to <see cref="IFloatingPoint{T}.Ceiling"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Ceiling))]
	protected Expression CeilingConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Ceiling), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Sqrt(double)"/> into a call to <see cref="IRootFunctions{T}.Sqrt"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Sqrt))]
	protected Expression SqrtConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Sqrt), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Cbrt(double)"/> into a call to <see cref="IRootFunctions{T}.Cbrt"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Cbrt))]
	protected Expression CbrtConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Cbrt), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Log(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Log))]
	protected Expression LogConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Log2(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log2"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Log2))]
	protected Expression Log2ConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log2), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Log10(double)"/> into a call to <see cref="ILogarithmicFunctions{T}.Log10"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Log10))]
	protected Expression Log10ConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log10), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Cos(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Cos"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Cos))]
	protected Expression CosConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Cos), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Sin(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Sin"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Sin))]
	protected Expression SinConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Sin), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Tan(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Tan"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Tan))]
	protected Expression TanConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Tan), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Acos(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Acos"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Acos))]
	protected Expression ACosConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Acos), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Asin(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Asin"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Asin))]
	protected Expression ASinConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Asin), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Atan(double)"/> into a call to <see cref="ITrigonometricFunctions{T}.Atan"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Atan))]
	protected Expression ATanConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Atan), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Cosh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Cosh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Cosh))]
	protected Expression CoshConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Cosh), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Sinh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Sinh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Sinh))]
	protected Expression SinhConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Sinh), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Tanh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Tanh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Tanh))]
	protected Expression TanhConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Tanh), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Acosh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Acosh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Acosh))]
	protected Expression ACoshConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Acosh), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Asinh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Asinh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Asinh))]
	protected Expression ASinhConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Asinh), e, expressions);

	/// <summary>
	/// Converts a call to <see cref="Math.Atanh(double)"/> into a call to <see cref="IHyperbolicFunctions{T}.Atanh"/>.
	/// </summary>
	[ExpressionCallSignature(typeof(Math), nameof(Math.Atanh))]
	protected Expression ATanhConversionMath(Expression e, Expression[] expressions)
		=> TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Atanh), e, expressions);

	#endregion
}
