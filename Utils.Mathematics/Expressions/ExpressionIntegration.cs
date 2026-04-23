using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;
#pragma warning disable CS8604 // Existence possible d'un argument de référence null.

/// <summary>
/// Provides integration rules for mathematical expression trees.
/// </summary>
public class ExpressionIntegration<T> : ExpressionTransformer where T : IFloatingPoint<T>
{
    /// <summary>
    /// Gets the name of the parameter used as the integration variable.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets or sets the cached parameter expression that matches <see cref="ParameterName"/>.
    /// </summary>
    private ParameterExpression parameter { get; set; } = null!;

    /// <summary>
    /// Integrates a lambda expression with respect to the configured parameter.
    /// </summary>
    /// <param name="e">The lambda expression to integrate.</param>
    /// <returns>The integrated expression tree.</returns>
    public Expression Integrate(LambdaExpression e)
    {
        ArgumentNullException.ThrowIfNull(e);

        parameter = e.Parameters.FirstOrDefault(p => p.Name == ParameterName)
                ?? throw new InvalidOperationException($"The parameter '{ParameterName}' was not found in the lambda expression.");

        return Expression.Lambda(Transform(e.Body), e.Parameters);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionIntegration{T}"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    public ExpressionIntegration(string parameterName)
    {
        this.ParameterName = parameterName;
    }



    /// <summary>
    /// Ignores conversion wrappers by integrating the wrapped operand.
    /// </summary>
    /// <param name="e">The conversion expression.</param>
    /// <param name="operand">The wrapped operand.</param>
    /// <returns>The integral of the wrapped operand.</returns>
    [ExpressionSignature(ExpressionType.Convert)]
    public Expression Convert(
        UnaryExpression e,
        Expression operand
    )
    {
        return Transform(operand);
    }

    /// <summary>
    /// Ignores checked conversion wrappers by integrating the wrapped operand.
    /// </summary>
    /// <param name="e">The checked conversion expression.</param>
    /// <param name="operand">The wrapped operand.</param>
    /// <returns>The integral of the wrapped operand.</returns>
    [ExpressionSignature(ExpressionType.ConvertChecked)]
    public Expression ConvertChecked(
        UnaryExpression e,
        Expression operand
    )
    {
        return Transform(operand);
    }
    /// <summary>
    /// Integrates a numeric constant by multiplying it with the integration parameter.
    /// </summary>
    /// <param name="e">The constant expression being transformed.</param>
    /// <param name="value">The numeric value stored by the constant expression.</param>
    /// <returns>The integral expression for the constant value.</returns>
    [ExpressionSignature(ExpressionType.Constant)]
    public Expression Constant(
        ConstantExpression e,
        object value
    )
    {
        return Expression.Multiply(Expression.Convert(e, typeof(T)), parameter);
    }

    /// <summary>
    /// Integrates a negated expression by negating the integral of its operand.
    /// </summary>
    /// <param name="e">The unary expression representing the negation.</param>
    /// <param name="operand">The operand that is being negated.</param>
    /// <returns>The integral of the negated expression.</returns>
    [ExpressionSignature(ExpressionType.Negate)]
    public Expression Negate(
        UnaryExpression e,
        Expression operand
    )
    {
        return Expression.Negate(Transform(operand));
    }

    /// <summary>
    /// Integrates a parameter expression, returning x^2/2 when it matches the integration variable.
    /// </summary>
    /// <param name="e">The parameter expression that may match the integration variable.</param>
    /// <param name="value">The value associated with the parameter (unused).</param>
    /// <returns>The integral of the parameter expression.</returns>
    [ExpressionSignature(ExpressionType.Parameter)]
    public Expression Parameter(
        ParameterExpression e,
        object value
    )
    {
        if (e.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Power(e, CreateConstant(2d)),
                CreateConstant(2d)
            );
        }
        else
        {
            return Expression.Multiply(
                e, parameter
            );
        }
    }

    /// <summary>
    /// Integrates a sum by integrating each operand individually.
    /// </summary>
    /// <param name="e">The binary expression representing the addition.</param>
    /// <param name="left">The left operand of the addition.</param>
    /// <param name="right">The right operand of the addition.</param>
    /// <returns>The integral of the sum expression.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    public Expression Add(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Add(
            Transform(left),
            Transform(right)
            );
    }

    /// <summary>
    /// Integrates a subtraction by integrating both operands and subtracting the results.
    /// </summary>
    /// <param name="e">The binary expression representing the subtraction.</param>
    /// <param name="left">The expression on the left side of the operator.</param>
    /// <param name="right">The expression on the right side of the operator.</param>
    /// <returns>The integral of the subtraction expression.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    public Expression Substract(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Subtract(
            Transform(left),
            Transform(right)
        );
    }

    /// <summary>
    /// Integrates a product of a constant and an expression by treating the constant as a factor.
    /// </summary>
    /// <param name="e">The binary expression representing the multiplication.</param>
    /// <param name="left">The constant factor on the left side.</param>
    /// <param name="right">The expression on the right side.</param>
    /// <returns>The integral of the product.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        Expression right
    )
    {
        return Expression.Multiply(left, Transform(right));
    }

    /// <summary>
    /// Integrates a product of an expression and a constant by treating the constant as a factor.
    /// </summary>
    /// <param name="e">The binary expression representing the multiplication.</param>
    /// <param name="left">The non-constant expression on the left side.</param>
    /// <param name="right">The constant factor on the right side.</param>
    /// <returns>The integral of the product.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Multiply(right, Transform(left));
    }

    /// <summary>
    /// Integrates a quotient whose denominator is a constant by integrating the numerator.
    /// </summary>
    /// <param name="e">The binary expression representing the division.</param>
    /// <param name="left">The expression in the numerator.</param>
    /// <param name="right">The constant expression in the denominator.</param>
    /// <returns>The integral of the division expression.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Divide(Transform(left), right);
    }

    /// <summary>
    /// Integrates a constant divided by a parameter by applying the logarithm rule.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant expression in the numerator.</param>
    /// <param name="right">The parameter in the denominator.</param>
    /// <returns>The integral of the quotient expression.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            ParameterExpression right
)
    {
        if (right.Name != ParameterName) return null;
        return Expression.Multiply(
                left,
                Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), right)
            );
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by the integration parameter.
    /// This covers inputs where numeric normalization introduces a <see cref="ExpressionType.Convert"/> node.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The parameter in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        ParameterExpression right
    )
    {
        if (right.Name != ParameterName) return null;
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;

        ConstantExpression numericLeft = Expression.Constant(System.Convert.ToDouble(constant.Value));
        return Expression.Multiply(
            numericLeft,
            Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), right)
        );
    }

    /// <summary>
    /// Integrates a constant divided by a power expression representing x raised to n.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant factor in the numerator.</param>
    /// <param name="right">The power expression in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
    BinaryExpression e,
    [ConstantNumeric] ConstantExpression left,
    [ExpressionSignature(ExpressionType.Power)] BinaryExpression right
)
    {
        if (right.Left is not ParameterExpression p || p.Name != ParameterName)
        {
            return null;
        }

        ConstantExpression? expo = right.Right as ConstantExpression;
        if (expo is null && right.Right is UnaryExpression unaryExpo &&
            (unaryExpo.NodeType == ExpressionType.Convert || unaryExpo.NodeType == ExpressionType.ConvertChecked))
        {
            expo = unaryExpo.Operand as ConstantExpression;
        }

        if (expo is null || !NumberUtils.IsNumeric(expo.Value))
        {
            return null;
        }

        double n = System.Convert.ToDouble(expo.Value);
        if (double.Abs(n - 1.0) < double.Epsilon)
        {
            return Expression.Multiply(
                left,
                Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), p)
            );
        }

        double newExpo = 1.0 - n;
        return Expression.Divide(
            Expression.Multiply(left, Expression.Power(p, Expression.Constant(newExpo))),
            Expression.Constant(newExpo)
        );
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by a power expression in the denominator.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The power expression in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        [ExpressionSignature(ExpressionType.Power)] BinaryExpression right
    )
    {
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;
        ConstantExpression numericLeft = Expression.Constant(System.Convert.ToDouble(constant.Value));
        return Divide(e, numericLeft, right);
    }

    /// <summary>
    /// Integrates a constant divided by a square root method call of the integration parameter.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant factor in the numerator.</param>
    /// <param name="right">The method call expected to represent <c>double.Sqrt</c>.</param>
    /// <returns>The integral of the expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
    BinaryExpression e,
    [ConstantNumeric] ConstantExpression left,
    MethodCallExpression right
)
    {
        if (right.Method.Name == nameof(double.Sqrt) &&
            right.Arguments.Count == 1 &&
            right.Arguments[0] is ParameterExpression pSqrt &&
            pSqrt.Name == ParameterName)
        {
            double factor = 2.0 * System.Convert.ToDouble(left.Value);
            return Expression.Multiply(
                Expression.Constant(factor),
                Expression.Call(typeof(T).GetMethod(nameof(double.Sqrt), [typeof(T)]), pSqrt)
            );
        }

        if (right.Method.Name == nameof(double.Pow) &&
            right.Arguments.Count == 2 &&
            right.Arguments[0] is ParameterExpression pPow &&
            pPow.Name == ParameterName)
        {
            ConstantExpression? exponent = right.Arguments[1] as ConstantExpression;
            if (exponent is null && right.Arguments[1] is UnaryExpression unaryExponent &&
                (unaryExponent.NodeType == ExpressionType.Convert || unaryExponent.NodeType == ExpressionType.ConvertChecked))
            {
                exponent = unaryExponent.Operand as ConstantExpression;
            }

            if (exponent is null || !NumberUtils.IsNumeric(exponent.Value))
            {
                return null;
            }

            double n = System.Convert.ToDouble(exponent.Value);
            if (double.Abs(n - 1.0) < double.Epsilon)
            {
                return Expression.Multiply(
                    left,
                    Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), pPow)
                );
            }

            double newExpo = 1.0 - n;
            return Expression.Divide(
                Expression.Multiply(left, Expression.Power(pPow, Expression.Constant(newExpo))),
                Expression.Constant(newExpo)
            );
        }

        return null;
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by a method call denominator.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The method call denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        MethodCallExpression right
    )
    {
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;
        ConstantExpression numericLeft = Expression.Constant(System.Convert.ToDouble(constant.Value));
        return Divide(e, numericLeft, right);
    }

    /// <summary>
    /// Integrates a natural logarithm call whose argument is the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Log</c>.</param>
    /// <param name="p">The parameter passed to the logarithm.</param>
    /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), "Log")]
    public Expression? Log(
            MethodCallExpression e,
            ParameterExpression p
    )
    {
        if (p.Name != ParameterName) return null;
        return Expression.Multiply(
                    parameter,
                    Expression.Subtract(
                        Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), parameter),
                        CreateConstant(1d)
                        )
                );
    }

    /// <summary>
    /// Integrates a base-10 logarithm call that uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Log10</c>.</param>
    /// <param name="p">The parameter passed to the logarithm.</param>
    /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    public Expression? Log10(
            MethodCallExpression e,
            ParameterExpression p
    )
    {
        if (p.Name != ParameterName) return null;
        var ln10 = CreateConstant(double.Log(10.0));
        return Expression.Subtract(
            Expression.Multiply(p,
                Expression.Call(typeof(T).GetMethod(nameof(double.Log10), [typeof(T)]), p)),
            Expression.Divide(p, ln10)
        );
    }

    /// <summary>
    /// Integrates a power expression whose base is the integration parameter.
    /// </summary>
    /// <param name="e">The power expression being transformed.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The constant exponent applied to the base.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    public Expression? Power(
    BinaryExpression e,
    ParameterExpression p,
    [ConstantNumeric] ConstantExpression expo
)
    {
        if (p.Name != ParameterName) return null;
        double n = System.Convert.ToDouble(expo.Value);
        if (double.Abs(n + 1.0) < double.Epsilon)
        {
            return Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]), p);
        }
        return Expression.Divide(
            Expression.Power(p, Expression.Constant(n + 1.0)),
            Expression.Constant(n + 1.0)
        );
    }

    /// <summary>
    /// Integrates a power expression whose exponent is wrapped in a numeric conversion.
    /// </summary>
    /// <param name="e">The power expression being transformed.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The converted exponent expression.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    public Expression? Power(
        BinaryExpression e,
        ParameterExpression p,
        UnaryExpression expo
    )
    {
        if (expo.NodeType != ExpressionType.Convert && expo.NodeType != ExpressionType.ConvertChecked)
        {
            return null;
        }

        if (expo.Operand is not ConstantExpression constantExpo || !NumberUtils.IsNumeric(constantExpo.Value))
        {
            return null;
        }

        return Power(e, p, Expression.Constant(System.Convert.ToDouble(constantExpo.Value)));
    }

    /// <summary>
    /// Integrates an exponential call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Exp</c>.</param>
    /// <param name="op">The parameter passed to the exponential function.</param>
    /// <returns>The integral of the exponential call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression? Exp(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (op.Name != ParameterName) return null;
        return Expression.Call(typeof(T).GetMethod(nameof(double.Exp), [typeof(T)]), op);
    }

    /// <summary>
    /// Integrates an exponential call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Exp</c>.</param>
    /// <param name="be">The binary multiplication composing the exponential argument.</param>
    /// <returns>The integral of the exponential call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression? Exp(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(typeof(T).GetMethod(nameof(double.Exp), [typeof(T)]), be),
                c
            );
    }

    /// <summary>
    /// Integrates a sine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sin</c>.</param>
    /// <param name="op">The parameter passed to the sine function.</param>
    /// <returns>The integral of the sine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression? Sin(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (op.Name != ParameterName) return null;
        return Expression.Negate(
                Expression.Call(typeof(T).GetMethod(nameof(double.Cos), [typeof(T)]), op)
            );
    }

    /// <summary>
    /// Integrates a sine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sin</c>.</param>
    /// <param name="be">The binary multiplication composing the sine argument.</param>
    /// <returns>The integral of the sine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression? Sin(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Negate(Expression.Call(typeof(T).GetMethod(nameof(double.Cos), [typeof(T)]), be)),
                c
            );
    }

    /// <summary>
    /// Integrates a cosine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cos</c>.</param>
    /// <param name="op">The parameter passed to the cosine function.</param>
    /// <returns>The integral of the cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression? Cos(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (op.Name != ParameterName) return null;
        return Expression.Call(typeof(T).GetMethod(nameof(double.Sin), [typeof(T)]), op);
    }

    /// <summary>
    /// Integrates a cosine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cos</c>.</param>
    /// <param name="be">The binary multiplication composing the cosine argument.</param>
    /// <returns>The integral of the cosine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression? Cos(
    MethodCallExpression e,
    BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(typeof(T).GetMethod(nameof(double.Sin), [typeof(T)]), be),
                c
            );
    }


    /// <summary>
    /// Integrates a tangent call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tan</c>.</param>
    /// <param name="op">The parameter passed to the tangent function.</param>
    /// <returns>The integral of the tangent call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression? Tan(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (op.Name != ParameterName) return null;

        return Expression.Negate(
                Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]),
                    Expression.Call(typeof(T).GetMethod(nameof(double.Cos), [typeof(T)]), op))
            );
    }

    /// <summary>
    /// Integrates a tangent call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tan</c>.</param>
    /// <param name="be">The binary multiplication composing the tangent argument.</param>
    /// <returns>The integral of the tangent call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression? Tan(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Negate(Expression.Call(typeof(T).GetMethod(nameof(double.Log), [typeof(T)]),
                    Expression.Call(typeof(T).GetMethod(nameof(double.Cos), [typeof(T)]), be))),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic sine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sinh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic sine function.</param>
    /// <returns>The integral of the hyperbolic sine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression? Sinh(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (op.Name != ParameterName)
        {
            return null;
        }
        return Expression.Call(typeof(T).GetMethod(nameof(double.Cosh), [typeof(T)]), op);
    }

    /// <summary>
    /// Integrates a hyperbolic sine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sinh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic sine argument.</param>
    /// <returns>The integral of the hyperbolic sine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression? Sinh(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(typeof(T).GetMethod(nameof(double.Cosh), [typeof(T)]), be),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic cosine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cosh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic cosine function.</param>
    /// <returns>The integral of the hyperbolic cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    public Expression? Cosh(
            MethodCallExpression e,
            ParameterExpression op
    )
    {
        if (op.Name != ParameterName) return null;

        return Expression.Call(typeof(T).GetMethod(nameof(double.Sinh), [typeof(T)]), op);
    }

    /// <summary>
    /// Integrates a hyperbolic cosine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cosh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic cosine argument.</param>
    /// <returns>The integral of the hyperbolic cosine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    public Expression? Cosh(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(typeof(T).GetMethod(nameof(double.Sinh), [typeof(T)]), be),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic tangent call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tanh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic tangent function.</param>
    /// <returns>The integral of the hyperbolic tangent call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression? Tanh(
            MethodCallExpression e,
            ParameterExpression op
    )
    {
        if (op.Name != ParameterName) return null;

        return Expression.Call(
            typeof(T).GetMethod(nameof(double.Log), [typeof(T)]),
            Expression.Call(typeof(T).GetMethod(nameof(double.Cosh), [typeof(T)]), op)
        );
    }

    /// <summary>
    /// Integrates a hyperbolic tangent call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tanh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic tangent argument.</param>
    /// <returns>The integral of the hyperbolic tangent call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression? Tanh(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c?.Value) ||
            be.Right is not ParameterExpression p2 || p2.Name != ParameterName)
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(
                    typeof(T).GetMethod(nameof(double.Log), [typeof(T)]),
                    Expression.Call(typeof(T).GetMethod(nameof(double.Cosh), [typeof(T)]), be)
                ),
                c
            );
    }

    /// <summary>
    /// Creates a typed numeric constant for <typeparamref name="T"/>.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <returns>Constant expression of type <typeparamref name="T"/>.</returns>
    private static ConstantExpression CreateConstant(double value)
    {
        return Expression.Constant(T.CreateChecked(value));
    }

}



/// <summary>
/// Provides the historical non-generic entry-point for double-based integration.
/// </summary>
public class ExpressionIntegration : ExpressionIntegration<double>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionIntegration"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    public ExpressionIntegration(string parameterName)
        : base(parameterName)
    {
    }
}

#pragma warning restore CS8604 // Existence possible d'un argument de référence null.
