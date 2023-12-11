﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

public class ExpressionDerivation : ExpressionTranformer
{
    public string ParameterName { get; }

    public ExpressionDerivation(string parameterName)
    {
        this.ParameterName = parameterName;
    }

    public Expression Derivate(LambdaExpression e)
    {
        return Expression.Lambda(Transform(e.Body.Simplify()).Simplify(), e.Parameters);
    }

    [ExpressionSignature(ExpressionType.Constant)]
    protected Expression Constant(
        ConstantExpression e,
        object value
    )
    {
        return Expression.Constant(0.0);
    }

    [ExpressionSignature(ExpressionType.Parameter)]
    protected Expression Parameter(
        ParameterExpression e
    )
    {
        if (e.Name == ParameterName)
        {
            return Expression.Constant(1.0);
        }
        else
        {
            return Expression.Constant(0.0);
        }
    }

    [ExpressionSignature(ExpressionType.Negate)]
    protected Expression Negate(
        UnaryExpression e,
        Expression operand
    )
    {
        return Expression.Negate(Transform(operand));
    }

    [ExpressionSignature(ExpressionType.Add)]
    protected Expression Add(
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
    protected Expression Subtract(
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
    protected Expression Multiply(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Add(
            Expression.Multiply(Transform(left), right),
            Expression.Multiply(left, Transform(right))
        );
    }

    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression Divide(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Divide(
            Expression.Subtract(
                Expression.Multiply(left, Transform(right)),
                Expression.Multiply(Transform(left), right)),
            Expression.Power(right, Expression.Constant(2.0)));
    }

    [ExpressionSignature(ExpressionType.Power)]
    protected Expression Power(
        BinaryExpression e,
        Expression left,
        ConstantExpression right)
    {
        return Expression.Multiply(
            right,
            Expression.Multiply(
                Expression.Power(left, Expression.Subtract(right, Expression.Constant(1.0))),
                Transform(left)
                )
            );
    }

    [ExpressionSignature(ExpressionType.Power)]
    protected Expression Power(
        BinaryExpression e,
        Expression left,
        Expression right)
    {
        return
            Expression.Multiply(
                Expression.Power(
                    left,
                    Expression.Subtract(right, Expression.Constant(1.0))
                ),
                Expression.Add(
                    Expression.Multiply(right, Transform(left)),
                    Expression.Multiply(
                        left,
                        Expression.Multiply(
                            Expression.Call(typeof(double).GetMethod(nameof(double.Log)), left),
                            Transform(right)
                        )
                    )

                )
            );
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    protected Expression Exp(
        MethodCallExpression e,
        Expression operand)
    {
        return
            Expression.Multiply(
                Transform(operand),
                Expression.Call(typeof(double).GetMethod(nameof(double.Exp)), operand)
            );
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Log))]
    protected Expression LogMath(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Divide(
            Transform(operand),
            operand
            );
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    protected Expression Log10(
            MethodCallExpression e,
            Expression operand)
    {
        return Expression.Divide(
            operand,
            Expression.Multiply(
                Expression.Constant(Math.Log(10)),
                Transform(operand)
            )
        );
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    protected Expression Sin(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Multiply(
            Transform(operand),
            Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), operand));
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    protected Expression Cos(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Negate(
            Expression.Multiply(
            Transform(operand),
            Expression.Call(typeof(double).GetMethod(nameof(double.Sin)), operand)));
    }

    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    protected Expression Tan(
        MethodCallExpression e,
        Expression operand)
    {
        return Transform(Expression.Divide(
             Expression.Call(typeof(double).GetMethod(nameof(double.Sin)), operand),
             Expression.Call(typeof(double).GetMethod(nameof(double.Cos)), operand)
            ).Simplify()
        );
    }

}
