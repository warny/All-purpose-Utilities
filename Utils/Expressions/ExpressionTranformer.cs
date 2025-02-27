using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Reflection.BindingFlags;
using System.Collections.Immutable;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Provides an abstract base class to transform or rewrite LINQ expression trees.
/// Subclasses may override the transformation logic for specific expression signatures.
/// </summary>
public abstract class ExpressionTransformer
{
	private static readonly Type TypeOfExpression = typeof(Expression);

	/// <summary>
	/// A list of instance methods on this transformer type that are decorated with
	/// <see cref="ExpressionSignatureAttribute"/>. Each entry stores the method,
	/// the attribute itself, and its parameter info.
	/// </summary>
	private IReadOnlyList<(MethodInfo Method, ExpressionSignatureAttribute Attribute, ParameterInfo[] Parameters)> TransformMethods { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ExpressionTransformer"/> class.
	/// During construction, it gathers all methods marked with <see cref="ExpressionSignatureAttribute"/>
	/// from the derived type.
	/// </summary>
	protected ExpressionTransformer()
	{
		Type t = GetType();
		TransformMethods =
			t.GetMethods(Public | NonPublic | InvokeMethod | Instance)
			 .Select(m => (Method: m, Attr: m.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault()))
			 .Where(ma => ma.Attr != null)
			 .Select(ma => (ma.Method, ma.Attr, ma.Method.GetParameters()))
			 .ToImmutableList();
	}

	/// <summary>
	/// Prepares an expression for transformation. Subclasses can override this to apply
	/// initial logic before the main <see cref="Transform(Expression)"/> switch (e.g. caching).
	/// The default implementation returns the expression unchanged.
	/// </summary>
	/// <param name="e">The expression to prepare.</param>
	/// <returns>The prepared expression.</returns>
	protected virtual Expression PrepareExpression(Expression e) => e;

	/// <summary>
	/// Applies transformation rules to a given expression, returning a (potentially) modified expression.
	/// This method checks for known signatures (via <see cref="ExpressionSignatureAttribute"/>-annotated methods)
	/// and if a match is found, invokes the corresponding transformation function.
	/// If no signature method matches, it calls <see cref="FinalizeExpression"/> by default.
	/// </summary>
	/// <param name="e">The expression to transform.</param>
	/// <returns>A possibly rewritten expression.</returns>
	protected Expression Transform(Expression e)
	{
		// Prepare the initial parameters array and an array of sub-expressions (if any).
		object[] parameters;
		Expression[] expressionParameters;

		switch (e)
		{
			case ConstantExpression cc:
				expressionParameters = Array.Empty<Expression>();
				parameters = [cc, cc.Value];
				break;

			case UnaryExpression ue:
				expressionParameters = [PrepareExpression(ue.Operand)];
				e = ue = (UnaryExpression)CopyExpression(e, expressionParameters);
				parameters = new object[] { ue, ue.Operand };
				break;

			case BinaryExpression be:
				expressionParameters =
				[
					PrepareExpression(be.Left),
					PrepareExpression(be.Right)
				];
				e = be = (BinaryExpression)CopyExpression(e, expressionParameters);
				parameters = [be, be.Left, be.Right];
				break;

			case MethodCallExpression mce:
				{
					expressionParameters = mce.Arguments.Select(PrepareExpression).ToArray();
					e = mce = (MethodCallExpression)CopyExpression(e, expressionParameters);
					parameters = new object[mce.Arguments.Count + 1];
					parameters[0] = mce;
					Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
					break;
				}

			case ParameterExpression pe:
				expressionParameters = Array.Empty<Expression>();
				parameters = [pe];
				break;

			case InvocationExpression ie:
				{
					expressionParameters = ie.Arguments.Select(PrepareExpression).ToArray();
					e = ie = (InvocationExpression)CopyExpression(e, expressionParameters);
					parameters = new object[ie.Arguments.Count + 1];
					parameters[0] = ie;
					Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
					break;
				}

			case LambdaExpression le:
				{
					// Recursively transform the body, and prepare parameter expressions
					expressionParameters = le.Parameters
											 .Select(a => (ParameterExpression)PrepareExpression(a))
											 .ToArray();
					e = le = Expression.Lambda(Transform(le.Body), (ParameterExpression[])expressionParameters);
					parameters = new object[le.Parameters.Count + 1];
					parameters[0] = le;
					Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
					break;
				}

			default:
				expressionParameters = Array.Empty<Expression>();
				parameters = new object[] { e };
				break;
		}

		// Attempt to find a matching transform method marked with ExpressionSignatureAttribute.
		foreach ((var method, var attr, var parametersInfo) in TransformMethods)
		{
			// If the attribute doesn't match the expression type, skip
			if (!attr.Match(e))
				continue;

			// The method must return an Expression (or derived) type
			if (!TypeOfExpression.IsAssignableFrom(method.ReturnType))
				throw new InvalidProgramException("Transform method must return an Expression type.");

			// The first parameter must match the main expression
			if (!parametersInfo[0].ParameterType.IsInstanceOfType(parameters[0]))
				continue;

			object result;

			// If more than one parameter, handle special cases for Expression[] or typed arguments
			if (parametersInfo.Length > 1)
			{
				if (parametersInfo[1].ParameterType == typeof(Expression[]))
				{
					// The second parameter is the array of sub-expressions
					result = method.Invoke(this, new object[] { e, expressionParameters });
				}
				else
				{
					// Validate each expression parameter against the method parameter types
					bool isValid = true;
					for (int i = 1; i < parametersInfo.Length; i++)
					{
						if (parameters[i] is Expression paramExpr)
						{
							if (!CheckParameter(paramExpr, parametersInfo[i]))
							{
								isValid = false;
								break;
							}
						}
						else
						{
							// If it's not an Expression, check if we can assign directly
							if (!parametersInfo[i].ParameterType.IsAssignableFrom(parameters[i].GetType()))
							{
								isValid = false;
								break;
							}
						}
					}

					if (!isValid) continue;

					result = method.Invoke(this, parameters);
					if (result is null) continue;
				}
			}
			// If exactly one parameter, just invoke with [ expressionObject ]
			else if (parametersInfo.Length == 1)
			{
				result = method.Invoke(this, new[] { parameters[0] });
			}
			else
			{
				// No valid parameters => skip
				continue;
			}

			return (Expression)result;
		}

		// If no custom transform method was used, finalize the expression
		if (e is ConstantExpression)
		{
			// Typically finalize constants with no sub-expressions
			return FinalizeExpression(e, Array.Empty<Expression>());
		}
		else
		{
			// Other expression types finalize using their sub-expressions
			return FinalizeExpression(e, expressionParameters);
		}
	}

	/// <summary>
	/// Called if no custom transformation method (annotated with <see cref="ExpressionSignatureAttribute"/>)
	/// is found. Allows final post-processing. The default implementation throws an exception.
	/// </summary>
	/// <param name="e">The expression being finalized.</param>
	/// <param name="parameters">The sub-expressions or operands for <paramref name="e"/>.</param>
	/// <returns>A finalized expression.</returns>
	/// <exception cref="Exception">Thrown by default to indicate that transformation cannot be completed.</exception>
	protected virtual Expression FinalizeExpression(Expression e, Expression[] parameters)
	{
		throw new Exception("The expression transformation cannot be finalized.");
	}

	/// <summary>
	/// Replaces all occurrences of <paramref name="oldParameters"/> within <paramref name="e"/>
	/// with the corresponding items in <paramref name="newParameters"/>.
	/// </summary>
	/// <param name="e">The expression in which parameter references are replaced.</param>
	/// <param name="oldParameters">The parameters to remove.</param>
	/// <param name="newParameters">The new expressions that replace <paramref name="oldParameters"/>.</param>
	/// <returns>A copy of <paramref name="e"/> where specified parameters are replaced.</returns>
	protected Expression ReplaceArguments(Expression e, ParameterExpression[] oldParameters, Expression[] newParameters)
	{
		switch (e)
		{
			case ParameterExpression pe:
				{
					int i = Array.IndexOf(oldParameters, pe);
					return i >= 0 ? newParameters[i] : e;
				}
			case UnaryExpression ue:
				return CopyExpression(ue, ReplaceArguments(ue.Operand, oldParameters, newParameters));

			case BinaryExpression be:
				{
					var left = ReplaceArguments(be.Left, oldParameters, newParameters);
					var right = ReplaceArguments(be.Right, oldParameters, newParameters);
					return CopyExpression(be, left, right);
				}
			case InvocationExpression ie:
				{
					var arguments = ie.Arguments
									  .Select(a => ReplaceArguments(a, oldParameters, newParameters))
									  .ToArray();
					return CopyExpression(ie, arguments);
				}
			case MethodCallExpression mce:
				{
					var arguments = mce.Arguments
									   .Select(a => ReplaceArguments(a, oldParameters, newParameters))
									   .ToArray();
					return CopyExpression(mce, arguments);
				}
		}
		return e;
	}

	/// <summary>
	/// Creates a new expression of the same <see cref="ExpressionType"/> as <paramref name="e"/>,
	/// using the supplied <paramref name="parameters"/> as sub-expressions or arguments.
	/// If certain <see cref="ExpressionType"/> values are not supported by this switch,
	/// they are simply returned as-is or an exception is thrown.
	/// </summary>
	/// <param name="e">The original expression to copy.</param>
	/// <param name="parameters">The sub-expressions to insert into the copied expression.</param>
	/// <returns>A new expression replicating the structure of <paramref name="e"/> with
	/// possibly different sub-expressions.</returns>
	protected Expression CopyExpression(Expression e, params Expression[] parameters)
	{
		return e.NodeType switch
		{
			ExpressionType.Add => Expression.Add(parameters[0], parameters[1]),
			ExpressionType.AddChecked => Expression.AddChecked(parameters[0], parameters[1]),
			ExpressionType.And => Expression.And(parameters[0], parameters[1]),
			ExpressionType.AndAlso => Expression.AndAlso(parameters[0], parameters[1]),
			ExpressionType.ArrayLength => Expression.ArrayLength(parameters[0]),
			ExpressionType.ArrayIndex => Expression.ArrayIndex(parameters[0], parameters[1]),
			ExpressionType.Call => Expression.Call(((MethodCallExpression)e).Method, parameters),
			ExpressionType.Coalesce => Expression.Coalesce(parameters[0], parameters[1]),
			ExpressionType.Conditional => Expression.Condition(parameters[0], parameters[1], parameters[2]),
			ExpressionType.Constant => Expression.Constant(((ConstantExpression)e).Value, e.Type),
			ExpressionType.Convert => Expression.Convert(parameters[0], ((UnaryExpression)e).Type),
			ExpressionType.ConvertChecked => Expression.ConvertChecked(parameters[0], ((UnaryExpression)e).Type),
			ExpressionType.Divide => Expression.Divide(parameters[0], parameters[1]),
			ExpressionType.Equal => Expression.Equal(parameters[0], parameters[1]),
			ExpressionType.ExclusiveOr => Expression.ExclusiveOr(parameters[0], parameters[1]),
			ExpressionType.GreaterThan => Expression.GreaterThan(parameters[0], parameters[1]),
			ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(parameters[0], parameters[1]),
			ExpressionType.Invoke => Expression.Invoke(((InvocationExpression)e).Expression, parameters),
			ExpressionType.Lambda => e,
			ExpressionType.LeftShift => Expression.LeftShift(parameters[0], parameters[1]),
			ExpressionType.LessThan => Expression.LessThan(parameters[0], parameters[1]),
			ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(parameters[0], parameters[1]),
			ExpressionType.ListInit => e,
			ExpressionType.MemberAccess => e,
			ExpressionType.MemberInit => e,
			ExpressionType.Modulo => Expression.Modulo(parameters[0], parameters[1]),
			ExpressionType.Multiply => Expression.Multiply(parameters[0], parameters[1]),
			ExpressionType.MultiplyChecked => Expression.MultiplyChecked(parameters[0], parameters[1]),
			ExpressionType.Negate => Expression.Negate(parameters[0]),
			ExpressionType.UnaryPlus => Expression.UnaryPlus(parameters[0]),
			ExpressionType.NegateChecked => Expression.NegateChecked(parameters[0]),
			ExpressionType.New => Expression.New(((NewExpression)e).Constructor, parameters),
			ExpressionType.NewArrayInit => Expression.NewArrayInit(((NewArrayExpression)e).Type.GetElementType()!, parameters),
			ExpressionType.NewArrayBounds => Expression.NewArrayBounds(((NewArrayExpression)e).Type.GetElementType()!, parameters),
			ExpressionType.Not => Expression.Not(parameters[0]),
			ExpressionType.NotEqual => Expression.NotEqual(parameters[0], parameters[1]),
			ExpressionType.Or => Expression.Or(parameters[0], parameters[1]),
			ExpressionType.OrElse => Expression.OrElse(parameters[0], parameters[1]),
			ExpressionType.Parameter => e,
			ExpressionType.Power => Expression.Power(parameters[0], parameters[1]),
			ExpressionType.Quote => Expression.Quote(parameters[0]),
			ExpressionType.RightShift => Expression.RightShift(parameters[0], parameters[1]),
			ExpressionType.Subtract => Expression.Subtract(parameters[0], parameters[1]),
			ExpressionType.SubtractChecked => Expression.SubtractChecked(parameters[0], parameters[1]),
			ExpressionType.TypeAs => Expression.TypeAs(parameters[0], ((UnaryExpression)e).Type),
			ExpressionType.TypeIs => Expression.TypeIs(parameters[0], ((TypeBinaryExpression)e).TypeOperand),
			ExpressionType.TypeEqual => Expression.TypeEqual(parameters[0], ((TypeBinaryExpression)e).TypeOperand),
			ExpressionType.Assign => Expression.Assign(parameters[0], parameters[1]),
			ExpressionType.Block => Expression.Block(parameters),
			ExpressionType.DebugInfo => e,
			ExpressionType.Decrement => Expression.Decrement(parameters[0]),
			ExpressionType.Dynamic => e,
			ExpressionType.Default => e,
			ExpressionType.Extension => e,
			ExpressionType.Goto => e,
			ExpressionType.Increment => Expression.Increment(parameters[0]),
			ExpressionType.Index => e,
			ExpressionType.Label => e,
			ExpressionType.RuntimeVariables => e,
			ExpressionType.Loop => Expression.Loop(parameters[0]),
			ExpressionType.Switch => e,
			ExpressionType.Throw => Expression.Throw(parameters[0]),
			ExpressionType.Try => e,
			ExpressionType.Unbox => Expression.Unbox(parameters[0], ((UnaryExpression)e).Type),
			ExpressionType.AddAssign => Expression.AddAssign(parameters[0], parameters[1]),
			ExpressionType.AndAssign => Expression.AndAssign(parameters[0], parameters[1]),
			ExpressionType.DivideAssign => Expression.DivideAssign(parameters[0], parameters[1]),
			ExpressionType.ExclusiveOrAssign => Expression.ExclusiveOrAssign(parameters[0], parameters[1]),
			ExpressionType.LeftShiftAssign => Expression.LeftShiftAssign(parameters[0], parameters[1]),
			ExpressionType.ModuloAssign => Expression.ModuloAssign(parameters[0], parameters[1]),
			ExpressionType.MultiplyAssign => Expression.MultiplyAssign(parameters[0], parameters[1]),
			ExpressionType.OrAssign => Expression.OrAssign(parameters[0], parameters[1]),
			ExpressionType.PowerAssign => Expression.PowerAssign(parameters[0], parameters[1]),
			ExpressionType.RightShiftAssign => Expression.RightShiftAssign(parameters[0], parameters[1]),
			ExpressionType.SubtractAssign => Expression.SubtractAssign(parameters[0], parameters[1]),
			ExpressionType.AddAssignChecked => Expression.AddAssignChecked(parameters[0], parameters[1]),
			ExpressionType.MultiplyAssignChecked => Expression.MultiplyAssignChecked(parameters[0], parameters[1]),
			ExpressionType.SubtractAssignChecked => Expression.SubtractAssignChecked(parameters[0], parameters[1]),
			ExpressionType.PreIncrementAssign => Expression.PreIncrementAssign(parameters[0]),
			ExpressionType.PreDecrementAssign => Expression.PreDecrementAssign(parameters[0]),
			ExpressionType.PostIncrementAssign => Expression.PostIncrementAssign(parameters[0]),
			ExpressionType.PostDecrementAssign => Expression.PostDecrementAssign(parameters[0]),
			ExpressionType.OnesComplement => Expression.OnesComplement(parameters[0]),
			ExpressionType.IsTrue => Expression.IsTrue(parameters[0]),
			ExpressionType.IsFalse => Expression.IsFalse(parameters[0]),
			_ => throw new NotSupportedException($"Expression type '{e.NodeType}' is not supported.")
		};
	}

	/// <summary>
	/// Checks whether the given expression matches the type specified by <paramref name="parameter"/>,
	/// and if it has a custom <see cref="ExpressionSignatureAttribute"/>, verifies that as well.
	/// </summary>
	/// <param name="e">The expression to validate.</param>
	/// <param name="parameter">The parameter that declared a signature requirement.</param>
	/// <returns>True if <paramref name="e"/> is valid for the parameter; otherwise false.</returns>
	private bool CheckParameter(Expression e, ParameterInfo parameter)
	{
		// Check if the expression type is compatible with the parameter
		if (!parameter.ParameterType.IsAssignableFrom(e.GetType()))
			return false;

		// If the parameter has its own ExpressionSignatureAttribute, ensure it matches
		var attribute = parameter.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault();
		if (attribute is null) return true;

		return attribute.Match(e);
	}
}

/// <summary>
/// Marks a method or parameter as having a signature requirement for a certain <see cref="ExpressionType"/>.
/// When used on a method, the method is considered for transformation if its attribute matches the current node type.
/// When used on a parameter, it further restricts which sub-expressions are permissible.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ExpressionSignatureAttribute : Attribute
{
	/// <summary>
	/// Gets the <see cref="ExpressionType"/> that this signature attribute matches. If set to -1,
	/// any <see cref="ExpressionType"/> is permitted.
	/// </summary>
	public ExpressionType ExpressionType { get; }

	/// <summary>
	/// Creates a new instance of <see cref="ExpressionSignatureAttribute"/> for a specific
	/// <see cref="ExpressionType"/>.
	/// </summary>
	/// <param name="expressionType">The node type to match, or -1 for any.</param>
	public ExpressionSignatureAttribute(ExpressionType expressionType)
	{
		ExpressionType = expressionType;
	}

	/// <summary>
	/// Indicates whether the given expression matches the requirements of this attribute.
	/// The default implementation checks <see cref="ExpressionType"/> or allows any if set to -1.
	/// </summary>
	/// <param name="e">The expression to match.</param>
	/// <returns>True if it matches; otherwise false.</returns>
	public virtual bool Match(Expression e)
	{
		return ExpressionType == (ExpressionType)(-1) || e.NodeType == ExpressionType;
	}
}

/// <summary>
/// Marks a method as matching only method-call expressions that invoke a specific function name
/// on a specific type. This is a specialized <see cref="ExpressionSignatureAttribute"/> for calls.
/// </summary>
public class ExpressionCallSignatureAttribute : ExpressionSignatureAttribute
{
	/// <summary>
	/// Gets the declaring type that should match the method call.
	/// </summary>
	public Type[] Types { get; }

	/// <summary>
	/// Gets the method name that should match the call.
	/// </summary>
	public string FunctionName { get; }

	/// <summary>
	/// Creates a new instance of <see cref="ExpressionCallSignatureAttribute"/> for calls
	/// to <paramref name="type"/>.<paramref name="functionName"/>.
	/// </summary>
	/// <param name="type">The declaring type of the target method.</param>
	/// <param name="functionName">The name of the method to match.</param>
	public ExpressionCallSignatureAttribute(Type type, string functionName)
		: base(ExpressionType.Call)
	{
		Types = [type];
		FunctionName = functionName;
	}

	/// <summary>
	/// Creates a new instance of <see cref="ExpressionCallSignatureAttribute"/> for calls
	/// to <paramref name="type"/>.<paramref name="functionName"/>.
	/// </summary>
	/// <param name="type">The declaring type of the target method.</param>
	/// <param name="functionName">The name of the method to match.</param>
	public ExpressionCallSignatureAttribute(Type[] types, string functionName)
		: base(ExpressionType.Call)
	{
		Types = types;
		FunctionName = functionName;
	}

	/// <inheritdoc />
	public override bool Match(Expression e)
	{
		if (e is not MethodCallExpression ec) return false;
		return Types.Any(ec.Method.DeclaringType.IsDefinedBy) && ec.Method.Name == FunctionName;
	}
}

/// <summary>
/// A specialized attribute that indicates the expected parameter is a <see cref="ConstantExpression"/>
/// holding a numeric value, optionally restricted to a specific set of allowed numeric values.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ConstantNumericAttribute : ExpressionSignatureAttribute
{
	/// <summary>
	/// The allowed numeric values, if any. If null, any numeric constant is allowed.
	/// </summary>
	public double[] Values { get; }

	/// <summary>
	/// Creates a new instance allowing any numeric constant.
	/// </summary>
	public ConstantNumericAttribute() : base(ExpressionType.Constant)
	{
		Values = null;
	}

	/// <summary>
	/// Creates a new instance allowing only the specified numeric values.
	/// </summary>
	/// <param name="values">The allowed numeric values.</param>
	public ConstantNumericAttribute(params double[] values) : base(ExpressionType.Constant)
	{
		Values = values;
	}

	/// <inheritdoc />
	public override bool Match(Expression e)
	{
		if (e is not ConstantExpression cc) return false;
		if (!NumberUtils.IsNumeric(cc.Value)) return false;

		// If no specific allowed values, any numeric constant is fine
		if (Values == null) return true;

		// Otherwise, ensure the constant's value is among the specified set
		return Values.Any(v => v == Convert.ToDouble(cc.Value));
	}
}

/// <summary>
/// A specialized attribute that indicates the matched expression's return type
/// must be assignable to a specified type. Useful for restricting the
/// type of an operand beyond its <see cref="ExpressionType"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ReturnTypeAttribute : ExpressionSignatureAttribute
{
	/// <summary>
	/// Gets the required return type (or interface) for the matched expression.
	/// </summary>
	public Type ReturnType { get; }

	/// <summary>
	/// Creates an attribute requiring the expression type to be assignable to <paramref name="returnType"/>.
	/// </summary>
	/// <param name="returnType">The required return type or base class.</param>
	public ReturnTypeAttribute(Type returnType)
		: base((ExpressionType)(-1))
	{
		ReturnType = returnType;
	}

	/// <inheritdoc />
	public override bool Match(Expression e)
	{
		return ReturnType.IsAssignableFrom(e.Type);
	}
}
