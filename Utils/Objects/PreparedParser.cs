using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Utils.Expressions;

namespace Utils.Objects;


/*
public class PreparedParser<T>
{
	[GeneratedRegex(@"
        (
	        \{(?<special>\{)
	        |
	        \}(?<special>\})
	        |
	        \{\s*(?<expression>((?>\(((?<p>\()|(?<-p>\))|[^()]*)*\))(?(p)(?!))|[^():,])*?)(,(?<alignment>[+-]?\d+))?(:(?<format>.+?))?\s*\}
	        |
			(?<special>[\^$.|?*+()[\]{}])
			|
	        (?<text>[^{}]+?)
	        |
	        (?<error>[{}])
        )
        ", RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
	private static partial Regex CreatePrepareRegex();
	/// <summary>
	/// Regex used for parsing a string of the form
	/// <example>{field[:format]}text{field2[:format2]}...</example>
	/// <remarks>
	/// Double braces ("{{" or "}}") are considered as text and are replaced
	/// by a single brace.
	/// </remarks>
	/// </summary>
	private static readonly Regex parseFormatString = CreatePrepareRegex();
	private readonly Func<T, string> parseValue;

	public PreparedParser(string formatString, CultureInfo cultureInfo = null)
	{
		parseValue = CreateParser(parseFormatString.Matches(formatString), []);
	}

	public PreparedParser(string formatString, Dictionary<string, Action<T, string>> parsingActions, CultureInfo cultureInfo = null)
	{
		parseValue = CreateParser(parseFormatString.Matches(formatString), parsingActions);
	}

	private Func<T, string> CreateParser(MatchCollection parsedFormatString, Dictionary<string, Action<T, string>> parsingActions)
	{
		Type type = typeof(T);

		List<Expression> expressions = new List<Expression>();



		StringBuilder newRegex = new StringBuilder();
		foreach (Match match in parsedFormatString)
		{

			foreach (Group group in match.Groups)
			{
				switch (group.Name)
				{
					case "error":
						throw new FormatException($"Incorrect format string : unattend {group.Value} at {group.Index}");
					case "special":
						newRegex.Append('\\');
						newRegex.Append(group.Value);
						break;
					case "text":
						newRegex.Append(group.Value);
						break;
					case "expression":
						ExpressionParser.ParseExpression(match.Groups["expression"].Value, parameterExpressions, null, defaultFirst, namespaces)
						break;
				}
			}
		}
	}


	private static readonly Dictionary<string, Dictionary<Type, string>> _parsersCache
		= new Dictionary<string, Dictionary<Type, string>>();
	private static readonly object _lock = new object();

	public static Dictionary<Type, string> CreateBasicTypesParsers(CultureInfo cultureInfo)
	{
		string cacheKey = cultureInfo.Name;

		// Return cached parsers if they already exist
		if (_parsersCache.ContainsKey(cacheKey))
		{
			return _parsersCache[cacheKey];
		}

		lock (_lock)
		{
			// Double-checked locking to avoid duplicate initialization
			if (!_parsersCache.TryGetValue(cacheKey, out Dictionary<Type, string> value))
			{
				string escapedGroupSeparator = Regex.Escape(cultureInfo.NumberFormat.NumberGroupSeparator);
				string escapedDecimalSeparator = Regex.Escape(cultureInfo.NumberFormat.NumberDecimalSeparator);

				var integerFormat = $"(\\d|{escapedGroupSeparator})+";
				var signedIntegerFormat = $"[{Regex.Escape(cultureInfo.NumberFormat.NegativeSign)}]?" + integerFormat;
				var decimalNumberFormat = signedIntegerFormat + escapedDecimalSeparator + integerFormat;

				var guidFormat = @"^[{(]?[0-9A-Fa-f]{8}(-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}[)}]?$";
				var boolFormat = @"^(true|false|True|False)$";
				var charFormat = @"^.$";
				var timeSpanFormat = @"^(-)?\d{1,2}:\d{2}:\d{2}(\.\d{1,7})?$";
				var byteArrayFormat = @"^([0-9A-Fa-f]{2}\s*)+$";

				string shortDateRegex = BuildDateTimeRegex(cultureInfo.DateTimeFormat.ShortDatePattern);
				string longDateRegex = BuildDateTimeRegex(cultureInfo.DateTimeFormat.LongDatePattern);
				string shortTimeRegex = BuildDateTimeRegex(cultureInfo.DateTimeFormat.ShortTimePattern);
				string longTimeRegex = BuildDateTimeRegex(cultureInfo.DateTimeFormat.LongTimePattern);

				string dateTimeRegex = $"({shortDateRegex}|{longDateRegex})\\s({shortTimeRegex}|{longTimeRegex})";

				var parsers = new Dictionary<Type, string>
				{
					{ typeof(string), ".*" },
					{ typeof(sbyte), signedIntegerFormat },
					{ typeof(short), signedIntegerFormat },
					{ typeof(int), signedIntegerFormat },
					{ typeof(long), signedIntegerFormat },
					{ typeof(byte), integerFormat },
					{ typeof(ushort), integerFormat },
					{ typeof(uint), integerFormat },
					{ typeof(ulong), integerFormat },
					{ typeof(float), decimalNumberFormat },
					{ typeof(double), decimalNumberFormat },
					{ typeof(decimal), decimalNumberFormat },
					{ typeof(Guid), guidFormat },
					{ typeof(bool), boolFormat },
					{ typeof(char), charFormat },
					{ typeof(TimeSpan), timeSpanFormat },
					{ typeof(byte[]), byteArrayFormat },
					{ typeof(DateTime), dateTimeRegex },
					{ typeof(DateOnly), $"({shortDateRegex}|{longDateRegex})" },
					{ typeof(TimeOnly), $"({shortTimeRegex}|{longTimeRegex})" },
					{ typeof(MailAddress), @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b" },
					{ typeof(Uri), @"\b(?:https?|ftp):\/\/[^\s/$.?#].[^\s]*\b" },
					{ typeof(IPAddress), @"(\b(?:\d{1,3}\.){3}\d{1,3}\b|\b(?:[a-fA-F0-9:]+:+)+[a-fA-F0-9]+\b)" }
				};
				value = parsers;

				// Add to cache
				_parsersCache[cacheKey] = value;
			}

			return value;
		}
	}

	private static string BuildDateTimeRegex(string pattern)
	{
		return Regex.Replace(Regex.Escape(pattern), @"\w+",
			(Match m) => m.Value switch
			{
				"yyyy" => "\\d{4}",
				"yy" => "\\d{2}",
				"MM" => "\\d{2}",
				"M" => "\\d{1,2}",
				"dddd" => "\\w+",
				"ddd" => "\\w+\\.?",
				"dd" => "\\d{2}",
				"d" => "\\d{1,2}",
				"HH" => "\\d{2}",
				"H" => "\\d{1,2}",
				"hh" => "\\d{2}",
				"h" => "\\d{1,2}",
				"mm" => "\\d{2}",
				"m" => "\\d{1,2}",
				"ss" => "\\d{2}",
				"s" => "\\d{1,2}",
				"tt" => "(AM|PM|am|pm)",
				"f" => "\\d",
				"FFF" => "\\d{3}",
				_ => m.Value
			});
	}
}
*/
