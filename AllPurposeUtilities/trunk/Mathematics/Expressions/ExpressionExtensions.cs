using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public static class ExpressionExtensions
	{
		private static ExpressionComparer expressionComparer = new ExpressionComparer();

		public static Expression Simplify( this Expression e )
		{
			ExpressionSimplifier simplifier = new ExpressionSimplifier();
			return simplifier.Transform(e);
		}

		public static LambdaExpression Derive( this LambdaExpression e )
		{
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");
			if (e.Parameters.Count != 1)
				throw new ExpressionExtensionsException("Incorrect number of parameters. Must be 1");
			return e.Derive(e.Parameters[0].Name);
		}

		public static LambdaExpression Derive( this LambdaExpression e, string paramName )
		{
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");

			ExpressionDerivation derivation = new ExpressionDerivation(paramName);
			var expression = e.Body.Simplify();
			expression = derivation.Transform(expression);
			expression = expression.Simplify();
			return Expression.Lambda(expression, e.Parameters);
		}

		public static Expression<T> Derive<T>( this Expression<T> e )
		{
			// check just one variable
			if (e.Parameters.Count != 1)
				throw new ExpressionExtensionsException("Incorrect number of parameters. Must be 1");

			// calc derivative
			return e.Derive(e.Parameters[0].Name);
		}

		public static Expression<T> Derive<T>( this Expression<T> e, string paramName )
		{
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");

			ExpressionDerivation derivation  =new ExpressionDerivation(paramName);
			var expression = e.Body.Simplify();
			expression = derivation.Transform(expression);
			expression = expression.Simplify();

			return Expression.Lambda<T>(expression, e.Parameters);
		}
	}

	public class ExpressionExtensionsException : Exception
	{
		public ExpressionExtensionsException( string msg ) : base(msg, null) { }
		public ExpressionExtensionsException( string msg, Exception innerException ) : base(msg, innerException) { }
	}

}
