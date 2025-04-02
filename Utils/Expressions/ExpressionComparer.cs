using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides a custom equality comparer for <see cref="Expression"/> objects.
/// The comparison involves simplifying each expression via an <see cref="ExpressionSimplifier"/>
/// before checking for structural and semantic equivalence.
/// </summary>
public class ExpressionComparer : IEqualityComparer<Expression>
{
	/// <summary>
	/// A shared <see cref="ExpressionSimplifier"/> instance used to simplify expressions
	/// before they are compared.
	/// </summary>
	private static readonly ExpressionSimplifier _expressionSimplifier = new ExpressionSimplifier();

	/// <summary>
	/// Prevents direct instantiation outside of this class. Use <see cref="Default"/> instead.
	/// </summary>
	private ExpressionComparer() { }

	/// <summary>
	/// Gets the default <see cref="ExpressionComparer"/> instance for global usage.
	/// </summary>
	public static ExpressionComparer Default { get; } = new ExpressionComparer();

	/// <summary>
	/// Determines whether two <see cref="Expression"/> objects are equal by simplifying
	/// and comparing them structurally.
	/// </summary>
	/// <param name="x">The first expression to compare.</param>
	/// <param name="y">The second expression to compare.</param>
	/// <returns>
	/// <see langword="true"/> if both expressions are considered equivalent after simplification;
	/// otherwise <see langword="false"/>.
	/// </returns>
	public bool Equals(Expression x, Expression y)
	{
		var xParameters = x is LambdaExpression xLambda ? xLambda.Parameters.ToArray() : null;
		var yParameters = y is LambdaExpression yLambda ? yLambda.Parameters.ToArray() : null;

		x = _expressionSimplifier.Simplify(x);
		y = _expressionSimplifier.Simplify(y);

		return Equals(x, xParameters, y, yParameters);
	}

	/// <summary>
	/// Provides a recursive equality check between two expressions, along with their parameters.
	/// </summary>
	/// <param name="x">The first expression after simplification.</param>
	/// <param name="xParams">Any parameter expressions for <paramref name="x"/> if it is a lambda.</param>
	/// <param name="y">The second expression after simplification.</param>
	/// <param name="yParams">Any parameter expressions for <paramref name="y"/> if it is a lambda.</param>
	/// <returns>
	/// <see langword="true"/> if both expressions are structurally equivalent; otherwise <see langword="false"/>.
	/// </returns>
	private bool Equals(Expression x, ParameterExpression[] xParams, Expression y, ParameterExpression[] yParams)
	{
		if (x == y) return true;
		if (x.NodeType != y.NodeType) return false;

		if (x is LambdaExpression xl && y is LambdaExpression yl)
		{
			var newXParams = xl.Parameters.ToArray();
			var newYParams = yl.Parameters.ToArray();

			if (newXParams.Length != newYParams.Length) return false;
			for (int i = 0; i < newXParams.Length; i++)
			{
				if (newXParams[i].Type != newYParams[i].Type) return false;
			}
			return Equals(xl.Body, newXParams, yl.Body, newYParams);
		}

		if (x is ConstantExpression xco && y is ConstantExpression yco)
		{
			// If not numeric, compare by direct value
			if (!xco.Type.In(Types.Number) || !yco.Type.In(Types.Number))
			{
				return xco.Value.Equals(yco.Value);
			}

			// Attempt numeric comparison across different numeric types
			bool TryCompareNumber(ConstantExpression xConst, ConstantExpression yConst, Type candidateType, out bool result)
			{
				result = false;
				if (xConst.Type != candidateType && yConst.Type != candidateType) return false;

				// If the underlying numeric type doesn't match or is smaller than the candidate, skip
				if (Marshal.SizeOf(xco.Type) > Marshal.SizeOf(candidateType)) return false;
				if (Marshal.SizeOf(yco.Type) > Marshal.SizeOf(candidateType)) return false;

				result = Convert.ChangeType(xConst.Value, candidateType)
							.Equals(Convert.ChangeType(yConst.Value, candidateType));
				return true;
			}

			// Check each numeric type known in Types.Number
			foreach (var type in Types.Number)
			{
				if (TryCompareNumber(xco, yco, type, out var result))
				{
					return result;
				}
			}
			return false;
		}

		if (x is ParameterExpression xpe && y is ParameterExpression ype)
		{
			int xi = xParams?.IndexOf(e => e.Name == xpe.Name) ?? -1;
			int yi = yParams?.IndexOf(e => e.Name == ype.Name) ?? -1;
			return xi != -1 && yi != -1 && xi == yi;
		}

		if (x is UnaryExpression xuo && y is UnaryExpression yuo)
		{
			return Equals(xuo.Operand, xParams, yuo.Operand, yParams);
		}

		if (x is BinaryExpression xbo && y is BinaryExpression ybo)
		{
			return Equals(xbo.Left, xParams, ybo.Left, yParams)
				&& Equals(xbo.Right, xParams, ybo.Right, yParams);
		}

		if (x is MethodCallExpression xmco && y is MethodCallExpression ymco)
		{
			if (!(xmco.Type == ymco.Type
				  && xmco.Object == ymco.Object
				  && xmco.Method == ymco.Method
				  && xmco.Arguments.Count == ymco.Arguments.Count))
			{
				return false;
			}

			for (int i = 0; i < xmco.Arguments.Count; i++)
			{
				if (!Equals(xmco.Arguments[i], xParams, ymco.Arguments[i], yParams))
				{
					return false;
				}
			}
			return true;
		}

		if (x is MemberExpression xmo && y is MemberExpression ymo)
		{
			return xmo.Type == ymo.Type
				&& Equals(xmo.Expression, yParams, ymo.Expression, yParams)
				&& xmo.Member == ymo.Member;
		}

		return false;
	}

	/// <summary>
	/// Returns a hash code for the specified <see cref="Expression"/> by simplifying it and taking
	/// the hash code of its string representation.
	/// </summary>
	/// <param name="obj">The <see cref="Expression"/> for which to get a hash code.</param>
	/// <returns>An integer hash code based on the simplified expression's string representation.</returns>
	public int GetHashCode(Expression obj) 
		=> _expressionSimplifier.Simplify(obj).ToString().GetHashCode();
}
