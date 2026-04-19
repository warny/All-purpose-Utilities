using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Expressions;
using Utils.Expressions.Resolvers;
using Utils.Objects;

namespace Utils.Format;

/// <summary>
/// Builds string-format delegates while compiling embedded expressions with an injected compiler.
/// </summary>
public sealed class StringFormatBuilder : IStringFormatBuilder
{
    private static readonly string[] DefaultNamespaces = ["System", "System.Linq", "System.Text", "Utils.Objects"];

    private static readonly PropertyInfo CurrentCultureProperty =
        typeof(CultureInfo).GetProperty(nameof(CultureInfo.CurrentCulture))
        ?? throw new InvalidOperationException("CurrentCulture property was not found.");

    private static readonly ConstructorInfo NullFormatterConstructor =
        typeof(NullFormatter).GetConstructor([typeof(CultureInfo)])
        ?? throw new InvalidOperationException("NullFormatter constructor was not found.");

    private static readonly IResolver DefaultResolver =
        new DefaultResolver(new TypeFinder(new ParserOptions(), DefaultNamespaces, null), null);

    private readonly IExpressionCompiler _compiler;

    /// <summary>
    /// Initializes a new instance of <see cref="StringFormatBuilder"/>.
    /// </summary>
    /// <param name="compiler">Compiler used to compile embedded expressions.</param>
    public StringFormatBuilder(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc />
    public T Create<T>(string formatString, params string[] names) where T : Delegate
        => Create<T>(formatString, null, null, names);

    /// <inheritdoc />
    public T Create<T>(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, params string[] names) where T : Delegate
    {
        ParameterExpression[] parameters = GetDelegateParameters<T>(names);
        return CreateFromParameters<T>(formatString, customFormatter, cultureInfo, parameters);
    }

    /// <inheritdoc />
    public T Create<T, THandler>(string formatString, params string[] names) where T : Delegate
        => Create<T, THandler>(formatString, null, null, names);

    /// <inheritdoc />
    public T Create<T, THandler>(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, params string[] names) where T : Delegate
    {
        ParameterExpression[] parameters = GetDelegateParameters<T>(names);
        return CreateWithHandlerFromParameters<T, THandler>(formatString, customFormatter, cultureInfo, parameters);
    }

    /// <inheritdoc />
    public Func<IDataRecord, string> Create(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, IDataRecord dataRecord)
    {
        if (dataRecord is null)
        {
            throw new ArgumentNullException(nameof(dataRecord));
        }

        MethodInfo getItemMethod = typeof(IDataRecord).GetMethod("get_Item", [typeof(int)])
            ?? throw new InvalidOperationException("IDataRecord does not have 'get_Item' method.");

        var expressions = new List<Expression>();
        ParameterExpression? formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
        ParameterExpression? culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

        ParameterExpression dataRecordParameter = Expression.Parameter(typeof(IDataRecord), "dataRecord");
        var fieldVariables = new List<ParameterExpression>();

        var fields = new List<(int Index, string Name, Type Type)>();
        for (int i = 0; i < dataRecord.FieldCount; i++)
        {
            fields.Add((i, dataRecord.GetName(i), dataRecord.GetFieldType(i)));
        }

        foreach (IGrouping<string, (int Index, string Name, Type Type)> fieldGroup in fields.GroupBy(f => f.Name))
        {
            foreach (var field in fieldGroup.Select((f, pos) => (Position: pos, f.Index, f.Name, f.Type)))
            {
                ParameterExpression variable = Expression.Variable(field.Type, field.Position == 0 ? field.Name : $"{field.Name}_{field.Position}");
                fieldVariables.Add(variable);

                expressions.Add(
                    Expression.Assign(
                        variable,
                        Expression.Convert(
                            Expression.Call(dataRecordParameter, getItemMethod, Expression.Constant(field.Index)),
                            field.Type)));
            }
        }

        Expression body = GenerateCommands(formatString, formatter, culture, [.. fieldVariables]);
        expressions.Add(body);

        var variables = new List<ParameterExpression>(fieldVariables);
        if (formatter is not null)
        {
            variables.Add(formatter);
        }

        if (culture is not null)
        {
            variables.Add(culture);
        }

        BlockExpression block = Expression.Block(variables, expressions);
        return Expression.Lambda<Func<IDataRecord, string>>(block, dataRecordParameter).Compile();
    }

    /// <summary>
    /// Builds parameter expressions from the target delegate signature.
    /// </summary>
    /// <typeparam name="T">Delegate type.</typeparam>
    /// <param name="names">Optional parameter names.</param>
    /// <returns>Parameter expressions matching the delegate signature.</returns>
    private static ParameterExpression[] GetDelegateParameters<T>(string[] names) where T : Delegate
    {
        var delegateParameters = typeof(T).GetMethod("Invoke")?.GetParameters() ?? [];
        if (names.Length != 0 && names.Length != delegateParameters.Length)
        {
            throw new ArgumentException("Invalid number of names", nameof(names));
        }

        return delegateParameters
            .Select((p, i) => Expression.Parameter(p.ParameterType, names.Length > 0 ? names[i] : p.Name ?? $"arg{i}"))
            .ToArray();
    }

    /// <summary>
    /// Creates a formatter delegate from explicit parameter expressions.
    /// </summary>
    /// <typeparam name="T">Delegate type to compile.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="customFormatter">Formatter used for value rendering.</param>
    /// <param name="cultureInfo">Culture used for formatting operations.</param>
    /// <param name="parameterExpressions">Parameters to expose during expression compilation.</param>
    /// <returns>A compiled formatter delegate.</returns>
    private T CreateFromParameters<T>(
        string formatString,
        ICustomFormatter? customFormatter,
        CultureInfo? cultureInfo,
        params ParameterExpression[] parameterExpressions)
        where T : Delegate
    {
        var expressions = new List<Expression>();
        ParameterExpression? formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
        ParameterExpression? culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

        Expression body = GenerateCommands(formatString, formatter, culture, parameterExpressions);
        expressions.Add(body);

        var variables = new List<ParameterExpression>();
        if (formatter is not null)
        {
            variables.Add(formatter);
        }

        if (culture is not null)
        {
            variables.Add(culture);
        }

        BlockExpression block = Expression.Block(variables, expressions);
        return Expression.Lambda<T>(block, parameterExpressions).Compile();
    }

    /// <summary>
    /// Creates a formatter delegate that uses a custom interpolated string handler.
    /// </summary>
    /// <typeparam name="T">Delegate type to compile.</typeparam>
    /// <typeparam name="THandler">Interpolated string handler type.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="customFormatter">Formatter used for value rendering.</param>
    /// <param name="cultureInfo">Culture used for formatting operations.</param>
    /// <param name="parameterExpressions">Parameters to expose during expression compilation.</param>
    /// <returns>A compiled formatter delegate.</returns>
    private T CreateWithHandlerFromParameters<T, THandler>(
        string formatString,
        ICustomFormatter? customFormatter,
        CultureInfo? cultureInfo,
        params ParameterExpression[] parameterExpressions)
        where T : Delegate
    {
        var expressions = new List<Expression>();
        ParameterExpression? formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
        ParameterExpression? culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

        ParameterExpression? handlerStringBuilder = null;
        Type handlerType = typeof(THandler);
        if (handlerType.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(StringBuilder))))
        {
            handlerStringBuilder = Expression.Variable(typeof(StringBuilder), "builder");
            expressions.Add(Expression.Assign(handlerStringBuilder, Expression.New(typeof(StringBuilder))));
        }

        Type delegateReturnType = typeof(T).GetMethod("Invoke")?.ReturnType ?? typeof(void);
        bool returnHandler = delegateReturnType.IsAssignableFrom(handlerType);

        Expression body = GenerateCommands(
            handlerType,
            handlerStringBuilder is not null ? [handlerStringBuilder] : [],
            DefaultResolver,
            formatString,
            formatter,
            culture,
            parameterExpressions,
            returnHandler);
        expressions.Add(body);

        var variables = new List<ParameterExpression>();
        if (formatter is not null)
        {
            variables.Add(formatter);
        }

        if (culture is not null)
        {
            variables.Add(culture);
        }

        if (handlerStringBuilder is not null)
        {
            variables.Add(handlerStringBuilder);
        }

        BlockExpression block = Expression.Block(variables, expressions);
        return Expression.Lambda<T>(block, parameterExpressions).Compile();
    }

    /// <summary>
    /// Generates a sequence of commands to parse and execute a formatted string dynamically.
    /// Returns an expression that calls <c>ToString()</c> on the generated handler.
    /// </summary>
    private Expression GenerateCommands(
        string formatString,
        ParameterExpression? formatter,
        ParameterExpression? cultureInfo,
        ParameterExpression[] parameterExpressions)
        => Expression.Call(
            GenerateCommands(
                typeof(DefaultInterpolatedStringHandler),
                [],
                DefaultResolver,
                formatString,
                formatter,
                cultureInfo,
                parameterExpressions,
                true),
            typeof(DefaultInterpolatedStringHandler).GetMethod("ToString")
            ?? throw new InvalidOperationException("ToString method was not found on DefaultInterpolatedStringHandler."));

    /// <summary>
    /// Generates a sequence of commands to parse and execute a formatted string dynamically,
    /// returning an expression that performs the necessary operations.
    /// </summary>
    private Expression GenerateCommands(
        Type handlerType,
        ParameterExpression[] handlerParameters,
        IResolver resolver,
        string formatString,
        ParameterExpression? formatter,
        ParameterExpression? cultureInfo,
        ParameterExpression[] parameterExpressions,
        bool returnHandler)
    {
        var builder = new BlockExpressionBuilder();

        ParameterExpression cultureInfoExpression = ExpressionOrDefault(
            cultureInfo,
            "@@cultureInfo",
            typeof(CultureInfo),
            Expression.Property(null, CurrentCultureProperty),
            builder);

        ParameterExpression formatterExpression = ExpressionOrDefault(
            formatter,
            "@@formatter",
            typeof(ICustomFormatter),
            Expression.New(NullFormatterConstructor, cultureInfoExpression),
            builder);

        var parser = new InterpolatedStringParser(formatString);

        MethodInfo handlerAppendLiteral = handlerType.GetMethod("AppendLiteral", [typeof(string)])
            ?? throw new InvalidOperationException("AppendLiteral method was not found on handler type.");

        ParameterExpression handlerVariable = builder.GetOrCreateVariable(handlerType, "handler");

        int literalLength = parser.OfType<LiteralPart>().Sum(p => p.Length);
        int formattedCount = parser.OfType<FormattedPart>().Count();

        builder.Add(
            Expression.Assign(
                handlerVariable,
                CreateNewHandlerExpression(
                    handlerType,
                    literalLength,
                    formattedCount,
                    formatterExpression,
                    handlerParameters,
                    resolver)));

        IReadOnlyDictionary<string, Expression> symbols = parameterExpressions
            .ToDictionary(p => p.Name ?? string.Empty, static p => (Expression)p, StringComparer.Ordinal);

        foreach (IInterpolatedStringPart part in parser)
        {
            switch (part)
            {
                case LiteralPart literal:
                    builder.Add(
                        Expression.Call(handlerVariable, handlerAppendLiteral, Expression.Constant(literal.Text)));
                    break;

                case FormattedPart formattedPart:
                    Expression expression = Expression.Convert(
                        _compiler.Compile(formattedPart.ExpressionText, symbols),
                        typeof(object));

                    builder.Add(
                        CreateFormatCallExpression(
                            handlerVariable,
                            resolver,
                            expression,
                            formattedPart.Alignment,
                            formattedPart.Format));
                    break;
            }
        }

        builder.Add(returnHandler
            ? handlerVariable
            : Expression.Call(handlerVariable, handlerType.GetMethod("ToString", [])
                ?? throw new InvalidOperationException("ToString method was not found on handler type.")));

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
        ConstructorInfo[] constructors = handlerType.GetConstructors();
        ConstantExpression literalLengthConstant = Expression.Constant(literalLength);
        ConstantExpression formattedCountConstant = Expression.Constant(formattedCount);

        var parametersCases = new List<Expression[]>();

        Expression formatterArgument = formatter.Type == typeof(IFormatProvider)
            ? formatter
            : Expression.Convert(formatter, typeof(IFormatProvider));
        parametersCases.Add([literalLengthConstant, formattedCountConstant, formatterArgument]);

        parametersCases.Add([literalLengthConstant, formattedCountConstant]);
        parametersCases.Add([]);

        if (!handlerParameters.IsNullOrEmptyCollection())
        {
            parametersCases = parametersCases
                .Select(parameterCase => parameterCase.Concat(handlerParameters).ToArray())
                .Concat(parametersCases)
                .ToList();
        }

        foreach (Expression[] parameterCase in parametersCases)
        {
            var constructor = resolver.SelectConstructor(constructors, parameterCase);
            if (constructor is not null)
            {
                return Expression.New(constructor.Value.Method, constructor.Value.Parameters);
            }
        }

        throw new InvalidOperationException("No suitable constructor was found.");
    }

    /// <summary>
    /// Creates the call expression for <c>handler.AppendFormatted(...)</c>.
    /// </summary>
    private static Expression CreateFormatCallExpression(
        ParameterExpression handlerExpression,
        IResolver resolver,
        Expression expression,
        int? alignment,
        string? format)
    {
        var parametersCases = new List<Expression[]>();

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

        MethodInfo[] methods = handlerExpression.Type
            .GetMethods()
            .Where(m => m.Name == "AppendFormatted")
            .ToArray();

        foreach (Expression[] parametersCase in parametersCases)
        {
            var method = resolver.SelectMethod(methods, handlerExpression, null, parametersCase);
            if (method is not null)
            {
                return Expression.Call(handlerExpression, method.Value.Method, method.Value.Parameters);
            }
        }

        throw new InvalidOperationException("No suitable function was found for AppendFormatted.");
    }

    /// <summary>
    /// Creates and assigns a variable expression for use in a block.
    /// </summary>
    /// <typeparam name="T">The variable type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="addExpression">Action used to append assignment expressions.</param>
    /// <returns>The created variable expression, or <c>null</c> when value is <c>null</c>.</returns>
    private static ParameterExpression? CreateAndAssignVariable<T>(
        T? value,
        string name,
        Action<Expression> addExpression)
    {
        if (value is null)
        {
            return null;
        }

        ParameterExpression variable = Expression.Variable(typeof(T), name);
        addExpression(Expression.Assign(variable, Expression.Constant(value)));
        return variable;
    }

    /// <summary>
    /// Returns the existing expression or creates a variable initialized with a default value.
    /// </summary>
    /// <param name="expression">Existing expression.</param>
    /// <param name="name">Variable name to use when creating a default expression.</param>
    /// <param name="type">Variable type.</param>
    /// <param name="defaultValue">Default value expression.</param>
    /// <param name="builder">Block expression builder used to register assignments.</param>
    /// <returns>The existing or generated parameter expression.</returns>
    private static ParameterExpression ExpressionOrDefault(
        ParameterExpression? expression,
        string name,
        Type type,
        Expression defaultValue,
        BlockExpressionBuilder builder)
    {
        if (expression is not null)
        {
            return expression;
        }

        ParameterExpression variable = builder.GetOrCreateVariable(type, name);
        builder.Add(Expression.Assign(variable, defaultValue));
        return variable;
    }
}
