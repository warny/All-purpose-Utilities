using System.Text;
using System.Text.RegularExpressions;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.String;

/// <summary>
/// Provides helper methods for working with strings, including trimming brackets and parsing delimited content.
/// </summary>
public static class StringUtils
{
	/// <summary>
	/// Supprime les parenthèses autour d'une chaîne
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string TrimBrackets(this string str, char openingBracket, char closingBracket)
		=> TrimBrackets(str, new Brackets(openingBracket, closingBracket));

	/// <summary>
	/// Supprime les parenthèses autour d'une chaîne
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string TrimBrackets(this string str, char bracket)
		=> TrimBrackets(str, new Brackets(bracket, bracket));

	/// <summary>
	/// Supprime les parenthèses autour d'une chaîne
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string TrimBrackets(string str, params Brackets[] brackets)
	{
		if (brackets.IsNullOrEmptyCollection())
			brackets = Brackets.All;
		if (str.IsNullOrWhiteSpace()) return "";

		int start = 0, end = str.Length - 1;
		while (str[end] == ' ') end--;

		for (var i = 0; i < str.Length; i++)
		{
			var c = str[i];
			if (c == ' ')
			{
				start = i + 1;
				continue;
			}
			Brackets currentBrackets = brackets.FirstOrDefault(b => b.Open == c);
			if (currentBrackets == null) break;
			if (str[end] == currentBrackets.Close)
			{
				start = i + 1;
				end--;
			}
			while (str[end] == ' ') end--;
		}

		return str.Substring(start, end - start + 1);
	}

	/// <summary>
	/// Transform a string in the form "word(s)" or "chev(al|aux)" into its singular or plural form.
	/// </summary>
	/// <param name="str">String to transform</param>
	/// <param name="number">Number of objects</param>
	/// <returns></returns>
	public static string ToPlural(this string str, long number)
	{
		var regex = new Regex(@"\((?<singular>\w+)\|(?<plural>\w+)\)|\((?<plural>\w+)\)");
		return regex.Replace(str, m =>
		{
			if (number.Between(-1, 1))
				return m.Groups["singular"]?.Value ?? "";
			else
			{
				return m.Groups["plural"].Value;
			}
		});
	}


	/// <summary>
	/// Décompose la chaîne en tableau de ligne de commande
	/// </summary>
	/// <param name="line">Ligne de commande</param>
	/// <returns>Décomposition en tableau</returns>
	public static string[] ParseCommandLine(string line)
	{
		const int normal = 0;
		const int instring = 1;

		var result = new List<string>();
		var lastindex = 0;
		var state = normal;
		for (var i = 0; i < line.Length; i++)
		{
			var c = line[i];
			switch (state)
			{
				case normal:
					{
						switch (c)
						{
							case ' ':
								{
									result.Add(line.Substring(lastindex, i - lastindex).TrimBrackets('\"'));
									lastindex = i + 1;
								}
								break;
							case '\"':
								{
									state = instring;
								}
								break;
						}
					}
					break;
				case instring:
					{
						switch (c)
						{
							case '\"':
								{
									state = normal;
								}
								break;
						}
					}
					break;
			}
		}
		result.Add(TrimQuotes(line.Substring(lastindex)));
		return result.Where(r => !r.IsNullOrWhiteSpace()).ToArray();
	}

	/// <summary>
	/// Supprime les " lorsqu'une chaîne en est entourée 
	/// </summary>
	/// <param name="str">chaine à modifier</param>
	/// <returns>chaine nettoyée</returns>
	private static string TrimQuotes(string str)
	{
		if (str.Length>=2 &&  str[0] == '\"' && str[^1] == '\"')
			return str[1..^1].Replace("\"\"", "\"");
		return str;
	}

	/// <summary>
	/// Cette procédure prend une chaîne de caractères en entrée et retourne une nouvelle chaîne de caractères dans laquelle certains caractères spéciaux ont été échappés en vue de les utiliser dans une expression régulière.
	/// </summary>
	/// <param name="str">Chaîne à echapper</param>
	/// <returns>Chaîne où les caractères spéciaux sont echappés</returns>
	public static string EscapeForRegex(string str)
	{
		var result = new StringBuilder(str.Length * 2);
		foreach (var c in str)
		{
			if (!char.IsLetter(c) && !char.IsDigit(c))
				result.Append('\\');
			result.Append(c);
		}
		return result.ToString();
	}

        /// <summary>
        /// Splits a comma-separated string while respecting nested markers such as brackets or braces.
        /// </summary>
        /// <param name="commaSeparatedValues">The string containing comma-separated values.</param>
        /// <param name="commaChar">The character that separates values.</param>
        /// <param name="depthMarkerChars">The markers that define nesting boundaries.</param>
        /// <returns>A sequence of values extracted from the string.</returns>
        public static IEnumerable<string> SplitCommaSeparatedList(this string commaSeparatedValues, char commaChar, params Parenthesis[] depthMarkerChars)
                        => commaSeparatedValues.SplitCommaSeparatedList(commaChar, false, depthMarkerChars);

        /// <summary>
        /// Splits a comma-separated string while respecting nested markers such as brackets or braces, with control over empty entries.
        /// </summary>
        /// <param name="commaSeparatedValues">The string containing comma-separated values.</param>
        /// <param name="commaChar">The character that separates values.</param>
        /// <param name="removeEmptyEntries">True to omit empty results from the output; otherwise false.</param>
        /// <param name="depthMarkerChars">The markers that define nesting boundaries.</param>
        /// <returns>A sequence of values extracted from the string.</returns>
        public static IEnumerable<string> SplitCommaSeparatedList(this string commaSeparatedValues, char commaChar, bool removeEmptyEntries, params Parenthesis[] depthMarkerChars)
	{
		var lastTypeIndex = 0;
		var depth = new Stack<Parenthesis>();
		for (var i = 0; i < commaSeparatedValues.Length; i++)
		{
			var current = commaSeparatedValues[i];
			Parenthesis m;
			if ((m = depthMarkerChars.FirstOrDefault(m => m.Start[0] == current)) != null)
			{
				if (depth.Any() && m.Start == m.End)
				{
					depth.Pop();
					continue;
				}

				depth.Push(m);
				continue;
			}
			if (depthMarkerChars.FirstOrDefault(m => m.End[0] == current) is not null)
			{
				var startChar = depth.Pop();
				if (startChar.End[0] == current) continue;
				throw new Exception(commaSeparatedValues);
			}
			if (current == commaChar && !depth.Any())
			{
				var value = commaSeparatedValues[lastTypeIndex..i];
				if (!removeEmptyEntries || !string.IsNullOrEmpty(value)) yield return value;
				lastTypeIndex = i + 1;
			}
		}
		{
			var value = commaSeparatedValues[lastTypeIndex..];
			if (!removeEmptyEntries || !string.IsNullOrEmpty(value)) yield return value;
		}
	}

}

/// <summary>
/// Represents a pair of characters that mark the beginning and end of a delimited block.
/// </summary>
public class Brackets
{
        /// <summary>
        /// Gets the opening character of the bracket pair.
        /// </summary>
        public char Open { get; }

        /// <summary>
        /// Gets the closing character of the bracket pair.
        /// </summary>
        public char Close { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Brackets"/> class using a two-character string.
        /// </summary>
        /// <param name="brackets">A string containing the opening and closing characters.</param>
        public Brackets(string brackets) : this(brackets[0], brackets[1]) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Brackets"/> class.
        /// </summary>
        /// <param name="open">The opening character.</param>
        /// <param name="close">The closing character.</param>
        public Brackets(char open, char close)
        {
                this.Open = open;
                this.Close = close;
        }

        /// <summary>
        /// Gets a <see cref="Brackets"/> instance representing round brackets (parentheses).
        /// </summary>
        public static Brackets RoundBrackets { get; } = new Brackets('(', ')');

        /// <summary>
        /// Gets a <see cref="Brackets"/> instance representing square brackets.
        /// </summary>
        public static Brackets SquareBrackets { get; } = new Brackets('[', ']');

        /// <summary>
        /// Gets a <see cref="Brackets"/> instance representing braces.
        /// </summary>
        public static Brackets Braces { get; } = new Brackets('{', '}');

        /// <summary>
        /// Gets all bracket instances provided by the utility.
        /// </summary>
        public static Brackets[] All { get; } = [RoundBrackets, SquareBrackets, Braces];

        /// <summary>
        /// Returns a string that represents the bracket pair.
        /// </summary>
        /// <returns>A string describing the bracket pair.</returns>
        public override string ToString() => $" {Open} ... {Close} ";
}
