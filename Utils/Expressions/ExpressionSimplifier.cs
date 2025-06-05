using System;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	/// <summary>
	/// Provides methods to simplify or rewrite expression trees by applying various transformations,
	/// typically related to arithmetic identities or constant folding.
	/// This partial class works alongside other parts of <see cref="ExpressionSimplifier"/> to compose
	/// a complete transformation pipeline.
	/// </summary>
	public partial class ExpressionSimplifier : ExpressionTransformer
	{
		/// <summary>
		/// Simplifies the given <paramref name="e"/> by calling <see cref="Transform"/>.
		/// </summary>
		/// <param name="e">The <see cref="Expression"/> to simplify.</param>
		/// <returns>A simplified version of <paramref name="e"/>, if any transformation rules match.</returns>
		public Expression Simplify(Expression e)
		{
			return Transform(e);
		}

		/// <summary>
		/// Prepares an expression for transformation by calling <see cref="Transform"/>.
		/// Subclasses can override for custom logic, but here it simply re-applies <see cref="Transform"/>.
		/// </summary>
		/// <param name="e">The expression to prepare.</param>
		/// <returns>The transformed expression.</returns>
		protected override Expression PrepareExpression(Expression e)
		{
			return Transform(e);
		}

		#region Operations with 0 and 1

		/// <summary>
		/// Simplifies <c>left + 0</c> to <c>left</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Add)]
		public Expression AdditionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
		{
			if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return Transform(left);
			return null;
		}

		/// <summary>
		/// Simplifies <c>0 + right</c> to <c>right</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Add)]
		public Expression AdditionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
		{
			if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return right;
			return null;
		}

		/// <summary>
		/// Simplifies <c>left - 0</c> to <c>left</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		public Expression SubstractionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
		{
			if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return left;
			return null;
		}

		/// <summary>
		/// Simplifies <c>0 - right</c> to <c>-right</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		public Expression SubstractionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
		{
			if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return Transform(Expression.Negate(right));
			return null;
		}

		/// <summary>
		/// Simplifies multiplication by 0, 1, or -1 (e.g., <c>x * 0</c> to 0, <c>x * 1</c> to <c>x</c>, etc.).
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		public Expression MultiplicationWithZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0, 1, -1)] ConstantExpression right)
		{
			if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return right;  // x * 0 => 0
			if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return Transform(left);   // x * 1 => x
			if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return Expression.Negate(left); // x * -1 => -x
			return null;
		}

		/// <summary>
		/// Simplifies multiplication by 0, 1, or -1 (e.g., <c>0 * x</c>, <c>1 * x</c>, <c>-1 * x</c>).
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		public Expression MultiplicationWithZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1, -1)] ConstantExpression left, Expression right)
		{
			if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left;
			if (NumberUtils.CompareNumeric(left.Value, 1) == 0) return right;
			if (NumberUtils.CompareNumeric(left.Value, -1) == 0) return Expression.Negate(right);
			return null;
		}

		/// <summary>
		/// Simplifies division by 0, 1, or -1 (throwing if 0).
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		public Expression DivideWithZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
		{
			if (NumberUtils.CompareNumeric(right.Value, 0) == 0) throw new DivideByZeroException();
			if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return left;
			if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return Expression.Negate(left);
			return null;
		}

		/// <summary>
		/// Simplifies <c>0 / x</c> to <c>0</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		public Expression DivideWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
		{
			if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left;
			return null;
		}

		/// <summary>
		/// Simplifies <c>0^x</c> or <c>1^x</c> to 0 or 1 respectively.
		/// </summary>
		[ExpressionSignature(ExpressionType.Power)]
		public Expression PowerOfZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1)] ConstantExpression left, Expression right)
		{
			if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left; // 0^x => 0
			if (NumberUtils.CompareNumeric(left.Value, 1) == 0) return left; // 1^x => 1
			return null;
		}

		/// <summary>
		/// Simplifies <c>x^0</c> or <c>x^1</c> to <c>1</c> or <c>x</c> respectively.
		/// </summary>
		[ExpressionSignature(ExpressionType.Power)]
		public Expression PowerByZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
		{
			if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return Expression.Constant(Convert.ChangeType(1, right.Type));
			if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return Transform(left);
			if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return Transform(Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), left));
			if (NumberUtils.CompareNumeric(right.Value, 0) == -1) return Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), Transform(Expression.Power(left, Expression.Constant(-(double)Convert.ChangeType(right.Value, typeof(double))))));
			return null;
		}

		#endregion

		#region Addition

		/// <summary>
		/// Simplifies constant addition <c>(a + b)</c> where both are numeric constants.
		/// </summary>
		[ExpressionSignature(ExpressionType.Add)]
		protected Expression AdditionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
		{
			return Expression.Constant((object)((dynamic)left.Value + (dynamic)right.Value));
		}

		/// <summary>
		/// Rewrites <c>left + (-right)</c> as <c>left - right</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Add)]
		protected Expression AdditionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
		{
			return Transform(Expression.Subtract(left, right.Operand));
		}

		/// <summary>
		/// Rewrites <c>left - (-right)</c> as <c>left + right</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
		{
			return Transform(Expression.Add(left, right.Operand));
		}

		/// <summary>
		/// Rewrites <c>(-left) - right</c> as <c>-(left + right)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
		{
			return Transform(Expression.Negate(Expression.Add(left.Operand, right)));
		}

		/// <summary>
		/// Rewrites <c>-(left - right)</c> as <c>-right + left</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Negate)]
		protected Expression NegateWithSubstraction(UnaryExpression e, [ExpressionSignature(ExpressionType.Subtract)] BinaryExpression operand)
		{
			return Transform(Expression.Add(Expression.Negate(operand.Left), operand.Right));
		}

		/// <summary>
		/// Simplifies <c>left - (right1 + right2)</c> to <c>(left - right1) - right2</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionWithAddition(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Add)] BinaryExpression right)
		{
			return Expression.Subtract(
				Expression.Subtract(left, right.Left),
				right.Right
			);
		}

		/// <summary>
		/// Simplifies <c>left - (right1 - right2)</c> to <c>(left + right2) - right1</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionWithSubstraction(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Subtract)] BinaryExpression right)
		{
			return Expression.Subtract(
				Expression.Add(left, right.Right),
				right.Left
			);
		}

		/// <summary>
		/// Simplifies constant subtraction <c>(a - b)</c> where both are numeric constants.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
		{
			return Expression.Constant((object)((dynamic)left.Value - (dynamic)right.Value));
		}

		/// <summary>
		/// Attempts to factor out common elements in <c>left + right</c> if possible.
		/// E.g., rewriting <c>a*x + b*x</c> as <c>(a+b)*x</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Add)]
		protected Expression AdditionOfEqualsElements(BinaryExpression e, Expression left, Expression right)
		{
			bool leftAugmented = false;
			Expression leftleft;
			Expression leftright;
			if (left.NodeType == ExpressionType.Multiply)
			{
				leftleft = ((BinaryExpression)left).Left;
				leftright = ((BinaryExpression)left).Right;
			}
			else
			{
				leftAugmented = true;
				leftleft = Expression.Constant(Convert.ChangeType(1, left.Type));
				leftright = left;
			}

			bool rightAugmented = false;
			Expression rightleft;
			Expression rightright;
			if (right.NodeType == ExpressionType.Multiply)
			{
				rightleft = ((BinaryExpression)right).Left;
				rightright = ((BinaryExpression)right).Right;
			}
			else
			{
				rightAugmented = true;
				rightleft = Expression.Constant(Convert.ChangeType(1, right.Type));
				rightright = right;
			}

			// Check if the single factor is truly 1
			if (leftAugmented && leftright is ConstantExpression leftRightConst && NumberUtils.CompareNumeric(leftRightConst.Value, 1) == 0)
				return null;
			if (rightAugmented && rightright is ConstantExpression rightRightConst && NumberUtils.CompareNumeric(rightRightConst.Value, 1) == 0)
				return null;

			// Attempt to unify or swap factors for factoring out
			if (!leftAugmented && !rightAugmented && ExpressionComparer.Default.Equals(leftleft, rightleft))
			{
				ObjectUtils.Swap(ref leftleft, ref leftright);
				ObjectUtils.Swap(ref rightleft, ref rightright);
			}
			else if (ExpressionComparer.Default.Equals(leftleft, rightright))
			{
				ObjectUtils.Swap(ref leftleft, ref leftright);
			}
			else if (ExpressionComparer.Default.Equals(leftright, rightleft))
			{
				ObjectUtils.Swap(ref rightleft, ref rightright);
			}
			else if (ExpressionComparer.Default.Equals(leftright, rightright))
			{
				// do nothing
			}
			else
			{
				return null;
			}

			// Factor out
			return Transform(
				Expression.Multiply(
					Transform(Expression.Add(leftleft, rightleft)),
					leftright
				)
			);
		}

		/// <summary>
		/// Attempts to factor out common elements in <c>left - right</c> if possible.
		/// E.g., rewriting <c>a*x - b*x</c> as <c>(a-b)*x</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Subtract)]
		protected Expression SubstractionOfEqualsElements(BinaryExpression e, Expression left, Expression right)
		{
			bool leftAugmented = false;
			Expression leftleft;
			Expression leftright;
			if (left.NodeType == ExpressionType.Multiply)
			{
				leftleft = ((BinaryExpression)left).Left;
				leftright = ((BinaryExpression)left).Right;
			}
			else
			{
				leftAugmented = true;
				leftleft = Expression.Constant(Convert.ChangeType(1, left.Type));
				leftright = left;
			}

			bool rightAugmented = false;
			Expression rightleft;
			Expression rightright;
			if (right.NodeType == ExpressionType.Multiply)
			{
				rightleft = ((BinaryExpression)right).Left;
				rightright = ((BinaryExpression)right).Right;
			}
			else
			{
				rightAugmented = true;
				rightleft = Expression.Constant(Convert.ChangeType(1, right.Type));
				rightright = right;
			}

			// Attempt to unify or swap factors
			if ((!leftAugmented && !rightAugmented) && ExpressionComparer.Default.Equals(leftleft, rightleft))
			{
				ObjectUtils.Swap(ref leftleft, ref leftright);
				ObjectUtils.Swap(ref rightleft, ref rightright);
			}
			else if (ExpressionComparer.Default.Equals(leftleft, rightright))
			{
				ObjectUtils.Swap(ref leftleft, ref leftright);
			}
			else if (ExpressionComparer.Default.Equals(leftright, rightleft))
			{
				ObjectUtils.Swap(ref rightleft, ref rightright);
			}
			else if (ExpressionComparer.Default.Equals(leftright, rightright))
			{
				// do nothing
			}
			else
			{
				return null;
			}

			return Transform(
				Expression.Multiply(
					Transform(Expression.Subtract(leftleft, rightleft)),
					leftright
				)
			);
		}

		#endregion

		#region Multiplication

		/// <summary>
		/// Simplifies constant multiplication <c>(a * b)</c> where both are numeric constants.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationOfConstants(
			BinaryExpression e,
			[ConstantNumeric] ConstantExpression left,
			[ConstantNumeric] ConstantExpression right)
		{
			return Expression.Constant((object)((dynamic)left.Value * (dynamic)right.Value));
		}

		/// <summary>
		/// Commutes <c>left * constant</c> to <c>constant * left</c> for consistent transformations.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression Multiplication(BinaryExpression e, Expression left, [ConstantNumeric] ConstantExpression right)
		{
			return Expression.Multiply(right, left);
		}

		/// <summary>
		/// Combines a constant with another multiply expression, e.g., <c>c * (c2 * rest)</c> => <c>(c*c2) * rest</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression Multiplication(
			BinaryExpression e,
			[ConstantNumeric] ConstantExpression left,
			[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
		{
			if (right.Left is ConstantExpression rightLeftConst)
			{
				return Expression.Multiply(
					Expression.Constant((object)((dynamic)left.Value * (dynamic)rightLeftConst.Value)),
					right.Right
				);
			}
			return null;
		}

		/// <summary>
		/// Combines two multiply expressions if both have a constant factor,
		/// e.g. <c>(c1 * x) * (c2 * y)</c> => <c>(c1*c2) * (x*y)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression Multiplication(
			BinaryExpression e,
			[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left,
			[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
		{
			if (left.Left is ConstantExpression leftLeft && right.Left is ConstantExpression rightLeft)
			{
				return Expression.Multiply(
					Expression.Constant((object)((dynamic)leftLeft.Value * (dynamic)rightLeft.Value)),
					Transform(Expression.Multiply(left.Right, right.Right))
				);
			}
			return null;
		}

		/// <summary>
		/// Simplifies constant division <c>(a / b)</c> when both are numeric constants.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionOfConstants(
			BinaryExpression e,
			[ConstantNumeric] ConstantExpression left,
			[ConstantNumeric] ConstantExpression right)
		{
			return Expression.Constant((object)((dynamic)left.Value / (dynamic)right.Value));
		}

		/// <summary>
		/// Rewrites <c>left * (-right)</c> as <c>-(left * right)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
		{
			return Expression.Negate(
				Transform(Expression.Multiply(left, right.Operand))
			);
		}

		/// <summary>
		/// Rewrites <c>(-left) * right</c> as <c>-(left * right)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
		{
			return Expression.Negate(
				Transform(Expression.Multiply(left.Operand, right))
			);
		}

		/// <summary>
		/// Rewrites <c>left / (-right)</c> as <c>-(left / right)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
		{
			return Expression.Negate(
				Transform(Expression.Divide(left, right.Operand))
			);
		}

		/// <summary>
		/// Rewrites <c>(-left) / right</c> as <c>-(left / right)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
		{
			return Expression.Negate(
				Transform(Expression.Divide(left.Operand, right))
			);
		}

		/// <summary>
		/// Distributes multiplication if one side is <c>(constant * part)</c>, e.g.,
		/// <c>(c * x) * y</c> => <c>c * (x * y)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationOfEqualsElements(BinaryExpression e, [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left, Expression right)
		{
			Expression constant;
			Expression leftpart;

			if (left.Left.NodeType == ExpressionType.Constant)
			{
				constant = left.Left;
				leftpart = left.Right;
			}
			else if (left.Right.NodeType == ExpressionType.Constant)
			{
				constant = left.Right;
				leftpart = left.Left;
			}
			else
			{
				return null;
			}

			return Transform(
				Expression.Multiply(
					constant,
					Expression.Multiply(leftpart, right)
				)
			);
		}

		/// <summary>
		/// Distributes multiplication if one side is <c>(constant * part)</c>, e.g.,
		/// <c>x * (c * y)</c> => <c>c * (x * y)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationOfEqualsElements(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
		{
			Expression constant;
			Expression rightpart;

			if (right.Left.NodeType == ExpressionType.Constant)
			{
				constant = right.Left;
				rightpart = right.Right;
			}
			else if (right.Right.NodeType == ExpressionType.Constant)
			{
				constant = right.Right;
				rightpart = right.Left;
			}
			else
			{
				return null;
			}

			return Transform(
				Expression.Multiply(
					constant,
					Expression.Multiply(left, rightpart)
				)
			);
		}

		/// <summary>
		/// Attempts to combine exponent factors if they share a common base, e.g.
		/// <c>(x^a) * (x^b)</c> => <c>x^(a+b)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Multiply)]
		protected Expression MultiplicationOfEqualsElements(BinaryExpression e, Expression left, Expression right)
		{
			Expression leftleft;
			Expression leftright;
			if (left.NodeType == ExpressionType.Power)
			{
				var _left = (BinaryExpression)left;
				leftleft = _left.Left;
				leftright = _left.Right;
			}
			else
			{
				leftleft = left;
				leftright = Expression.Constant(Convert.ChangeType(1, left.Type));
			}

			Expression rightleft;
			Expression rightright;
			if (right.NodeType == ExpressionType.Power)
			{
				var _right = (BinaryExpression)right;
				rightleft = _right.Left;
				rightright = _right.Right;
			}
			else
			{
				rightleft = right;
				rightright = Expression.Constant(Convert.ChangeType(1, right.Type));
			}

			if (ExpressionComparer.Default.Equals(leftleft, rightleft))
			{
				return Transform(
					Expression.Power(
						leftleft,
						Transform(Expression.Add(leftright, rightright))
					)
				);
			}
			return null;
		}

		/// <summary>
		/// Simplifies nested divisions, e.g. <c>(x / y) / (z / w)</c> => <c>(x*w) / (y*z)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionOfDivision(BinaryExpression e,
			[ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
			[ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
		{
			return Expression.Divide(
				Transform(Expression.Multiply(left.Left, right.Right)),
				Transform(Expression.Multiply(left.Right, right.Left))
			);
		}

		/// <summary>
		/// Simplifies nested divisions, e.g. <c>x / (y / z)</c> => <c>(x*z) / y</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionOfDivision(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
		{
			return Expression.Divide(
				Transform(Expression.Multiply(left, right.Right)),
				right.Left
			);
		}

		/// <summary>
		/// Simplifies nested divisions, e.g. <c>(x / y) / z</c> => <c>x / (y*z)</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Divide)]
		protected Expression DivisionOfDivision(BinaryExpression e,
			[ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
			Expression right)
		{
			return Expression.Divide(
				left.Left,
				Transform(Expression.Multiply(left.Right, right))
			);
		}

		#endregion

		#region Power

		/// <summary>
		/// Simplifies power expressions when both base and exponent are numeric constants,
		/// e.g. <c>(2)^(3)</c> => <c>8</c>.
		/// </summary>
		[ExpressionSignature(ExpressionType.Power)]
		protected Expression PowerOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
		{
			var result = double.Pow((double)left.Value, (double)right.Value);
			return Expression.Constant(result);
		}

		#endregion

		#region Lambda/Invoke

		/// <summary>
		/// Inlines or transforms an <see cref="InvocationExpression"/> that calls a lambda
		/// by substituting the lambda parameters with the invocation arguments and re-transforming the result.
		/// </summary>
		[ExpressionSignature(ExpressionType.Invoke)]
		protected Expression InvokeExpression(InvocationExpression expression)
		{
			if (expression.Expression is LambdaExpression le)
			{
				var newArguments = expression.Arguments.ToArray();
				var oldArguments = le.Parameters.ToArray();
				return Transform(ReplaceArguments(le.Body, oldArguments, newArguments));
			}
			return expression;
		}

		#endregion

		/// <summary>
		/// Called when no custom transformation method matches. In this partial class, it
		/// copies the expression structure using <see cref="CopyExpression"/> as the default final step.
		/// </summary>
		/// <param name="e">The expression to finalize.</param>
		/// <param name="parameters">Sub-expressions or operands.</param>
		/// <returns>A copy of <paramref name="e"/> with sub-expressions replaced by <paramref name="parameters"/>.</returns>
		protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
		{
			return CopyExpression(e, parameters);
		}
	}
}
