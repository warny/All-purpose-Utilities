using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Utils.Collections;
using Utils.Expressions;
using Utils.Expressions.Resolvers;

namespace Utils.Objects;

/// <summary>
/// Provides utilities for creating and managing string formatting operations, including the creation of dynamic string formatters.
/// </summary>
public static partial class StringFormat
{
	/// <summary>
	/// Default namespaces used for expression parsing.
	/// </summary>
	private static readonly string[] DefaultNamespaces = { "System", "System.Linq", "System.Text", "Utils.Objects" };

	private static readonly MethodInfo AlignMethod = typeof(StringExtensions).GetMethod(nameof(StringExtensions.Align), [typeof(string), typeof(int)]);
	private static readonly MethodInfo CustomFormatMethod = typeof(ICustomFormatter).GetMethod("Format", [typeof(string), typeof(object), typeof(IFormatProvider)]);
	private static readonly PropertyInfo CurrentCultureProperty = typeof(CultureInfo).GetProperty(nameof(CultureInfo.CurrentCulture));
	private static readonly ConstructorInfo NullFormatterConstructor = typeof(NullFormatter).GetConstructor([typeof(CultureInfo)]);

	private static readonly IResolver _defaultResolver = new DefaultResolver(new TypeFinder(new ParserOptions(), DefaultNamespaces, null), null);

	/// <summary>
	/// Generates a sequence of commands to parse and execute a formatted string dynamically.
	/// </summary>
	/// <param name="formatString">The format string to parse.</param>
	/// <param name="formatter">The custom formatter to use.</param>
	/// <param name="cultureInfo">The culture information for formatting.</param>
	/// <param name="parameterExpressions">The parameter expressions used in the formatting.</param>
	/// <param name="defaultFirst">Specifies if default parameters should be prioritized.</param>
	/// <param name="namespaces">The namespaces for resolving types in expressions.</param>
	/// <returns>An expression that represents the formatted string operation.</returns>
	private static Expression GenerateCommands(
		string formatString,
		ParameterExpression formatter,
		ParameterExpression cultureInfo,
		ParameterExpression[] parameterExpressions,
		bool defaultFirst,
		string[] namespaces) 
	=> 	Expression.Call(
			GenerateCommands(typeof(DefaultInterpolatedStringHandler), [], DefaultResolver.Default, formatString, formatter, cultureInfo, parameterExpressions, defaultFirst, namespaces),
			typeof(DefaultInterpolatedStringHandler).GetMethod("ToString")
		);

	/// <summary>
	/// Generates a sequence of commands to parse and execute a formatted string dynamically.
	/// </summary>
	/// <param name="handlerType">The handler type to use</param>
	/// <param name="formatString">The format string to parse.</param>
	/// <param name="formatter">The custom formatter to use.</param>
	/// <param name="cultureInfo">The culture information for formatting.</param>
	/// <param name="parameterExpressions">The parameter expressions used in the formatting.</param>
	/// <param name="defaultFirst">Specifies if default parameters should be prioritized.</param>
	/// <param name="namespaces">The namespaces for resolving types in expressions.</param>
	/// <returns>An expression that represents the formatted string operation.</returns>
	private static Expression GenerateCommands(
		Type handlerType, 
		ParameterExpression[] handlerParameters,
		IResolver resolver,
		string formatString, 
		ParameterExpression formatter, 
		ParameterExpression cultureInfo, 
		ParameterExpression[] parameterExpressions, 
		bool defaultFirst, 
		string[] namespaces
	)
	{
		var result = new List<Expression>();
		var variables = new List<ParameterExpression>();
		var cultureInfoExpression = ExpressionOrDefault(
			cultureInfo, "@@cultureInfo", typeof(CultureInfo),
			Expression.Property(null, CurrentCultureProperty), variables, result);
		var formatterExpression = ExpressionOrDefault(
			formatter, "@@formatter", typeof(ICustomFormatter),
			Expression.New(NullFormatterConstructor, cultureInfoExpression), variables, result);

		var parser = new InterpolatedStringParser(formatString);

		// Using DefaultInterpolatedStringHandler to build the interpolated string.
		var handlerAppendLiteral = handlerType.GetMethod("AppendLiteral", [typeof(string)]);

		var handlerVariable = Expression.Variable(handlerType, "handler");
		variables.Add(handlerVariable);

		var literalLength = parser.OfType<LiteralPart>().Sum(p => p.Length);
		var formattedCount = parser.OfType<FormattedPart>().Count();

		// Initialize the DefaultInterpolatedStringHandler
		result.Add(Expression.Assign(handlerVariable, CreateNewHandlerExpression(handlerType, literalLength, formattedCount, formatter, handlerParameters, resolver)));

		foreach (var part in parser)
		{
			switch (part)
			{
				case LiteralPart literal:
					{
						result.Add(Expression.Call(handlerVariable, handlerAppendLiteral, Expression.Constant(literal.Text)));
						break;
					}
				case FormattedPart formatted:
					{
						var expression = Expression.Convert(
							ExpressionParser.ParseExpression(
								formatted.ExpressionText, parameterExpressions, null, defaultFirst, namespaces),
							typeof(object));

						result.Add(CreateFormatCallExpression(handlerVariable, resolver, expression, formatted.Alignment, formatted.Format));
						break;
					}
			}
		}

		// Finalize and return the constructed string
		result.Add(handlerVariable);

		return Expression.Block(variables, result);
	}

	/// <summary>
	/// Choose the right constructor for the litteral handler
	/// </summary>
	/// <param name="handlerType">The handler type to use</param>
	/// <param name="handlerVariable"></param>
	/// <param name="literalLength"></param>
	/// <param name="formattedCount"></param>
	/// <returns></returns>
	private static NewExpression CreateNewHandlerExpression(
		Type handlerType,
		int literalLength,
		int formattedCount,
		ParameterExpression formatter,
		ParameterExpression[] handlerParameters,
		IResolver resolver
	)
	{
		var constructors = handlerType.GetConstructors();
		var literalLengthConstant = Expression.Constant(literalLength);
		var formattedCountConstant = Expression.Constant(formattedCount);

		List<Expression[]> parametersCases = [];



		if (formatter != null) {
			parametersCases.Add([literalLengthConstant, formattedCountConstant, formatter]);
		}

		parametersCases.Add([literalLengthConstant, formattedCountConstant]);
		parametersCases.Add([]);

		if (!handlerParameters.IsNullOrEmptyCollection())
		{
			parametersCases = [
				.. parametersCases.Select<Expression[], Expression[]>(pc => [..pc, .. handlerParameters] ),
				.. parametersCases
			];
		}

		foreach (var parameterCase in parametersCases)
		{
			Type[] parametersTypes = [..parameterCase.Select(e => e.Type)];
			var constructor = resolver.SelectConstructor(constructors, parameterCase);
			if (constructor is not null)
			{
				return Expression.New(constructor.Value.Method, constructor.Value.Parameters);
			}
		}

		throw new Exception("No suitable constructor was found");
	}

	private static Expression CreateFormatCallExpression(
		ParameterExpression handlerExpression,
		IResolver resolver,
		Expression expression,
		int? alignment,
		string format
	) {
		List<Expression[]> parametersCases = [];
		if (alignment is not null && format is not null)
		{
			parametersCases.Add([expression, Expression.Constant(alignment), Expression.Constant(format)]);
		}
		if (format is not null)
		{
			parametersCases.Add([expression, Expression.Constant(format)]);
		}
		if (alignment is not null)
		{
			parametersCases.Add([expression, Expression.Constant(alignment)]);
		}
		parametersCases.Add([expression]);

		var methods = handlerExpression.Type.GetMethods().Where(m => m.Name == "AppendFormatted").ToArray();

		foreach (var parametersCase in parametersCases)
		{
			var method = resolver.SelectMethod(methods, handlerExpression, null, parametersCase);
			if (method is not null)
			{
				return Expression.Call(handlerExpression, method.Value.Method, method.Value.Parameters);
			}
		}

		throw new Exception("No suitable function was found for AppendFormatted");

	}

	/// <summary>
	/// Creates a string formatter delegate of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of the formatter delegate.</typeparam>
	/// <param name="formatString">The interpolated format string.</param>
	/// <param name="names">The parameter names used in the format string.</param>
	/// <returns>A delegate that formats strings based on the provided parameters.</returns>
	public static T Create<T>(string formatString, params string[] names) where T : Delegate
		=> Create<T>(formatString, null, null, names);

	/// <summary>
	/// Creates a string formatter delegate of the specified type with custom formatter and culture.
	/// </summary>
	/// <typeparam name="T">The type of the formatter delegate.</typeparam>
	/// <param name="formatString">The interpolated format string.</param>
	/// <param name="customFormatter">The custom formatter to use.</param>
	/// <param name="cultureInfo">The culture information for formatting.</param>
	/// <param name="names">The parameter names used in the format string.</param>
	/// <returns>A delegate that formats strings based on the provided parameters.</returns>
	public static T Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, params string[] names) where T : Delegate
	{
		var delegateParameters = typeof(T).GetMethod("Invoke")?.GetParameters() ?? Array.Empty<ParameterInfo>();
		if (names.Length != 0 && names.Length != delegateParameters.Length)
		{
			throw new ArgumentException("Invalid number of names", nameof(names));
		}

		var parameters = delegateParameters
			.Select((p, i) => Expression.Parameter(p.ParameterType, names.Length > 0 ? names[i] : p.Name))
			.ToArray();

		return Create<T>(formatString, customFormatter, cultureInfo, parameters);
	}

	/// <summary>
	/// Creates a string formatter delegate of the specified type with detailed control over parameters.
	/// </summary>
	/// <typeparam name="T">The type of the formatter delegate.</typeparam>
	/// <param name="formatString">The interpolated format string.</param>
	/// <param name="customFormatter">The custom formatter to use.</param>
	/// <param name="cultureInfo">The culture information for formatting.</param>
	/// <param name="parameterExpressions">The parameter expressions used in the delegate.</param>
	/// <returns>A delegate that formats strings based on the provided parameters.</returns>
	public static T Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, params ParameterExpression[] parameterExpressions) where T : Delegate
	{
		var expressions = new List<Expression>();
		var formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
		var culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

		var body = GenerateCommands(formatString, formatter, culture, parameterExpressions, false, DefaultNamespaces);
		expressions.Add(body);

		var block = Expression.Block(new[] { formatter, culture }.Where(e => e != null), expressions);
		return Expression.Lambda<T>(block, parameterExpressions).Compile();
	}

	/// <summary>
	/// Creates a string formatter function for an IDataRecord.
	/// </summary>
	/// <param name="formatString">The interpolated format string.</param>
	/// <param name="customFormatter">The custom formatter to use.</param>
	/// <param name="cultureInfo">The culture information for formatting.</param>
	/// <param name="dataRecord">The IDataRecord containing data fields.</param>
	/// <returns>A function that formats strings based on the IDataRecord.</returns>
	public static Func<IDataRecord, string> Create(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, IDataRecord dataRecord)
	{
		var getItemMethod = typeof(IDataRecord).GetMethod("get_Item", [typeof(int)]);
		if (getItemMethod == null) throw new InvalidOperationException("IDataRecord does not have 'get_Item' method.");

		var expressions = new List<Expression>();
		var formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
		var culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

		var dataRecordParameter = Expression.Parameter(typeof(IDataRecord), "dataRecord");
		var fieldVariables = new List<ParameterExpression>();

		var fields = new List<(int Index, string Name, Type Type)>();

		for (int i = 0; i < dataRecord.FieldCount; i++)
		{
			fields.Add((i, dataRecord.GetName(i), dataRecord.GetFieldType(i)));
		}
		foreach (var fieldGroup in fields.GroupBy(f => f.Name))
		{
			foreach (var field in fieldGroup.Select((Field, Position) => (Position, Field.Index, Field.Name, Field.Type)))
			{
				var variable = Expression.Variable(field.Type, field.Position == 0 ? field.Name : $"{field.Name}_{field.Position}");
				fieldVariables.Add(variable);
				expressions.Add(Expression.Assign(variable, Expression.Convert(Expression.Call(dataRecordParameter, getItemMethod, Expression.Constant(field.Index)), field.Type)));
			}
		}

		var body = GenerateCommands(formatString, formatter, culture, [.. fieldVariables], false, DefaultNamespaces);
		expressions.Add(body);

		var block = Expression.Block(fieldVariables.Append(formatter).Append(culture), expressions);
		return Expression.Lambda<Func<IDataRecord, string>>(block, dataRecordParameter).Compile();
	}

	/// <summary>
	/// Creates and assigns a variable expression for use in a block.
	/// </summary>
	/// <typeparam name="T">The type of the variable.</typeparam>
	/// <param name="value">The initial value of the variable.</param>
	/// <param name="name">The name of the variable.</param>
	/// <param name="addExpression">An action to add the assignment expression to a collection.</param>
	/// <returns>A parameter expression representing the variable.</returns>
	private static ParameterExpression CreateAndAssignVariable<T>(T value, string name, Action<Expression> addExpression)
	{
		if (value == null) return null;
		var variable = Expression.Variable(typeof(T), name);
		addExpression(Expression.Assign(variable, Expression.Constant(value)));
		return variable;
	}

	/// <summary>
	/// Creates a parameter expression with a default value if not provided.
	/// </summary>
	/// <param name="expression">The existing expression or null.</param>
	/// <param name="name">The name of the variable.</param>
	/// <param name="type">The type of the variable.</param>
	/// <param name="defaultValue">The default value expression.</param>
	/// <param name="variables">The list of variables to which this will be added.</param>
	/// <param name="expressions">The list of expressions to which the assignment will be added.</param>
	/// <returns>A parameter expression representing the variable.</returns>
	private static ParameterExpression ExpressionOrDefault(
		ParameterExpression expression, string name, Type type, Expression defaultValue,
		List<ParameterExpression> variables, List<Expression> expressions)
	{
		if (expression != null) return expression;

		var variable = Expression.Variable(type, name);
		variables.Add(variable);
		expressions.Add(Expression.Assign(variable, defaultValue));
		return variable;
	}
}
