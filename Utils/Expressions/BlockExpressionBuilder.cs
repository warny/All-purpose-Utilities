using System.Linq.Expressions;
using Utils.Objects;

namespace Utils.Expressions;

/// <summary>
/// A helper class that builds expression blocks while minimizing the number of declared variables.
/// </summary>
public class BlockExpressionBuilder
{
	private readonly List<ParameterExpression> _variables;
	private readonly List<Expression> _expressions;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockExpressionBuilder"/> class
        /// with no pre-declared variables or expressions.
        /// </summary>
        public BlockExpressionBuilder()
        {
                _variables = [];
                _expressions = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockExpressionBuilder"/> class
        /// using pre-existing variables and expressions.
        /// </summary>
        /// <param name="variables">Variables that should be added to the block builder.</param>
        /// <param name="expressions">Expressions that should initially populate the block.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> or <paramref name="expressions"/> is <see langword="null"/>.</exception>
        public BlockExpressionBuilder(IEnumerable<ParameterExpression> variables, IEnumerable<Expression> expressions)
        {
                this._variables = [..variables.Arg().MustNotBeNull().Value];
                this._expressions = [..expressions.Arg().MustNotBeNull().Value];
        }



	/// <summary>
	/// Declares a new variable of a given type and name, then adds it to the block.
	/// </summary>
	/// <param name="type">The variable's type.</param>
	/// <param name="name">The variable's name.</param>
	/// <returns>The newly created <see cref="ParameterExpression"/>.</returns>
	public ParameterExpression AddVariable(Type type, string name)
		=> AddVariable(Expression.Variable(type, name));

	/// <summary>
	/// Adds an existing <see cref="ParameterExpression"/> to the block.
	/// </summary>
	/// <param name="variable">The parameter expression to add.</param>
	/// <returns>The same <see cref="ParameterExpression"/> that was added.</returns>
	public ParameterExpression AddVariable(ParameterExpression variable)
	{
		_variables.Add(variable);
		return variable;
	}

	/// <summary>
	/// Returns a previously declared variable by its name.
	/// </summary>
	/// <param name="name">The name of the variable.</param>
	/// <returns>The matching <see cref="ParameterExpression"/>, if found.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if no variable with the specified name is found.
	/// </exception>
	public ParameterExpression GetVariable(string name)
		=> _variables.First(v => v.Name == name);

	/// <summary>
	/// Retrieves a variable if it already exists by name.
	/// If it does not exist, a new one is declared with the specified type and name.
	/// </summary>
	/// <param name="type">The type of the variable.</param>
	/// <param name="name">The name of the variable.</param>
	/// <returns>A <see cref="ParameterExpression"/> representing the variable.</returns>
	public ParameterExpression GetOrCreateVariable(Type type, string name)
		=> _variables.FirstOrDefault(v => v.Name == name)
			?? AddVariable(type, name);

	/// <summary>
	/// Adds an expression to the block builder.
	/// </summary>
	/// <param name="expression">The expression to add.</param>
	/// <returns>The same expression passed in.</returns>
	public Expression Add(Expression expression)
	{
		_expressions.Add(expression);
		return expression;
	}

	/// <summary>
	/// Creates an optimized block expression. If there are multiple expressions or any
	/// referenced variables, a block is created with only the necessary variables.
	/// Otherwise, a single expression is returned (or <see cref="Expression.Empty"/> if none).
	/// </summary>
	/// <returns>An <see cref="Expression"/> that is either a block or a single expression.</returns>
	public Expression CreateBlock()
	{
		// Handle the case where no expressions exist
		if (_expressions.Count == 0)
		{
			// Return an empty, no-op expression
			return Expression.Empty();
		}

		// Identify only the variables that are actually used in these expressions
		var usedVariables = GetAssignedVariableInBlock(_expressions);
		var variablesToDeclare = usedVariables.Intersect(_variables).ToArray();

		// If there's more than one expression or any used variables, build a block
		if (variablesToDeclare.Any() || _expressions.Count > 1)
		{
			return Expression.Block(variablesToDeclare, _expressions);
		}
		else
		{
			// Only one expression, and no variables needed
			return _expressions.First();
		}
	}

	/// <summary>
	/// Scans an enumerable of expressions to find the <see cref="ParameterExpression"/> objects used.
	/// </summary>
	/// <param name="expressions">The expressions to scan.</param>
	/// <returns>An array of <see cref="ParameterExpression"/> objects.</returns>
	private static ParameterExpression[] GetAssignedVariableInBlock(IEnumerable<Expression> expressions)
	{
		List<ParameterExpression> usedVariables = new List<ParameterExpression>();

		foreach (var expression in expressions)
		{
			usedVariables.AddRange(GetVariablesInExpression(expression).Distinct());
		}

		return usedVariables.Distinct().ToArray();
	}

	/// <summary>
	/// Returns the set of variables used in the provided set of expressions.
	/// </summary>
	/// <param name="expressions">A list of expressions.</param>
	/// <returns>An enumerable of used <see cref="ParameterExpression"/> objects.</returns>
	private static IEnumerable<ParameterExpression> GetVariablesInExpressions(IEnumerable<Expression> expressions)
	{
		return expressions.SelectMany(GetVariablesInExpression).Distinct();
	}

	/// <summary>
	/// For a list of objects and a set of lambda selectors,
	/// finds variables used in each selected sub-expression.
	/// </summary>
	/// <typeparam name="T">Type of the source object.</typeparam>
	/// <param name="expression">Enumerable of source objects.</param>
	/// <param name="expressionGetters">One or more functions that extract
	/// an <see cref="Expression"/> (or set of <see cref="Expression"/>) from each object.</param>
	/// <returns>An enumerable of used <see cref="ParameterExpression"/> objects.</returns>
	private static IEnumerable<ParameterExpression> GetVariablesInExpression<T>(
		IEnumerable<T> expression,
		params Func<T, IEnumerable<Expression>>[] expressionGetters)
	{
		return [.. expression.SelectMany(e => expressionGetters
			.SelectMany(g => GetVariablesInExpression(g(e))))
			.Distinct()];
	}

	/// <summary>
	/// Recursively extracts variable references used by a single <see cref="Expression"/>.
	/// Uses pattern matching for more concise cases. The custom syntax [.. ] 
	/// is retained for your domain-specific needs.
	/// </summary>
	/// <param name="expression">The expression to analyze.</param>
	/// <returns>An enumerable of <see cref="ParameterExpression"/> objects.</returns>
	private static IEnumerable<ParameterExpression> GetVariablesInExpression(Expression expression)
	{
		if (expression is null) return [];

		switch (expression)
		{
			case BlockExpression be:
				return GetAssignedVariableInBlock(be.Expressions).Except(be.Variables);

			case ParameterExpression pe:
				return [pe];

			case ConditionalExpression ce:
				return [.. GetVariablesInExpression(ce.Test),
						.. GetVariablesInExpression(ce.IfTrue),
						.. GetVariablesInExpression(ce.IfFalse)];

			case LoopExpression le:
				return GetVariablesInExpression(le.Body);

			case BinaryExpression bex:
				return [.. GetVariablesInExpression(bex.Left),
						.. GetVariablesInExpression(bex.Right)];

			case UnaryExpression ue:
				return GetVariablesInExpression(ue.Operand);

			case MethodCallExpression mce:
				return [.. GetVariablesInExpression(mce.Object),
						.. GetVariablesInExpressions(mce.Arguments)];

			case NewExpression ne:
				return ne.Arguments.SelectMany(GetVariablesInExpression);

			case IndexExpression ie:
				return [.. GetVariablesInExpression(ie.Object),
						.. GetVariablesInExpressions(ie.Arguments)];

			case TryExpression te:
				return [.. GetVariablesInExpression(te.Body),
						.. GetVariablesInExpression(
							te.Handlers,
							h => [h.Filter],
							h => [h.Body]
						),
						.. GetVariablesInExpression(te.Finally)];

			case SwitchExpression se:
				return [.. GetVariablesInExpression(se.SwitchValue),
						.. GetVariablesInExpression(
							se.Cases,
							c => c.TestValues,
							c => [c.Body]
						),
						.. GetVariablesInExpression(se.DefaultBody)];

			case DynamicExpression de:
				return GetVariablesInExpressions(de.Arguments);

			case MemberExpression me:
				// e.g., obj.Property
				return GetVariablesInExpression(me.Expression);

			case MemberInitExpression mie:
				return [.. GetVariablesInExpression(mie.NewExpression),
						.. GetVariablesInMemberBindings(mie.Bindings)];

			case ListInitExpression lie:
				return [.. GetVariablesInExpression(lie.NewExpression),
						.. GetVariablesInElementInit(lie.Initializers)];

			case InvocationExpression ive:
				return [.. GetVariablesInExpression(ive.Expression),
						.. GetVariablesInExpressions(ive.Arguments)];

			case LambdaExpression lam:
				// For lambdas, parameters are local, but the body may reference outer variables
				return GetVariablesInExpression(lam.Body);

			case GotoExpression go:
				return GetVariablesInExpression(go.Value);

			case LabelExpression lae:
				return GetVariablesInExpression(lae.DefaultValue);

			case NewArrayExpression nae:
				return nae.Expressions.SelectMany(GetVariablesInExpression);

			case TypeBinaryExpression tbe:
				return GetVariablesInExpression(tbe.Expression);

			case DefaultExpression:
				return [];

			default:
				return [];
		}
	}

	/// <summary>
	/// Recursively extracts variables from a set of <see cref="MemberBinding"/> objects
	/// (used in <see cref="MemberInitExpression"/>).
	/// </summary>
	/// <param name="bindings">A collection of <see cref="MemberBinding"/> objects.</param>
	/// <returns>An enumerable of <see cref="ParameterExpression"/> objects.</returns>
	private static IEnumerable<ParameterExpression> GetVariablesInMemberBindings(
		IEnumerable<MemberBinding> bindings)
	{
		if (bindings is null) return [];

		List<ParameterExpression> result = new List<ParameterExpression>();

		foreach (var binding in bindings)
		{
			switch (binding.BindingType)
			{
				case MemberBindingType.Assignment:
					if (binding is MemberAssignment assignment)
						result.AddRange(GetVariablesInExpression(assignment.Expression));
					break;
				case MemberBindingType.MemberBinding:
					if (binding is MemberMemberBinding memberBinding)
						result.AddRange(GetVariablesInMemberBindings(memberBinding.Bindings));
					break;
				case MemberBindingType.ListBinding:
					if (binding is MemberListBinding listBinding)
						result.AddRange(GetVariablesInElementInit(listBinding.Initializers));
					break;
			}
		}

		return result;
	}

	/// <summary>
	/// Recursively extracts variables from a set of <see cref="ElementInit"/> objects
	/// (used in <see cref="ListInitExpression"/> or <see cref="MemberListBinding"/>).
	/// </summary>
	/// <param name="initializers">A collection of <see cref="ElementInit"/> objects.</param>
	/// <returns>An enumerable of <see cref="ParameterExpression"/> objects.</returns>
	private static IEnumerable<ParameterExpression> GetVariablesInElementInit(
		IEnumerable<ElementInit> initializers)
	{
		if (initializers is null) return [];

		List<ParameterExpression> result = new List<ParameterExpression>();
		foreach (var init in initializers)
		{
			result.AddRange(GetVariablesInExpressions(init.Arguments));
		}

		return result;
	}
}
