using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;
using Utils.Reflection;

namespace Utils.Mathematics.Expressions;

public partial class ExpressionSimplifier
{
    #region Power

    /// <summary>
    /// Replaces calls to <see cref="IPowerFunctions{T}.Pow"/> with the equivalent <see cref="Expression.Power(Expression, Expression)"/> node.
    /// </summary>
    /// <param name="e">Original call expression.</param>
    /// <param name="left">Base argument of the power operation.</param>
    /// <param name="right">Exponent argument of the power operation.</param>
    /// <returns>The transformed power expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(IPowerFunctions<double>.Pow))]
    protected Expression PowerConvertionNumber1(Expression e, Expression left, Expression right)
    {
        return Transform(Expression.Power(left, right));
    }

    /// <summary>
    /// Replaces calls to <see cref="Math.Pow(double, double)"/> with an <see cref="Expression.Power(Expression, Expression)"/> node.
    /// </summary>
    /// <param name="e">Original call expression.</param>
    /// <param name="left">Base argument of the power operation.</param>
    /// <param name="right">Exponent argument of the power operation.</param>
    /// <returns>The transformed power expression.</returns>
    [ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
    protected Expression PowerConvertionNumber2(Expression e, Expression left, Expression right)
    {
        return Transform(Expression.Power(left, right));
    }
    #endregion

    #region logarithm 

    /// <summary>
    /// Simplifies the addition of two natural logarithms by turning it into a single logarithm of a product.
    /// </summary>
    /// <param name="e">The original binary addition expression.</param>
    /// <param name="left">Left logarithm call.</param>
    /// <param name="right">Right logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression LogarithmSimplificationAddNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
    {
        return Expression.Call(
                typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log", [left.Type]),
                Transform(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the subtraction of two natural logarithms by turning it into a single logarithm of a quotient.
    /// </summary>
    /// <param name="e">The original binary subtraction expression.</param>
    /// <param name="left">Left logarithm call.</param>
    /// <param name="right">Right logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    protected Expression LogarithmSimplificationSubstractNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
    {
        return Expression.Call(
    typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log", [left.Type]),
    Transform(Expression.Divide(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the addition of two base-10 logarithms by turning it into a single logarithm of a product.
    /// </summary>
    /// <param name="e">The original binary addition expression.</param>
    /// <param name="left">Left base-10 logarithm call.</param>
    /// <param name="right">Right base-10 logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    protected Expression Logarithm10SimplificationAddNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
    {
        return Expression.Call(
    typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log10", [left.Type]),
    Transform(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the subtraction of two base-10 logarithms by turning it into a single logarithm of a quotient.
    /// </summary>
    /// <param name="e">The original binary subtraction expression.</param>
    /// <param name="left">Left base-10 logarithm call.</param>
    /// <param name="right">Right base-10 logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    protected Expression Logarithm10SimplificationSubstractNumber(Expression e,
            [ExpressionCallSignature(typeof(Math), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(Math), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
    {
        return Expression.Call(
            typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log10", [left.Type]),
            Transform(Expression.Divide(left.Arguments[0], right.Arguments[0])));
    }

    #endregion

    #region trigonometry
    private class TrigonometricMethods
    {

        public Type ReferenceType { get; }
        public MethodInfo Cos { get; }
        public MethodInfo Sin { get; }
        public MethodInfo Tan { get; }

        public TrigonometricMethods(Type type)
        {
            ReferenceType = typeof(ITrigonometricFunctions<>).MakeGenericType(type);
            Cos = type.GetMethod(nameof(ITrigonometricFunctions<double>.Cos), BindingFlags.Static | BindingFlags.Public, [type]);
            Sin = type.GetMethod(nameof(ITrigonometricFunctions<double>.Sin), BindingFlags.Static | BindingFlags.Public, [type]);
            Tan = type.GetMethod(nameof(ITrigonometricFunctions<double>.Tan), BindingFlags.Static | BindingFlags.Public, [type]);
        }

        public void Deconstruct(out MethodInfo cos, out MethodInfo sin, out MethodInfo tan)
        {
            cos = Cos;
            sin = Sin;
            tan = Tan;
        }
    }

    private IDictionary<Type, TrigonometricMethods> TrigonometricMethodsByType { get; }
        = new CachedLoader<Type, TrigonometricMethods>((Type type, out TrigonometricMethods methods) => { methods = new TrigonometricMethods(type); return true; });

    /// <summary>
    /// Applies the trigonometric identity <c>sin²(x) + cos²(x) = 1</c> when possible.
    /// </summary>
    /// <param name="e">The original addition expression.</param>
    /// <param name="left">Left power expression.</param>
    /// <param name="right">Right power expression.</param>
    /// <returns>A constant expression representing one when the identity applies; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression AdditionOfCos2andSin2Number(Expression e,
            [ExpressionSignature(ExpressionType.Power)] BinaryExpression left,
            [ExpressionSignature(ExpressionType.Power)] BinaryExpression right)
    {
        if (!left.Type.IsOfGenericType(typeof(ITrigonometricFunctions<>))) return null;
        if (right.Type != left.Type) return null;

        var leftRight = left.Right as ConstantExpression;
        var rightRight = right.Right as ConstantExpression;
        if (leftRight.Value as double? != 2 || rightRight.Value as double? != 2) return null;

        if (left.Left is not MethodCallExpression leftLeft) return null;
        if (right.Left is not MethodCallExpression rightLeft) return null;

        (var cos, var sin, _) = TrigonometricMethodsByType[left.Type];

        if (!(leftLeft.Method == sin && rightLeft.Method == cos) && !(leftLeft.Method == cos && rightLeft.Method == sin)) return null;

        if (ExpressionComparer.Default.Equals(leftLeft.Arguments[0], rightLeft.Arguments[0])) return Expression.Constant(Convert.ChangeType(1.0, left.Type));

        return null;
    }

    /// <summary>
    /// Rewrites <c>sin(x) / cos(x)</c> into <c>tan(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Sine call in the numerator.</param>
    /// <param name="right">Cosine call in the denominator.</param>
    /// <returns>An expression that calls tangent when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfCosAndSinNumber(Expression e,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression right)
    {
        if (ExpressionComparer.Default.Equals(left.Arguments[0], right.Arguments[0]))
        {
            var tan = TrigonometricMethodsByType[left.Type].Tan;
            return Expression.Call(null, tan, left.Arguments[0]);
        }
        return null;
    }

    /// <summary>
    /// Rewrites <c>cos(x) / sin(x)</c> into <c>cot(x)</c> (expressed as <c>1 / tan(x)</c>) when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Cosine call in the numerator.</param>
    /// <param name="right">Sine call in the denominator.</param>
    /// <returns>An expression rewriting the division when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfSinAndCosNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression right)
    {
        if (ExpressionComparer.Default.Equals(left.Arguments[0], right.Arguments[0]))
        {
            var tan = TrigonometricMethodsByType[left.Type].Tan;
            return Expression.Divide(Expression.Constant(Convert.ChangeType(1.0, left.Type)), Expression.Call(null, tan, left.Arguments[0]));
        }
        return null;
    }

    /// <summary>
    /// Rewrites <c>cos(x) * tan(x)</c> into <c>sin(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original multiplication expression.</param>
    /// <param name="left">Cosine call.</param>
    /// <param name="right">Tangent call.</param>
    /// <returns>A sine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    protected Expression MultiplicationOfCosAndTanNumber(Expression e,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression right)
    {
        if (ExpressionComparer.Default.Equals(left.Arguments[0], right.Arguments[0]))
        {
            var sin = TrigonometricMethodsByType[left.Type].Sin;
            return Expression.Call(null, sin, left.Arguments[0]);
        }
        return null;
    }

    /// <summary>
    /// Rewrites <c>tan(x) * cos(x)</c> into <c>sin(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original multiplication expression.</param>
    /// <param name="left">Tangent call.</param>
    /// <param name="right">Cosine call.</param>
    /// <returns>A sine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    protected Expression MultiplicationOfTanAndCosNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression right)
    {
        if (ExpressionComparer.Default.Equals(left.Arguments[0], right.Arguments[0]))
        {
            var sin = TrigonometricMethodsByType[left.Type].Sin;
            return Expression.Call(null, sin, left.Arguments[0]);
        }
        return null;
    }


    /// <summary>
    /// Rewrites <c>sin(x) / tan(x)</c> into <c>cos(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Sine call.</param>
    /// <param name="right">Tangent call.</param>
    /// <returns>A cosine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfSinAndTanNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression right)
    {
        if (ExpressionComparer.Default.Equals(left.Arguments[0], right.Arguments[0]))
        {
            var cos = TrigonometricMethodsByType[left.Type].Cos;
            return Expression.Call(null, cos, left.Arguments[0]);
        }
        return null;
    }

    #endregion

}
