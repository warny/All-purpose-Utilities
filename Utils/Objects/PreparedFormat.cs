using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Expressions;

namespace Utils.Objects;

public static partial class StringFormat
{
    private static readonly string[] defaultNamespaces = ["System", "System.Linq", "System.Text", "Utils.Objects"];

    [GeneratedRegex(@"
        (
	        \{(?<text>\{)
	        |
	        \}(?<text>\})
	        |
	        \{\s*(?<expression>((?>\(((?<p>\()|(?<-p>\))|[^()]*)*\))(?(p)(?!))|[^():,])*?)(,(?<alignment>[+-]?\d+))?(:(?<format>.+?))?\s*\}
	        |
	        (?<text>[^{}]+)
	        |
	        (?<error>[{}])
        )
        ", RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex MyRegex();
    /// <summary>
    /// Regex used for parsing a string of the form
    /// <example>{field[:format]}text{field2[:format2]}...</example>
    /// <remarks>
    /// Double braces ("{{" or "}}") are considered as text and are replaced
    /// by a single brace.
    /// </remarks>
    /// </summary>
    private static readonly Regex parseFormatString = MyRegex();


    private static readonly MethodInfo alignMethod = typeof(StringUtils).GetMethod(nameof(StringUtils.Align), [typeof(string), typeof(int)]);
    private static readonly MethodInfo customFormatMethod = typeof(ICustomFormatter).GetMethod("Format", [typeof(string), typeof(object), typeof(IFormatProvider)]);
    private static readonly PropertyInfo currentCultureProperty = typeof(CultureInfo).GetProperty(nameof(CultureInfo.CurrentCulture));
    private static readonly ConstructorInfo nullFormatterConstructor = typeof(NullFormatter).GetConstructor([typeof(CultureInfo)]);
    private static readonly MethodInfo stringConcatMethod = typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])]);

    private static Expression GenerateCommands(string formatString, ParameterExpression formatter, ParameterExpression cultureInfo, ParameterExpression[] parameterExpressions, bool defaultFirst, string[] namespaces)
    {
        var result = new List<Expression>();
        var variables = new List<ParameterExpression>();
        ParameterExpression cultureInfoExpression = ExpressionOrDefault(cultureInfo, "@@cultureInfo", typeof(CultureInfo), Expression.Property(null, currentCultureProperty), variables, result);
        ParameterExpression formatterExpression = ExpressionOrDefault(formatter, "@@formater", typeof(ICustomFormatter), Expression.New(nullFormatterConstructor, [cultureInfoExpression]), variables, result);

        List<Expression> commands = new();
        foreach (Match match in parseFormatString.Matches(formatString))
        {
            if (match.Groups["error"].Success)
            {
                throw new FormatException(string.Format("Chaîne de format incorrect : {0} était inattendu", match.Groups["error"].Value));
            }
            if (match.Groups["text"].Success)
            {
                if (commands.Any() && commands[^1] is ConstantExpression ce && ce.Value is string s)
                {
                    commands[^1] = Expression.Constant(string.Concat(s, match.Groups["text"].Value), typeof(string));
                }
                else
                {
                    commands.Add(Expression.Constant(match.Groups["text"].Value, typeof(string)));
                }
            }
            else if (match.Groups["expression"].Success)
            {
                Expression command = Expression.Call(
                    formatterExpression, customFormatMethod,
                    [
                        Expression.Constant(match.Groups["format"].Value),
                        Expression.Convert(ExpressionParser.ParseExpression(match.Groups["expression"].Value, parameterExpressions, null, defaultFirst, namespaces), typeof(object)),
                        cultureInfoExpression
                    ]
                );

                if (match.Groups["alignment"].Success) {
                    command = Expression.Call(null, alignMethod, [command, Expression.Constant(int.Parse(match.Groups["alignment"].Value), typeof(int))]);
                }

                commands.Add(command);
            }
        }
        result.Add(Expression.Call(null, stringConcatMethod, Expression.NewArrayInit(typeof(string), commands)));

        if (!variables.Any() && result.Count == 1) return result[0];
        return Expression.Block(typeof(string), variables, result);
    }

    /// <summary>
    /// Create a string formatter using the given delegate type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">Type of the formatter function</typeparam>
    /// <param name="formatString">Interpolated format string</param>
    /// <param name="names">names of argument used in interpolated string, if no names are provided, use <typeparamref name="T"/> names instead</param>
    /// <returns>A function that builds a string from the provided interpolated string</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="names"/> are provided and does not match <typeparamref name="T"/> arguments count</exception>
    public static T Create<T>(string formatString, params string[] names)
        where T : Delegate
        => Create<T>(formatString, null, null, names);

    /// <summary>
    /// Create a string formatter using the given delegate type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">Type of the formatter function</typeparam>
    /// <param name="formatString">Interpolated format string</param>
    /// <param name="customFormatter">Custom formatter</param>
    /// <param name="cultureInfo">Culture Info</param>
    /// <param name="names">names of argument used in interpolated string, if no names are provided, use <typeparamref name="T"/> names instead</param>
    /// <returns>A function that builds a string from the provided interpolated string</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="names"/> are provided and does not match <typeparamref name="T"/> arguments count</exception>
    public static T Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, params string[] names)
    where T : Delegate
    {
        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        if (names.Length != 0 && names.Length != delegateParameters.Length) throw new ArgumentException("Invalid number of names", nameof(names));

        var parameters = new ParameterExpression[delegateParameters.Length];

        if (names.Length == 0) names = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            parameters[i] = Expression.Parameter(delegateParameters[i].ParameterType, names?[i] ?? delegateParameters[i].Name);
        }

        return Create<T>(formatString, customFormatter, cultureInfo, parameters);
    }

    /// <summary>
    /// Create a string formatter using the given delegate type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">Type of the formatter function</typeparam>
    /// <param name="formatString">Interpolated format string</param>
    /// <param name="customFormatter">Custom formatter</param>
    /// <param name="cultureInfo">Culture Info</param>
    /// <param name="parameterExpressions">parameters for the output function</param>
    /// <returns>A function that builds a string from the provided interpolated string</returns>
    public static T Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, params ParameterExpression[] parameterExpressions)
    where T : Delegate
    {
        List<Expression> expressions = [];
        ParameterExpression formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
        ParameterExpression culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

        Expression result = Create(formatString, parameterExpressions, formatter, culture, false, defaultNamespaces);

        expressions.Add(result);
        var blockVariables = new ParameterExpression[] { formatter, culture }.Where(p => p is not null);
        var body = Expression.Block(blockVariables, expressions);
        var lambda = Expression.Lambda<T>(body, parameterExpressions);
        return lambda.Compile();
    }

    /// <summary>
    /// Create a string formatter for the given <paramref name="dataRecord"/>
    /// Names are fields names, if multiple fields share the same name, the first gets raw name, others are suffixed with a _ and their relative position in order
    /// </summary>
    /// <typeparam name="T">Type of the formatter function</typeparam>
    /// <param name="formatString">Interpolated format string</param>
    /// <param name="customFormatter">Custom formatter</param>
    /// <param name="cultureInfo">Culture Info</param>
    /// <param name="dataRecord"><see cref="IDataRecord"/> for which to create an interpolated string</param>
    /// <returns>A function that builds a string from the provided interpolated string</returns>
    public static Func<IDataRecord, string> Create(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, IDataRecord dataRecord)
    {
        var getItem = typeof(IDataRecord).GetMethod("get_Item", [typeof(int)]);
        var expressions = new List<Expression>();

        ParameterExpression formatter = CreateAndAssignVariable(customFormatter, "formatter", expressions.Add);
        ParameterExpression culture = CreateAndAssignVariable(cultureInfo, "culture", expressions.Add);

        var drFields = new (int Index, Type Type, string Name)[dataRecord.FieldCount];
        for (int i = 0; i < dataRecord.FieldCount; i++)
        {
            Type fieldType = dataRecord.GetFieldType(i);
            drFields[i] = (i, fieldType, dataRecord.GetName(i));
        }

        var fieldsGroups = drFields.GroupBy(x => x.Name);

        var dataRecordParameter = Expression.Parameter(typeof(IDataRecord), "<dataRecord>");

        var variables = new ParameterExpression[dataRecord.FieldCount];
        foreach (var group in fieldsGroups)
        {
            foreach (var field in group.Select((Field, Index) => (Field, Index)))
            {
                variables[field.Field.Index] = Expression.Parameter(field.Field.Type, field.Field.Name + (field.Index > 0 ? "_" + field.Index.ToString() : ""));
                expressions.Add(Expression.Assign(variables[field.Field.Index], Expression.Convert(Expression.Call(dataRecordParameter, getItem, Expression.Constant(field.Field.Index)), field.Field.Type)));
            }
        }
        var result = Create(formatString, variables, formatter, culture, false, defaultNamespaces);

        expressions.Add(result);
        var blockVariables = variables.Append(formatter).Append(culture).Where(p => p is not null);
        var body = Expression.Block(blockVariables, expressions);
        var lambda = Expression.Lambda<Func<IDataRecord, string>>(body, [dataRecordParameter]);
        return lambda.Compile();
    }


    /// <summary>
    /// Create a string formatting expression
    /// </summary>
    /// <param name="formatString">Interpolated format string</param>
    /// <param name="formatter">Custom formatter variable</param>
    /// <param name="culture">Culture Info variable</param>
    /// <param name="parameterExpressions">parameters for the output function</param>
    /// <param name="namespaces">namespaces for classes resolution</param>
    /// <returns>An expression that builds a string from the provided interpolated string</returns>
    public static Expression Create(string formatString, ParameterExpression[] parameterExpressions, ParameterExpression formatter, ParameterExpression culture, bool defaultFirst, string[] namespaces)
    {
        var allParameters = parameterExpressions.Append(formatter).Append(culture).Where(p => p is not null).Distinct().ToArray();
        var result = GenerateCommands(formatString, formatter, culture, parameterExpressions, defaultFirst, namespaces);
        return result;
    }

    private static ParameterExpression CreateAndAssignVariable<T>(T value, string name, Action<BinaryExpression> action)
    {
        if (value is null) return null;
        var result = Expression.Variable(typeof(T), name);
        action(Expression.Assign(result, Expression.Constant(value)));
        return result;
    }

    private static ParameterExpression ExpressionOrDefault(ParameterExpression expression, string propertyName, Type type, Expression defaultValue, List<ParameterExpression> variables, List<Expression> expressions)
    {
        if (expression != null) return expression;

        ParameterExpression result = Expression.Variable(type, propertyName);
        variables.Add(result);
        expressions.Add(Expression.Assign(result, defaultValue));
        return result;
    }


}
