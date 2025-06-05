using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

public class ExpressionIntegration : ExpressionTransformer
{
    public string ParameterName { get; }
    private ParameterExpression parameter { get; set; }

    public Expression Integrate(LambdaExpression e)
    {
        parameter = e.Parameters.FirstOrDefault(p => p.Name == this.ParameterName);
        return Expression.Lambda(Transform(e.Body), e.Parameters);
    }

    public ExpressionIntegration(string parameterName)
    {
        this.ParameterName = parameterName;
    }

    [ExpressionSignature(ExpressionType.Constant)]
    public Expression Constant(
        ConstantExpression e,
        object value
    )
    {
        return Expression.Multiply(e, parameter);
    }

    [ExpressionSignature(ExpressionType.Negate)]
    public Expression Negate(
        UnaryExpression e,
        Expression operand
    )
    {
        return Expression.Negate(Transform(operand));
    }

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

    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        Expression right
    )
    {
        return Expression.Multiply(left, Transform(right));
    }

    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Multiply(right, Transform(left));
    }

    [ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Divide(Transform(left), right);
    }

	[ExpressionSignature(ExpressionType.Divide)]
	public Expression Divide(
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

	[ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
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

	[ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
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

	[ExpressionCallSignature(typeof(Math), "Log")]
	public Expression Log(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Log10))]
	public Expression Log10(
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

	[ExpressionSignature(ExpressionType.Power)]
    public Expression Power(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression Exp(
        MethodCallExpression e,
        ParameterExpression op
    )
	{
		if (op.Name != ParameterName) return null;
		return Expression.Call(typeof(double).GetMethod(nameof(double.Exp), [typeof(double)]), op);
	}

	[ExpressionCallSignature(typeof(double), nameof(double.Exp))]
	public Expression Exp(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression Sin(
        MethodCallExpression e,
        ParameterExpression op
    )
	{
		if (op.Name != ParameterName) return null;
		return Expression.Negate(
				Expression.Call(typeof(double).GetMethod(nameof(double.Cos), [typeof(double)]), op)
			);
	}

	[ExpressionCallSignature(typeof(double), nameof(double.Sin))]
	public Expression Sin(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression Cos(
        MethodCallExpression e,
        ParameterExpression op
    )
	{
		if (op.Name != ParameterName) return null;
		return Expression.Call(typeof(double).GetMethod(nameof(double.Sin), [typeof(double)]), op);
	}

	[ExpressionCallSignature(typeof(double), nameof(double.Cos))]
	public Expression Cos(
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


	[ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression Tan(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression Tan(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression Sinh(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression Sinh(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
	public Expression Cosh(
		MethodCallExpression e,
		ParameterExpression op
	)
	{
		if (op.Name != ParameterName) return null;

		return Expression.Call(typeof(double).GetMethod(nameof(double.Sinh), [typeof(double)]), op);
	}

	[ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
	public Expression Cosh(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
	public Expression Tanh(
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

	[ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression Tanh(
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
