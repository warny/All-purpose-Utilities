using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	class ExpressionUtils
	{
		public static bool CheckConstant( Expression expressionToCheck, double checkValue )
		{
			ConstantExpression expression = expressionToCheck as ConstantExpression;
			if (expression == null) return false;
			var value = expression.Value;

			if (ObjectUtils.IsNumeric(value)) {
				return ((double)value) == checkValue;
			}
			return false;
		}

	}

	public class ExpressionComparer : IEqualityComparer<Expression>, IComparer<Expression>
	{
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
			if (x == y) return true;
			if (x.NodeType!=y.NodeType) return false;
			
			var xco = x as ConstantExpression;
			var yco = y as ConstantExpression;
			if (xco != null) {
				return xco.Value.Equals (yco.Value);
			}

			var xuo = x as UnaryExpression;
			var yuo = y as UnaryExpression;
			if (xuo != null) {
				return this.Equals(xuo.Operand, yuo.Operand);
			}

			var xbo = x as BinaryExpression;
			var ybo = y as BinaryExpression;
			if (xbo != null) {
				return this.Equals(xbo.Left, ybo.Left) && this.Equals(xbo.Right, ybo.Right);

			}

			var xmco = x as MethodCallExpression;
			var ymco = y as MethodCallExpression;
			if (xmco != null) {
				if (!( xmco.Type == ymco.Type && xmco.Object == ymco.Object && xmco.Method == ymco.Method && xmco.Arguments.Count == ymco.Arguments.Count)) return false;

				for (int i = 0 ; i < xmco.Arguments.Count ; i++) {
					if (!this.Equals (xmco.Arguments[i], ymco.Arguments[i])) return false;
				}
				return true;
			}

			var xmo = x as MemberExpression;
			var ymo = y as MemberExpression;
			if (xmco != null) {
				return xmo.Type == ymo.Type && this.Equals (xmo.Expression, ymo.Expression) &&  xmo.Member == ymo.Member;
			}

			return false;
		}

		public int GetHashCode( Expression obj )
		{
			return obj.ToString().GetHashCode();
		}
	}
}
