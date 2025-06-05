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
        if (right.Name == ParameterName)
        {
            return Expression.Multiply(
                left,
                Expression.Call(typeof(Math).GetMethod("Log"), right)
            );
        }
        return null;
    }

    [ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        [ExpressionSignature(ExpressionType.Power)] BinaryExpression right
    )
    {
        if (right.Left is ParameterExpression p && p.Name == ParameterName &&
            right.Right is ConstantExpression expo && NumberUtils.IsNumeric(expo.Value))
        {
            double n = Convert.ToDouble(expo.Value);
            if (Math.Abs(n - 1.0) < double.Epsilon)
            {
                return Expression.Multiply(
                    left,
                    Expression.Call(typeof(Math).GetMethod("Log"), p)
                );
            }

            double newExpo = 1.0 - n;
            return Expression.Divide(
                Expression.Multiply(left, Expression.Power(p, Expression.Constant(newExpo))),
                Expression.Constant(newExpo)
            );
        }
        return null;
    }

    [ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        MethodCallExpression right
    )
    {
        if (right.Method.Name == nameof(Math.Sqrt) &&
            right.Arguments.Count == 1 &&
            right.Arguments[0] is ParameterExpression p && p.Name == ParameterName)
        {
            double factor = 2.0 * Convert.ToDouble(left.Value);
            return Expression.Multiply(
                Expression.Constant(factor),
                Expression.Call(typeof(Math).GetMethod(nameof(Math.Sqrt)), p)
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(Math), "Log")]
    public Expression Log(
        MethodCallExpression e,
        ParameterExpression p
    )
    {
        if (p.Name == ParameterName)
        {
            return Expression.Multiply(
                    parameter,
                    Expression.Subtract(
                        Expression.Call(typeof(Math).GetMethod("Log"), parameter),
                        Expression.Constant(1.0)
                        )
                );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    public Expression Log10(
        MethodCallExpression e,
        ParameterExpression p
    )
    {
        if (p.Name == ParameterName)
        {
            var ln10 = Expression.Constant(Math.Log(10.0));
            return Expression.Subtract(
                Expression.Multiply(p,
                    Expression.Call(typeof(double).GetMethod(nameof(double.Log10)), p)),
                Expression.Divide(p, ln10)
            );
        }
        return null;
    }

    [ExpressionSignature(ExpressionType.Power)]
    public Expression Power(
        BinaryExpression e,
        ParameterExpression p,
        [ConstantNumeric] ConstantExpression expo
    )
    {
        if (p.Name == ParameterName)
        {
            double n = Convert.ToDouble(expo.Value);
            if (Math.Abs(n + 1.0) < double.Epsilon)
            {
                return Expression.Call(typeof(Math).GetMethod("Log"), p);
            }
            return Expression.Divide(
                Expression.Power(p, Expression.Constant(n + 1.0)),
                Expression.Constant(n + 1.0)
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression Exp(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Call(typeof(double).GetMethod(nameof(double.Exp)), op);
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Call(typeof(double).GetMethod(nameof(double.Exp)), op),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression Sin(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Negate(
                Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), op)
            );
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Negate(Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), op)),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression Cos(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Call(typeof(double).GetMethod(nameof(double.Sin)), op);
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Call(typeof(double).GetMethod(nameof(double.Sin)), op),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression Tan(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Negate(
                Expression.Call(typeof(double).GetMethod(nameof(double.Log)),
                    Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), op))
            );
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Negate(Expression.Call(typeof(double).GetMethod(nameof(double.Log)),
                    Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), op))),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression Sinh(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Call(typeof(double).GetMethod(nameof(double.Cosh)), op);
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Call(typeof(double).GetMethod(nameof(double.Cosh)), op),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    public Expression Cosh(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Call(typeof(double).GetMethod(nameof(double.Sinh)), op);
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Call(typeof(double).GetMethod(nameof(double.Sinh)), op),
                c
            );
        }
        return null;
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression Tanh(
        MethodCallExpression e,
        ParameterExpression op
    )
    {
        if (op.Name == ParameterName)
        {
            return Expression.Call(
                typeof(double).GetMethod(nameof(double.Log)),
                Expression.Call(typeof(double).GetMethod(nameof(double.Cosh)), op)
            );
        }
        if (op is BinaryExpression be && be.NodeType == ExpressionType.Multiply &&
            be.Left is ConstantExpression c && NumberUtils.IsNumeric(c.Value) &&
            be.Right is ParameterExpression p2 && p2.Name == ParameterName)
        {
            return Expression.Divide(
                Expression.Call(
                    typeof(double).GetMethod(nameof(double.Log)),
                    Expression.Call(typeof(double).GetMethod(nameof(double.Cosh)), op)
                ),
                c
            );
        }
        return null;
    }


}
