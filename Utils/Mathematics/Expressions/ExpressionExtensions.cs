using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
    public static class ExpressionExtensions
    {
        public static Expression Simplify(this Expression e)
        {
            ExpressionSimplifier simplifier = new ExpressionSimplifier();
            return simplifier.Simplify(e);
        }

        public static LambdaExpression Derivate(this LambdaExpression e)
        {
            e.ArgMustNotBeNull();
            e.Parameters.ArgMustBeOfSize(1);
            return e.Derivate(e.Parameters[0].Name);
        }

        public static LambdaExpression Derivate(this LambdaExpression e, string paramName)
        {
            e.ArgMustNotBeNull();

            ExpressionDerivation derivation = new ExpressionDerivation(paramName);
            var expression = e.Body.Simplify();
            expression = derivation.Derivate((LambdaExpression)expression);
            expression = expression.Simplify();
            return Expression.Lambda(expression, e.Parameters);
        }

    }

    public class ExpressionExtensionsException : Exception
    {
        public ExpressionExtensionsException(string msg) : base(msg, null) { }
        public ExpressionExtensionsException(string msg, Exception innerException) : base(msg, innerException) { }
    }

}
