using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

public partial class ExpressionSimplifier : ExpressionTranformer
{
	public Expression Simplify(Expression e)
	{
		return Transform(e);
	}

	protected override Expression PrepareExpression(Expression e)
	{
		return Transform(e);
	}

	#region operations avec 0 et 1
	[ExpressionSignature(ExpressionType.Add)]
	public Expression AdditionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0.0)] ConstantExpression right)
	{
		if ((double)right.Value == 0.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Add)]
	public Expression AdditionWithZero(BinaryExpression e, [ConstantNumeric(0.0)] ConstantExpression left, Expression right)
	{
		if ((double)left.Value == 0.0) { return right; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	public Expression SubstractionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0.0)] ConstantExpression right)
	{
		if ((double)right.Value == 0.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	public Expression SubstractionWithZero(BinaryExpression e, [ConstantNumeric(0.0)] ConstantExpression left, Expression right)
	{
		if ((double)left.Value == 0.0) { return Transform(Expression.Negate(right)); }
		return null;
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	public Expression MultiplicationWithZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0.0, 1.0)] ConstantExpression right)
	{
		if ((double)right.Value == 0.0) { return right; }
		if ((double)right.Value == 1.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	public Expression MultiplicationWithZeroOrOne(BinaryExpression e, [ConstantNumeric(0.0, 1.0)] ConstantExpression left, Expression right)
	{
		if ((double)left.Value == 0.0) { return left; }
		if ((double)left.Value == 1.0) { return right; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Divide)]
	public Expression DivideWithZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0.0, 1.0)] ConstantExpression right)
	{
		if ((double)right.Value == 0.0) { throw new DivideByZeroException(); }
		if ((double)right.Value == 1.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Divide)]
	public Expression DivideWithZero(BinaryExpression e, [ConstantNumeric(0.0)] ConstantExpression left, Expression right)
	{
		if ((double)left.Value == 0.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Power)]
	public Expression PowerOfZeroOrOne(BinaryExpression e, [ConstantNumeric(0.0, 1.0)] ConstantExpression left, Expression right)
	{
		if ((double)left.Value == 0.0) { return left; }
		if ((double)left.Value == 1.0) { return left; }
		return null;
	}

	[ExpressionSignature(ExpressionType.Power)]
	public Expression PowerByZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0.0, 1.0)] ConstantExpression right)
	{
		if ((double)right.Value == 0.0) { return Expression.Constant(Convert.ChangeType(1.0, right.Type)); }
		if ((double)right.Value == 1.0) { return left; }
		return null;
	}

	#endregion

	#region addition
	[ExpressionSignature(ExpressionType.Add)]
	protected Expression AdditionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
	{
		return Expression.Constant((double)left.Value + (double)right.Value);
	}

	[ExpressionSignature(ExpressionType.Add)]
	protected Expression AdditionWithNegate(
		BinaryExpression e,
		Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
	{
		return Transform(Expression.Subtract(left, right.Operand));
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression SubstractionWithNegate(
		BinaryExpression e,
		Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
	{
		return Transform(Expression.Add(left, right.Operand));
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression SubstractionWithNegate(
		BinaryExpression e,
		[ExpressionSignature(ExpressionType.Negate)] UnaryExpression left,
		Expression right)
	{
		return Transform(Expression.Negate(Expression.Add(left.Operand, right)));
	}

	[ExpressionSignature(ExpressionType.Negate)]
	protected Expression NegateWithSubstraction(
		UnaryExpression e,
		[ExpressionSignature(ExpressionType.Subtract)] BinaryExpression operand)
	{
		return Transform(Expression.Subtract(operand.Right, operand.Left));
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression SubstractionWithAddition(
		BinaryExpression e,
		Expression left, [ExpressionSignature(ExpressionType.Add)] BinaryExpression right)
	{
		return Expression.Subtract(Expression.Subtract(left, right.Left), right.Right);
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression SubstractionWithSubstraction(
		BinaryExpression e,
		Expression left, [ExpressionSignature(ExpressionType.Subtract)] BinaryExpression right)
	{
		return Expression.Subtract(Expression.Add(left, right.Right), right.Left);
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression SubstractionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
	{
		return Expression.Constant((double)left.Value - (double)right.Value);
	}

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
			leftleft = Expression.Constant(1.0);
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
			rightleft = Expression.Constant(1.0);
			rightright = right;
		}

		if (leftAugmented && leftright is ConstantExpression && ((ConstantExpression)rightright).Value as double? == 1.0) return null;
		if (rightAugmented && rightright is ConstantExpression && ((ConstantExpression)rightright).Value as double? == 1.0) return null;

		//on pousse à droite les termes à factoriser
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
		}
		else
		{
			return null;
		}

		return Transform(Expression.Multiply(
			Transform(Expression.Add(leftleft, rightleft)),
			leftright
			));

	}

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
			leftleft = Expression.Constant(1.0);
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
			rightleft = Expression.Constant(1.0);
			rightright = right;
		}

		//o, pousse à droite les termes à factoriser
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
		}
		else
		{
			return null;
		}

		return Transform(Expression.Multiply(
			Transform(Expression.Subtract(leftleft, rightleft)),
			leftright
			));

	}
	#endregion

	#region multiplication
	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression MultiplicationOfConstants(
		BinaryExpression e,
		[ConstantNumeric] ConstantExpression left,
		[ConstantNumeric] ConstantExpression right
	)
	{
		return Expression.Constant((double)left.Value * (double)right.Value);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression Multiplication(
		BinaryExpression e,
		Expression left,
		[ConstantNumeric] ConstantExpression right
	)
	{
		return Expression.Multiply(right, left);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression Multiplication(
		BinaryExpression e,
		[ConstantNumeric] ConstantExpression left,
		[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right
	)
	{
		if (right.Left is ConstantExpression)
		{
			return Expression.Multiply(
				Expression.Constant((double)left.Value * (double)((ConstantExpression)right.Left).Value),
				right.Right
			);
		}
		return null;
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression Multiplication(
		BinaryExpression e,
		[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left,
		[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right
	)
	{
		if (left.Left is ConstantExpression && right.Left is ConstantExpression)
		{
			return Expression.Multiply(
				Expression.Constant((double)((ConstantExpression)left.Left).Value * (double)((ConstantExpression)right.Left).Value),
				Transform(Expression.Multiply(left.Right, right.Right))
			);
		}
		return null;
	}

	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionOfConstants(
		BinaryExpression e,
		[ConstantNumeric] ConstantExpression left,
		[ConstantNumeric] ConstantExpression right
	)
	{
		return Expression.Constant((double)left.Value / (double)right.Value);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression MultiplicationWithNegate(
		BinaryExpression e,
		Expression left,
		[ExpressionSignature(ExpressionType.Negate)] UnaryExpression right
	)
	{
		return Expression.Negate(
			Transform(Expression.Multiply(left, right.Operand))
		);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression MultiplicationWithNegate(
		BinaryExpression e,
		[ExpressionSignature(ExpressionType.Negate)] UnaryExpression left,
		Expression right
	)
	{
		return Expression.Negate(
			Transform(Expression.Multiply(left.Operand, right))
		);
	}

	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionWithNegate(
		BinaryExpression e,
		Expression left,
		[ExpressionSignature(ExpressionType.Negate)] UnaryExpression right
	)
	{
		return Expression.Negate(
			Transform(Expression.Divide(left, right.Operand))
		);
	}


	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionWithNegate(
		BinaryExpression e,
		[ExpressionSignature(ExpressionType.Negate)] UnaryExpression left,
		Expression right
	)
	{
		return Expression.Negate(
			Transform(Expression.Divide(left.Operand, right))
		);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression MultiplicationOfEqualsElements(
		BinaryExpression e,
		[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left,
		Expression right)
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
				Expression.Multiply(
					leftpart,
					right
				)
			)
		);
	}

	[ExpressionSignature(ExpressionType.Multiply)]
	protected Expression MultiplicationOfEqualsElements(
		BinaryExpression e,
		Expression left,
		[ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
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
				Expression.Multiply(
					left,
					rightpart
				)
			)
		);
	}

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
			leftright = Expression.Constant(1.0);
		}

		Expression rightleft;
		Expression rightright;
		if (right.NodeType == ExpressionType.Power)
		{
			rightleft = ((BinaryExpression)right).Left;
			rightright = ((BinaryExpression)right).Right;
		}
		else
		{
			rightleft = right;
			rightright = Expression.Constant(1.0);
		}

		//on pousse à droite les termes à factoriser
		if (ExpressionComparer.Default.Equals(leftleft, rightleft))
		{
			return Transform(
				Expression.Power(
					leftleft,
					Transform(Expression.Add(leftright, rightright))
				)
			);
		}
		else
		{
			return null;
		}
	}

	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionOfDivision(BinaryExpression e,
		[ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
		[ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
	{
		return Expression.Divide(
			Transform(Expression.Multiply(left.Left, right.Right)),
			Transform(Expression.Multiply(left.Right, right.Left)));
	}

	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionOfDivision(BinaryExpression e,
	Expression left,
	[ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
	{
		return Expression.Divide(Transform(Expression.Multiply(left, right.Right)), right.Left);
	}

	[ExpressionSignature(ExpressionType.Divide)]
	protected Expression DivisionOfDivision(BinaryExpression e,
		[ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
		Expression right)
	{
		return Expression.Divide(left.Left, Transform(Expression.Multiply(left.Right, right)));
	}
    #endregion

    #region Power
    [ExpressionSignature(ExpressionType.Power)]
    protected Expression PowerOfConstants(
    BinaryExpression e,
    [ConstantNumeric] ConstantExpression left,
    [ConstantNumeric] ConstantExpression right
)
    {
        var result = double.Pow((double)left.Value, (double)right.Value);
        return Expression.Constant(result);
    }
    #endregion

    #region lambda
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

    protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
	{
		return CopyExpression(e, parameters);
	}

}
