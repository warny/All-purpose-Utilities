using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;

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
        return parameter;
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

}
