using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public class ExpressionComparer : IEqualityComparer<Expression>
	{
		private static readonly ExpressionSimplifier expressionSimplifier = new ExpressionSimplifier();

		private ExpressionComparer() { }

		public static ExpressionComparer Default { get; } = new ExpressionComparer();

		public bool Equals(Expression x, Expression y)
		{
			var xParameters = x is LambdaExpression xLambda ? xLambda.Parameters.ToArray() : null;
			var yParameters = y is LambdaExpression yLambda ? yLambda.Parameters.ToArray() : null;

			x = expressionSimplifier.Simplify(x);
			y = expressionSimplifier.Simplify(y);
			return Equals(x, xParameters, y, yParameters);
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
				if (!xco.Type.In(Types.Number) || !yco.Type.In(Types.Number)) return xco.Value.Equals(yco.Value);

				bool TryCompareNumber(ConstantExpression x, ConstantExpression y, Type type, out bool result) {
					result = false;
					if (x.Type != type && y.Type != type) return false;
					if (Marshal.SizeOf(xco.Type) > Marshal.SizeOf(type)) return false;
                    if (Marshal.SizeOf(yco.Type) > Marshal.SizeOf(type)) return false;
                    result = Convert.ChangeType(xco.Value, type).Equals(Convert.ChangeType(yco.Value, type));
					return true;
				}

				foreach (var type in Types.Number)
				{
					if (TryCompareNumber(xco, yco, type, out var result)) return result;
				}

				return false;
            }

            if (x is ParameterExpression xpe && y is ParameterExpression ype) {
				int xi = xParams?.IndexOf(e => e.Name == xpe.Name) ?? -1;
				int yi = yParams?.IndexOf(e => e.Name == ype.Name) ?? -1;
				
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
