using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Mathematics.Expressions;

public partial class ExpressionSimplifier
{
    #region Power

    [ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
    protected Expression PowerConversionMath(Expression e, Expression left, Expression right)
    {
        return Transform(Expression.Power(left, right));
    }
    #endregion

    #region TransformTo INumber functions
    private Expression TransformCall(Type type, string functionName, Expression e, Expression[] expressions)
    {
        var f = type.GetMethod(functionName, BindingFlags.Public | BindingFlags.Static, [typeof(double)]);
        return Transform(Expression.Call(f, expressions));
    }

    [ExpressionCallSignature(typeof(Math), nameof(Math.Sign))]
    protected Expression SignConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Sign), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Min))]
    protected Expression MinConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Min), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Max))]
    protected Expression MaxConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Max), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Clamp))]
    protected Expression ClampConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(INumber<double>), nameof(INumber<double>.Clamp), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Abs))]
    protected Expression AbsConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Abs), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Round))]
    protected Expression RoundConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Round), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Floor))]
    protected Expression FloorConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Floor), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Ceiling))]
    protected Expression CeilingConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IFloatingPoint<double>), nameof(IFloatingPoint<double>.Ceiling), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Sqrt))]
    protected Expression SqrtConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Sqrt), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Cbrt))]
    protected Expression CbrtConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(IRootFunctions<double>), nameof(IRootFunctions<double>.Cbrt), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Log))]
    protected Expression LogConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Log2))]
    protected Expression Log2ConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log2), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Log10))]
    protected Expression Log10ConversionMath(Expression e, Expression[] expressions)
    => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ILogarithmicFunctions<double>.Log10), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Cos))]
    protected Expression CosConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Cos), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Sin))]
    protected Expression SinConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Sin), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Tan))]
    protected Expression TanConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Tan), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Acos))]
    protected Expression ACosConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Acos), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Asin))]
    protected Expression ASinConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Asin), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Atan))]
    protected Expression ATanConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(ITrigonometricFunctions<double>), nameof(ITrigonometricFunctions<double>.Atan), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Cosh))]
    protected Expression CoshConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Cosh), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Sinh))]
    protected Expression SinhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Sinh), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Tanh))]
    protected Expression TanhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Tanh), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Acosh))]
    protected Expression ACoshConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Acosh), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Asinh))]
    protected Expression ASinhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Asinh), e, expressions);

    [ExpressionCallSignature(typeof(Math), nameof(Math.Atanh))]
    protected Expression ATanhConversionMath(Expression e, Expression[] expressions)
        => TransformCall(typeof(IHyperbolicFunctions<double>), nameof(IHyperbolicFunctions<double>.Atanh), e, expressions);

    #endregion

}