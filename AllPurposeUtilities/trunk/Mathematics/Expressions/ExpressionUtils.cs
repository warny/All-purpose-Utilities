using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public static class ExpressionUtils
	{
		public static bool CheckConstant( Expression expressionToCheck, double checkValue )
		{
			ConstantExpression expression = expressionToCheck as ConstantExpression;
			if (expression == null) return false;
			var value = expression.Value;

			if (NumberUtils.IsNumeric(value)) {
				return ((double)value) == checkValue;
			}
			return false;
		}

		private static ExpressionComparer expressionComparer = new ExpressionComparer();

		public static bool Equals( Expression x, Expression y )
		{
			return expressionComparer.Equals(x, y);
		}

	}

	public class ExpressionComparer : IEqualityComparer<Expression>, IComparer<Expression>
	{
		ExpressionSimplifier expressionSimplifier = new ExpressionSimplifier();

		public int Compare( Expression x, Expression y )
		{
			return 0;
		}

		private int First( params int[] comparisons )
		{
			return comparisons.FirstOrDefault(c => c!=0);
		}

		public bool Equals( Expression x, Expression y )
		{
			x = expressionSimplifier.Simplify(x);
			y = expressionSimplifier.Simplify(y);
			return Equals(x, null, y, null);
		}

		private bool Equals( Expression x, ParameterExpression[] xParams, Expression y, ParameterExpression[] yParams )
		{
			if (x == y) return true;
			if (x.NodeType!=y.NodeType) return false;

			if (x is LambdaExpression xl && y is LambdaExpression yl) {
				var newXParams = xl.Parameters.ToArray();
				var newYParams = yl.Parameters.ToArray();

				if (newXParams.Length != newYParams.Length) return false;
				for (int i = 0 ; i < newXParams.Length ; i++) {
					if (newXParams[i].Type != newYParams[i].Type) return false;
				}

				return this.Equals(xl.Body, newXParams, yl.Body, newYParams);
			}

			if (x is ConstantExpression xco && y is ConstantExpression yco) {
				return xco.Value.Equals (yco.Value);
			}

			if (x is ParameterExpression xpe && y is ParameterExpression ype) {
				int xi = xParams.IndexOf(e => e.Name == xpe.Name);
				int yi = yParams.IndexOf(e => e.Name == ype.Name);
				
				return xi != -1 && yi != -1 && xi == yi;
			}

			if (x is UnaryExpression xuo && y is UnaryExpression yuo) {
				return this.Equals(xuo.Operand, xParams, yuo.Operand, yParams);
			}

			if (x is BinaryExpression xbo && y is BinaryExpression ybo) {
				return this.Equals(xbo.Left, xParams, ybo.Left, yParams) && this.Equals(xbo.Right, xParams, ybo.Right, yParams);
			}

			if (x is MethodCallExpression xmco && y is MethodCallExpression ymco) {
				if (!( xmco.Type == ymco.Type && xmco.Object == ymco.Object && xmco.Method == ymco.Method && xmco.Arguments.Count == ymco.Arguments.Count)) return false;

				for (int i = 0 ; i < xmco.Arguments.Count ; i++) {
					if (!this.Equals (xmco.Arguments[i], xParams, ymco.Arguments[i], yParams)) return false;
				}
				return true;
			}

			if (x is MemberExpression xmo && y is MemberExpression ymo) {
				return xmo.Type == ymo.Type && this.Equals (xmo.Expression, yParams, ymo.Expression, yParams) &&  xmo.Member == ymo.Member;
			}

			return false;
		}

		public int GetHashCode( Expression obj )
		{
			return expressionSimplifier.Simplify(obj).ToString().GetHashCode();
		}
	}
}
