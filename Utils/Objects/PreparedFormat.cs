using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Utils.Arrays;
using Utils.Collections;
using Utils.Expressions;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Utils.Objects
{


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

        private static string GenerateCommands(string formatString, ParameterExpression formatter, ParameterExpression cultureInfo)
        {
            string alignName = typeof(StringUtils).FullName + "." + nameof(StringUtils.Align);
            string formatterName = formatter?.Name ?? typeof (NullFormatter).FullName + "." + nameof(NullFormatter.Default);
            string cultureName = cultureInfo?.Name ?? typeof(CultureInfo).FullName + "." + nameof(CultureInfo.CurrentCulture);

            List<string> commands = new();
            foreach (Match match in parseFormatString.Matches(formatString))
            {
                if (match.Groups["error"].Success)
                {
                    throw new FormatException(string.Format("Chaîne de format incorrect : {0} était inattendu", match.Groups["error"].Value));
                }
                if (match.Groups["text"].Success)
                {
                    if (commands.Any() && commands[^1].EndsWith("\""))
                    {
                        commands[^1] = string.Concat(commands[^1].AsSpan(0, commands[^1].Length - 1), match.Groups["text"].Value.Replace("\"", "\\\""), "\"");
                    }
                    else
                    {
                        commands.Add("\"" + match.Groups["text"].Value.Replace("\"", "\\\"") + "\"");
                    }
                }
                else if (match.Groups["expression"].Success)
                {
                    var alignment = match.Groups["alignment"].Success ? match.Groups["alignment"].Value : "0";
                    commands.Add($"{alignName}({formatterName}.Format(\"{match.Groups["format"].Value.Replace("\"", "\\\"")}\", {match.Groups["expression"].Value}, {cultureName}), {alignment})");
                }
            }
            return "string.Concat(" + Environment.NewLine + string.Join("," + Environment.NewLine, commands) + Environment.NewLine + ");";
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
            string generatedCommand = GenerateCommands(formatString, formatter, culture);
            var result = ExpressionParser.ParseExpression(generatedCommand, allParameters, null, defaultFirst, namespaces);
            return result;
        }

        private static ParameterExpression CreateAndAssignVariable<T>(T value, string name, Action<BinaryExpression> action)
        {
            if (value is null) return null;
            var result = Expression.Variable(typeof(T), name);
            action(Expression.Assign(result, Expression.Constant(value)));
            return result;
        }

    }
}
