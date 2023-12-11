using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Mathematics.Expressions;

public partial class ExpressionSimplifier
{
	#region Power

	[ExpressionCallSignature(typeof(IPowerFunctions<>), nameof(IPowerFunctions<double>.Pow))]
	protected Expression PowerConvertionNumber(Expression e, Expression left, Expression right)
	{
		return Transform(Expression.Power(left, right));
	}
	#endregion

	#region logarithm 

	[ExpressionSignature(ExpressionType.Add)]
	protected Expression LogarithmSimplificationAddNumber(Expression e,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
	{
		return Expression.Call(
			typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log", [left.Type]),
			Transform(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression LogarithmSimplificationSubstractNumber(Expression e,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
	{
		return Expression.Call(
            typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log", [left.Type]),
            Transform(Expression.Divide(left.Arguments[0], right.Arguments[0])));
	}

	protected Expression Logarithm10SimplificationAddNumber(Expression e,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
		[ExpressionCallSignature(typeof(ILogarithmicFunctions<>), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
	{
		return Expression.Call(
            typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log10", [left.Type]),
            Transform(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
	}

	[ExpressionSignature(ExpressionType.Subtract)]
	protected Expression Logarithm10SimplificationSubstractNumber(Expression e,
		[ExpressionCallSignature(typeof(Math), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
		[ExpressionCallSignature(typeof(Math), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
	{
		return Expression.Call(
            typeof(ILogarithmicFunctions<>).GetStaticMethod([left.Type], "Log10", [left.Type]),
            Transform(Expression.Divide(left.Arguments[0], right.Arguments[0])));
	}

	#endregion

	#region trigonometry
	[ExpressionSignature(ExpressionType.Add)]
	protected Expression AdditionOfCos2andSin2Number(Expression e,
		[ExpressionSignature(ExpressionType.Power)] BinaryExpression left,
		[ExpressionSignature(ExpressionType.Power)] BinaryExpression right)
	{
		var leftRight = (left.Right as ConstantExpression);
		var rightRight = (right.Right as ConstantExpression);
		if ((double)leftRight.Value != 2 || (double)rightRight.Value != 2) return null;

		var leftLeft = (left.Left as MethodCallExpression);
		var rightLeft = (right.Left as MethodCallExpression);
		if (leftLeft is null || rightLeft is null) return null;

		var sin = typeof(ITrigonometricFunctions<>).GetStaticMethod([left.Type], "Sin", [left.Type]);
		var cos = typeof(ITrigonometricFunctions<>).GetStaticMethod([left.Type], "Cos", [left.Type]);

		if (!(leftLeft.Method == sin && rightLeft.Method == cos || leftLeft.Method == cos && rightLeft.Method == sin)) return null;

		if (ExpressionComparer.Default.Equals(leftLeft.Arguments[0], rightLeft.Arguments[0])) return Expression.Constant(1.0);

		return null;
	}


	#endregion

}
