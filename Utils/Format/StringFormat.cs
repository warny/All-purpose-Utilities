using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Expressions;
using Utils.Expressions.Resolvers;
using Utils.Objects;

namespace Utils.Format
{
	/// <summary>
	/// Provides utilities for creating and managing string formatting operations,
	/// including the creation of dynamic string formatters using expression trees.
	/// </summary>
	public static partial class StringFormat
	{
		/// <summary>
		/// The default namespaces used for expression parsing and resolution.
		/// </summary>
		private static readonly string[] _defaultNamespaces = { "System", "System.Linq", "System.Text", "Utils.Objects" };

		/// <summary>
		/// A reference to the <see cref="CultureInfo.CurrentCulture"/> property.
		/// </summary>
		private static readonly PropertyInfo _currentCultureProperty =
			typeof(CultureInfo).GetProperty(nameof(CultureInfo.CurrentCulture));

		/// <summary>
		/// A reference to the constructor for the <see cref="NullFormatter"/> class, which takes a <see cref="CultureInfo"/>.
		/// </summary>
		private static readonly ConstructorInfo _nullFormatterConstructor =
			typeof(NullFormatter).GetConstructor([typeof(CultureInfo)]);

		/// <summary>
		/// A default <see cref="IResolver"/> used to discover and select methods and constructors during expression building.
		/// </summary>
		private static readonly IResolver _defaultResolver =
			new DefaultResolver(new TypeFinder(new ParserOptions(), _defaultNamespaces, null), null);

		/// <summary>
		/// Generates a sequence of commands to parse and execute a formatted string dynamically.
		/// Returns an expression that calls <c>ToString()</c> on the generated handler.
		/// </summary>
		private static Expression GenerateCommands(
			string formatString,
			ParameterExpression formatter,
			ParameterExpression cultureInfo,
			ParameterExpression[] parameterExpressions,
			bool defaultFirst,
			string[] namespaces) 
			=> Expression.Call(
                                GenerateCommands(
                                        typeof(DefaultInterpolatedStringHandler),
                                        Array.Empty<ParameterExpression>(),
                                        _defaultResolver,
                                        formatString,
                                        formatter,
                                        cultureInfo,
                                        parameterExpressions,
                                        defaultFirst,
                                        namespaces,
                                        true
                                  ),
				typeof(DefaultInterpolatedStringHandler).GetMethod("ToString")
			);

		/// <summary>
		/// Generates a sequence of commands to parse and execute a formatted string dynamically,
		/// returning an expression (usually a <see cref="BlockExpression"/>) that performs the necessary operations.
		/// </summary>
                private static Expression GenerateCommands(
                        Type handlerType,
                        ParameterExpression[] handlerParameters,
                        IResolver resolver,
                        string formatString,
                        ParameterExpression formatter,
                        ParameterExpression cultureInfo,
                        ParameterExpression[] parameterExpressions,
                        bool defaultFirst,
                        string[] namespaces,
                        bool returnHandler)
                {
			// 1) Create a BlockExpressionBuilder to hold and build the block of expressions
			var builder = new BlockExpressionBuilder();

			// 2) ExpressionOrDefault(...) returns "cultureInfo" or a default value.
			var cultureInfoExpression = ExpressionOrDefault(
				cultureInfo, "@@cultureInfo", typeof(CultureInfo),
				Expression.Property(null, _currentCultureProperty),
				builder
			);
			var formatterExpression = ExpressionOrDefault(
				formatter, "@@formatter", typeof(ICustomFormatter),
				Expression.New(_nullFormatterConstructor, cultureInfoExpression),
				builder
			);

			// 4) Parse the interpolation string to separate literal and formatted parts
			var parser = new InterpolatedStringParser(formatString);

			// 5) Obtain the AppendLiteral(string) method of the handler
			var handlerAppendLiteral = handlerType.GetMethod("AppendLiteral", [typeof(string)]);

			// 6) Declare (or retrieve) the variable 'handler'
			var handlerVariable = builder.GetOrCreateVariable(handlerType, "handler");

			// 7) Calculations to initiate the handler (length of literal parts, number of formatted parts)
			var literalLength = parser.OfType<LiteralPart>().Sum(p => p.Length);
			var formattedCount = parser.OfType<FormattedPart>().Count();

			// 8) Add the assignment: handler = new DefaultInterpolatedStringHandler(literalLength, formattedCount, ...)
			builder.Add(
				Expression.Assign(
					handlerVariable,
					CreateNewHandlerExpression(
						handlerType,
						literalLength,
						formattedCount,
						formatterExpression,
						handlerParameters,
						resolver
					)
				)
			);

			// 9) For each parsed part, add the corresponding expression
			foreach (var part in parser)
			{
				switch (part)
				{
					case LiteralPart literal:
						// handler.AppendLiteral("some literal text");
						builder.Add(
							Expression.Call(handlerVariable, handlerAppendLiteral, Expression.Constant(literal.Text))
						);
						break;

					case FormattedPart formattedPart:
						// Convert expression text into an "object" for AppendFormatted(...)
						var expression = Expression.Convert(
							ExpressionParser.ParseExpression(
								formattedPart.ExpressionText,
								parameterExpressions,
								null,
								defaultFirst,
								namespaces
							),
							typeof(object)
						);

						// handler.AppendFormatted(..., alignment, format)
						builder.Add(
							CreateFormatCallExpression(
								handlerVariable,
								resolver,
								expression,
								formattedPart.Alignment,
								formattedPart.Format
							)
						);
						break;
				}
			}

                        // 10) At the end, we return either the handler itself or its ToString()
                        builder.Add(
                                returnHandler
                                        ? (Expression)handlerVariable
                                        : Expression.Call(handlerVariable, handlerType.GetMethod("ToString", []))
                        );

			// 11) The builder creates an optimal block that uses only the variables actually needed
			return builder.CreateBlock();
		}

		/// <summary>
		/// Chooses the right constructor for the specified handler type.
		/// </summary>
		private static NewExpression CreateNewHandlerExpression(
			Type handlerType,
			int literalLength,
			int formattedCount,
			ParameterExpression formatter,
			ParameterExpression[] handlerParameters,
			IResolver resolver)
		{
			var constructors = handlerType.GetConstructors();
			var literalLengthConstant = Expression.Constant(literalLength);
			var formattedCountConstant = Expression.Constant(formattedCount);

			var parametersCases = new List<Expression[]>();

			if (formatter != null)
			{
				parametersCases.Add(
					[literalLengthConstant, formattedCountConstant, formatter]
				);
			}

			parametersCases.Add([literalLengthConstant, formattedCountConstant]);
			parametersCases.Add([]);

			if (!handlerParameters.IsNullOrEmptyCollection())
			{
				// Combine additional handler parameters
				parametersCases = parametersCases
					.Select(pc => pc.Concat(handlerParameters).ToArray())
					.Concat(parametersCases)
					.ToList();
			}

			foreach (var parameterCase in parametersCases)
			{
				var constructor = resolver.SelectConstructor(constructors, parameterCase);
				if (constructor is not null)
					return Expression.New(constructor.Value.Method, constructor.Value.Parameters);
			}

			throw new Exception("No suitable constructor was found");
		}

		/// <summary>
		/// Creates the call expression for <c>handler.AppendFormatted(...)</c>.
		/// </summary>
		private static Expression CreateFormatCallExpression(
			ParameterExpression handlerExpression,
			IResolver resolver,
			Expression expression,
			int? alignment,
			string format)
		{
			var parametersCases = new List<Expression[]>();

			if (alignment is not null && format is not null)
				parametersCases.Add([expression, Expression.Constant(alignment), Expression.Constant(format)]);
			if (format is not null)
				parametersCases.Add([expression, Expression.Constant(format)]);
			if (alignment is not null)
				parametersCases.Add([expression, Expression.Constant(alignment)]);
			parametersCases.Add([expression]);

			var methods = handlerExpression.Type
				.GetMethods()
				.Where(m => m.Name == "AppendFormatted")
				.ToArray();

			foreach (var parametersCase in parametersCases)
			{
				var method = resolver.SelectMethod(methods, handlerExpression, null, parametersCase);
				if (method is not null)
					return Expression.Call(handlerExpression, method.Value.Method, method.Value.Parameters);
			}

			throw new Exception("No suitable function was found for AppendFormatted");
		}

		/// <summary>
		/// Creates a string formatter delegate of the specified type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of the formatter delegate.</typeparam>
		/// <param name="formatString">The interpolated format string.</param>
		/// <param name="names">The parameter names used in the format string.</param>
		/// <returns>A delegate that formats strings based on the provided parameters.</returns>
                public static T Create<T>(string formatString, params string[] names)
                        where T : Delegate
                {
                        return Create<T>(formatString, null, null, names);
                }

                /// <summary>
                /// Creates a string formatter delegate using a custom interpolated string handler.
                /// </summary>
                public static T Create<T, THandler>(string formatString, params string[] names)
                        where T : Delegate
                {
                        return Create<T, THandler>(formatString, null, null, names);
                }

		/// <summary>
		/// Creates a string formatter delegate of the specified type <typeparamref name="T"/>, with a custom formatter and culture info.
		/// </summary>
		/// <typeparam name="T">The type of the formatter delegate.</typeparam>
		/// <param name="formatString">The interpolated format string.</param>
		/// <param name="customFormatter">The custom formatter to use (optional).</param>
		/// <param name="cultureInfo">The culture info for formatting (optional).</param>
		/// <param name="names">The parameter names used in the format string.</param>
		/// <returns>A delegate that formats strings based on the provided parameters.</returns>
                public static T Create<T>(
                        string formatString,
                        ICustomFormatter customFormatter,
                        CultureInfo cultureInfo,
                        params string[] names)
                        where T : Delegate
                {
			var delegateParameters = typeof(T).GetMethod("Invoke")?.GetParameters() ?? [];
			if (names.Length != 0 && names.Length != delegateParameters.Length)
				throw new ArgumentException("Invalid number of names", nameof(names));

			var parameters = delegateParameters
				.Select((p, i) => Expression.Parameter(p.ParameterType, names.Length > 0 ? names[i] : p.Name))
				.ToArray();

                        return Create<T>(formatString, customFormatter, cultureInfo, parameters);
                }

                /// <summary>
                /// Creates a string formatter delegate using a custom interpolated string handler.
                /// </summary>
                public static T Create<T, THandler>(
                        string formatString,
                        ICustomFormatter customFormatter,
                        CultureInfo cultureInfo,
                        params string[] names)
                        where T : Delegate
                {
                        var delegateParameters = typeof(T).GetMethod("Invoke")?.GetParameters() ?? [];
                        if (names.Length != 0 && names.Length != delegateParameters.Length)
                                throw new ArgumentException("Invalid number of names", nameof(names));

                        var parameters = delegateParameters
                                .Select((p, i) => Expression.Parameter(p.ParameterType, names.Length > 0 ? names[i] : p.Name))
                                .ToArray();

                        return Create<T, THandler>(formatString, customFormatter, cultureInfo, parameters);
                }

		/// <summary>
		/// Creates a string formatter delegate of the specified type <typeparamref name="T"/>,
		/// allowing the caller to specify parameter expressions in detail.
		/// </summary>
		/// <typeparam name="T">The type of the formatter delegate.</typeparam>
		/// <param name="formatString">The interpolated format string.</param>
		/// <param name="customFormatter">The custom formatter to use (optional).</param>
		/// <param name="cultureInfo">The culture info for formatting (optional).</param>
		/// <param name="parameterExpressions">The parameter expressions used in the delegate.</param>
		/// <returns>A delegate that formats strings based on the provided parameters.</returns>
                public static T Create<T>(
                        string formatString,
                        ICustomFormatter customFormatter,
                        CultureInfo cultureInfo,
                        params ParameterExpression[] parameterExpressions)
                        where T : Delegate
                {
			var expressions = new List<Expression>();
			var formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
			var culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

			var body = GenerateCommands(formatString, formatter, culture, parameterExpressions, false, _defaultNamespaces);
			expressions.Add(body);

			var block = Expression.Block(
				new[] { formatter, culture }.Where(e => e != null),
				expressions
			);
                        return Expression.Lambda<T>(block, parameterExpressions).Compile();
                }

                /// <summary>
                /// Creates a string formatter delegate with a custom interpolated string handler and explicit parameters.
                /// </summary>
                public static T Create<T, THandler>(
                        string formatString,
                        ICustomFormatter customFormatter,
                        CultureInfo cultureInfo,
                        params ParameterExpression[] parameterExpressions)
                        where T : Delegate
                {
                        var expressions = new List<Expression>();
                        var formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
                        var culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

                        ParameterExpression handlerSb = null;
                        var handlerType = typeof(THandler);
                        if (handlerType.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(StringBuilder))))
                        {
                                handlerSb = Expression.Variable(typeof(StringBuilder), "builder");
                                expressions.Add(Expression.Assign(handlerSb, Expression.New(typeof(StringBuilder))));
                        }

                        var delegateReturn = typeof(T).GetMethod("Invoke")?.ReturnType ?? typeof(void);
                        bool returnHandler = delegateReturn.IsAssignableFrom(handlerType);

                        var body = GenerateCommands(
                                handlerType,
                                handlerSb != null ? [handlerSb] : Array.Empty<ParameterExpression>(),
                                _defaultResolver,
                                formatString,
                                formatter,
                                culture,
                                parameterExpressions,
                                false,
                                _defaultNamespaces,
                                returnHandler);
                        expressions.Add(body);

                        var block = Expression.Block(
                                new[] { formatter, culture, handlerSb }.Where(e => e != null),
                                expressions
                        );
                        return Expression.Lambda<T>(block, parameterExpressions).Compile();
                }

		/// <summary>
		/// Creates a string formatter function for an <see cref="IDataRecord"/>,
		/// allowing dynamic retrieval and formatting of field values.
		/// </summary>
		/// <param name="formatString">The interpolated format string.</param>
		/// <param name="customFormatter">The custom formatter to use (optional).</param>
		/// <param name="cultureInfo">The culture info for formatting (optional).</param>
		/// <param name="dataRecord">The <see cref="IDataRecord"/> containing data fields.</param>
		/// <returns>A function that formats strings based on the fields of the <paramref name="dataRecord"/>.</returns>
		public static Func<IDataRecord, string> Create(
			string formatString,
			ICustomFormatter customFormatter,
			CultureInfo cultureInfo,
			IDataRecord dataRecord)
		{
			var getItemMethod = typeof(IDataRecord).GetMethod("get_Item", [typeof(int)]) 
				?? throw new InvalidOperationException("IDataRecord does not have 'get_Item' method.");
			
			var expressions = new List<Expression>();
			var formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
			var culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

			var dataRecordParameter = Expression.Parameter(typeof(IDataRecord), "dataRecord");
			var fieldVariables = new List<ParameterExpression>();

			var fields = new List<(int Index, string Name, Type Type)>();
			for (var i = 0; i < dataRecord.FieldCount; i++)
			{
				fields.Add((i, dataRecord.GetName(i), dataRecord.GetFieldType(i)));
			}

			// Group by field name and create variables for each distinct name
			foreach (var fieldGroup in fields.GroupBy(f => f.Name))
			{
				foreach (var field in fieldGroup.Select((f, pos) => (pos, f.Index, f.Name, f.Type)))
				{
					var variable = Expression.Variable(field.Type, field.pos == 0 ? field.Name : $"{field.Name}_{field.pos}");
					fieldVariables.Add(variable);

					expressions.Add(
						Expression.Assign(
							variable,
							Expression.Convert(
								Expression.Call(dataRecordParameter, getItemMethod, Expression.Constant(field.Index)),
								field.Type
							)
						)
					);
				}
			}

			var body = GenerateCommands(
				formatString,
				formatter,
				culture,
				[.. fieldVariables],
				false,
				_defaultNamespaces
			);
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
		/// <returns>A parameter expression representing the variable, or <c>null</c> if <paramref name="value"/> is null.</returns>
		private static ParameterExpression CreateAndAssignVariable<T>(
			T value,
			string name,
			Action<Expression> addExpression)
		{
			if (value == null) return null;
			var variable = Expression.Variable(typeof(T), name);
			addExpression(Expression.Assign(variable, Expression.Constant(value)));
			return variable;
		}

		/// <summary>
		/// Creates a parameter expression with a default value if the given expression is <c>null</c>.
		/// Otherwise, returns the provided expression unchanged.
		/// </summary>
		private static ParameterExpression ExpressionOrDefault(
			ParameterExpression expression,
			string name,
			Type type,
			Expression defaultValue,
			BlockExpressionBuilder builder)
		{
			if (expression != null)
				return expression;

			var variable = builder.GetOrCreateVariable(type, name);
			builder.Add(Expression.Assign(variable, defaultValue));
			return variable;
		}
	}
}
