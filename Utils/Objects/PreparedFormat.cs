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

        private static string GenerateCommands(string formatString)
        {
            StringBuilder commands = new StringBuilder();
            commands.AppendLine("string.Concat(");
            foreach (Match match in parseFormatString.Matches(formatString))
            {
                if (match.Groups["error"].Success)
                {
                    throw new FormatException(string.Format("Chaîne de format incorrect : {0} était inattendu", match.Groups["error"].Value));
                }
                if (match.Groups["text"].Success)
                {
                    commands.AppendLine("\"" + match.Groups["text"].Value.Replace("\"", "\\\"") + "\",");
                }
                else if (match.Groups["expression"].Success)
                {
                    var alignment = match.Groups["alignment"].Success ? match.Groups["alignment"].Value : "0";
                    commands.AppendLine($"StringUtils.Align(formatter.Format(\"{match.Groups["format"].Value.Replace("\"", "\\\"")}\", {match.Groups["expression"].Value}, culture), {alignment}),");
                }
            }
            commands.Remove(commands.Length - 1, 1);
            commands.AppendLine(");");
            return commands.ToString();
        }

        public static T Create<T>(string formatString, params ParameterExpression[] parameterExpressions)
            where T : Delegate
            => Create<T>(formatString, NullFormatter.Default, CultureInfo.InvariantCulture, parameterExpressions);

        public static T Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, params ParameterExpression[] parameterExpressions)
        where T : Delegate
        {
            var formatter = Expression.Variable(typeof(ICustomFormatter), "formatter");
            var culture = Expression.Variable(typeof(CultureInfo), "culture");

            List<Expression> expressions = [];
            expressions.Add(Expression.Assign(formatter, Expression.Constant(customFormatter ?? NullFormatter.Default)));
            expressions.Add(Expression.Assign(culture, Expression.Constant(cultureInfo ?? CultureInfo.InvariantCulture)));
            string generatedCommand = GenerateCommands(formatString);
            expressions.Add(ExpressionParser.ParseExpression(generatedCommand, [.. parameterExpressions, formatter, culture], false, ["System", "Utils.Objects"]));

            var body = Expression.Block([formatter, culture], expressions);
            var lambda = Expression.Lambda<T>(body, parameterExpressions);
            return lambda.Compile();
        }

        public static Func<T, string> Create<T>(string formatString, ParameterExpression parameterExpression)
            => Create<T>(formatString, NullFormatter.Default, CultureInfo.InvariantCulture, parameterExpression);

        public static Func<T, string> Create<T>(string formatString, ICustomFormatter customFormatter, CultureInfo cultureInfo, ParameterExpression parameterExpression)
        {
            var formatter = Expression.Variable(typeof(ICustomFormatter), "formatter");

            List<Expression> expressions = new List<Expression>();
            expressions.Add(Expression.Assign(formatter, Expression.Constant(customFormatter ?? NullFormatter.Default)));
            expressions.Add(ExpressionParser.ParseExpression(GenerateCommands(formatString), [parameterExpression], true, ["System", "Utils.Objects"]));

            var body = Expression.Block([formatter], expressions);
            var lambda = Expression.Lambda<Func<T, string>>(body, [parameterExpression]);
            return lambda.Compile();
        }

    }
}
