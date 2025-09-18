using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides integration rules for mathematical expression trees.
/// </summary>
public class ExpressionIntegration : ExpressionTransformer
{
    /// <summary>
    /// Gets the name of the parameter used as the integration variable.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets or sets the cached parameter expression that matches <see cref="ParameterName"/>.
    /// </summary>
    private ParameterExpression parameter { get; set; }

    /// <summary>
    /// Integrates a lambda expression with respect to the configured parameter.
    /// </summary>
    /// <param name="e">The lambda expression to integrate.</param>
    /// <returns>The integrated expression tree.</returns>
    public Expression Integrate(LambdaExpression e)
    {
        return Expression.Lambda(Transform(e.Body), e.Parameters);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionIntegration"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    public ExpressionIntegration(string parameterName)
    {
        this.ParameterName = parameterName;
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
        return Expression.Multiply(e, parameter);
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
                Expression.Power(e, Expression.Constant(2.0)),
                Expression.Constant(2.0)
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Log), [ typeof(double) ]), right)
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
		if (right.Left is not ParameterExpression p || p.Name != ParameterName ||
			right.Right is not ConstantExpression expo || !NumberUtils.IsNumeric(expo.Value))
		{
			return null;
		}
		double n = Convert.ToDouble(expo.Value);
		if (Math.Abs(n - 1.0) < double.Epsilon)
		{
			return Expression.Multiply(
				left,
				Expression.Call(typeof(double).GetMethod(nameof(double.Log), [typeof(double)]), p)
			);
		}

		double newExpo = 1.0 - n;
		return Expression.Divide(
			Expression.Multiply(left, Expression.Power(p, Expression.Constant(newExpo))),
			Expression.Constant(newExpo)
		);
	}

        /// <summary>
        /// Integrates a constant divided by a square root method call of the integration parameter.
        /// </summary>
        /// <param name="e">The division expression being transformed.</param>
        /// <param name="left">The constant factor in the numerator.</param>
        /// <param name="right">The method call expected to represent <c>Math.Sqrt</c>.</param>
        /// <returns>The integral of the expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        MethodCallExpression right
    )
	{
		if (right.Method.Name != nameof(Math.Sqrt) ||
			right.Arguments.Count != 1 ||
			right.Arguments[0] is not ParameterExpression p || p.Name != ParameterName)
		{
			return null;
		}
		double factor = 2.0 * Convert.ToDouble(left.Value);
		return Expression.Multiply(
			Expression.Constant(factor),
			Expression.Call(typeof(double).GetMethod(nameof(double.Sqrt), [typeof(double)]), p)
		);
	}

        /// <summary>
        /// Integrates a natural logarithm call whose argument is the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Log</c>.</param>
        /// <param name="p">The parameter passed to the logarithm.</param>
        /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(Math), "Log")]
        public Expression? Log(
                MethodCallExpression e,
                ParameterExpression p
        )
	{
		if (p.Name != ParameterName) return null;
		return Expression.Multiply(
					parameter,
					Expression.Subtract(
						Expression.Call(typeof(double).GetMethod(nameof(double.Log), [typeof(double)]), parameter),
						Expression.Constant(1.0)
						)
				);
	}

        /// <summary>
        /// Integrates a base-10 logarithm call that uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Log10</c>.</param>
        /// <param name="p">The parameter passed to the logarithm.</param>
        /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
        public Expression? Log10(
                MethodCallExpression e,
                ParameterExpression p
        )
	{
		if (p.Name != ParameterName) return null;
		var ln10 = Expression.Constant(Math.Log(10.0));
		return Expression.Subtract(
			Expression.Multiply(p,
				Expression.Call(typeof(double).GetMethod(nameof(double.Log10), [typeof(double)]), p)),
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
		double n = Convert.ToDouble(expo.Value);
		if (Math.Abs(n + 1.0) < double.Epsilon)
		{
			return Expression.Call(typeof(double).GetMethod(nameof(double.Log), [typeof(double)]), p);
		}
		return Expression.Divide(
			Expression.Power(p, Expression.Constant(n + 1.0)),
			Expression.Constant(n + 1.0)
		);
	}

        /// <summary>
        /// Integrates an exponential call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Exp</c>.</param>
        /// <param name="op">The parameter passed to the exponential function.</param>
        /// <returns>The integral of the exponential call when the parameter matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression? Exp(
        MethodCallExpression e,
        ParameterExpression op
    )
	{
		if (op.Name != ParameterName) return null;
		return Expression.Call(typeof(double).GetMethod(nameof(double.Exp), [typeof(double)]), op);
	}

        /// <summary>
        /// Integrates an exponential call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Exp</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Exp), [typeof(double)]), be),
				c
			);
	}

        /// <summary>
        /// Integrates a sine call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Sin</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Cos), [typeof(double)]), op)
			);
	}

        /// <summary>
        /// Integrates a sine call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Sin</c>.</param>
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
				Expression.Negate(Expression.Call(typeof(double).GetMethod(nameof(double.Cos), [typeof(double)]), be)),
				c
			);
	}

        /// <summary>
        /// Integrates a cosine call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Cos</c>.</param>
        /// <param name="op">The parameter passed to the cosine function.</param>
        /// <returns>The integral of the cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression? Cos(
        MethodCallExpression e,
        ParameterExpression op
    )
	{
		if (op.Name != ParameterName) return null;
		return Expression.Call(typeof(double).GetMethod(nameof(double.Sin), [typeof(double)]), op);
	}

        /// <summary>
        /// Integrates a cosine call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Cos</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Sin), [typeof(double)]), be),
				c
			);
	}


        /// <summary>
        /// Integrates a tangent call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Tan</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Log), [typeof(double)]),
					Expression.Call(typeof(double).GetMethod(nameof(double.Cos), [typeof(double)]), op))
			);
	}

        /// <summary>
        /// Integrates a tangent call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Tan</c>.</param>
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
				Expression.Negate(Expression.Call(typeof(double).GetMethod(nameof(double.Log), [typeof(double)]),
					Expression.Call(typeof(double).GetMethod(nameof(double.Cos), [typeof(double)]), be))),
				c
			);
	}

        /// <summary>
        /// Integrates a hyperbolic sine call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Sinh</c>.</param>
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
		return Expression.Call(typeof(double).GetMethod(nameof(double.Cosh), [typeof(double)]), op);
	}

        /// <summary>
        /// Integrates a hyperbolic sine call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Sinh</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Cosh), [typeof(double)]), be),
				c
			);
	}

        /// <summary>
        /// Integrates a hyperbolic cosine call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Cosh</c>.</param>
        /// <param name="op">The parameter passed to the hyperbolic cosine function.</param>
        /// <returns>The integral of the hyperbolic cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
        public Expression? Cosh(
                MethodCallExpression e,
                ParameterExpression op
        )
	{
		if (op.Name != ParameterName) return null;

		return Expression.Call(typeof(double).GetMethod(nameof(double.Sinh), [typeof(double)]), op);
	}

        /// <summary>
        /// Integrates a hyperbolic cosine call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Cosh</c>.</param>
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
				Expression.Call(typeof(double).GetMethod(nameof(double.Sinh), [typeof(double)]), be),
				c
			);
	}

        /// <summary>
        /// Integrates a hyperbolic tangent call that directly uses the integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Tanh</c>.</param>
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
				typeof(double).GetMethod(nameof(double.Log), [typeof(double)]),
				Expression.Call(typeof(double).GetMethod(nameof(double.Cosh), [ typeof(double) ]), op)
			);
	}

        /// <summary>
        /// Integrates a hyperbolic tangent call whose argument is a scaled integration parameter.
        /// </summary>
        /// <param name="e">The method call expression representing <c>Math.Tanh</c>.</param>
        /// <param name="be">The binary multiplication composing the hyperbolic tangent argument.</param>
        /// <returns>The integral of the hyperbolic tangent call when the pattern matches; otherwise, <see langword="null"/>.</returns>
        [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression? Tanh(
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
				Expression.Call(
					typeof(double).GetMethod(nameof(double.Log), [typeof(double)]),
					Expression.Call(typeof(double).GetMethod(nameof(double.Cosh), [typeof(double)]), be)
				),
				c
			);
	}

}
