using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Collections;
using Utils.Expressions;
using Utils.Mathematics;

namespace Utils.Objects;

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
		{
			brackets = Brackets.All;
		}
		if (str.IsNullOrWhiteSpace()) return "";

		int start = 0, end = str.Length - 1;
		while (str[end] == ' ') end--;

		for (int i = 0; i < str.Length; i++)
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
		Regex regex = new Regex(@"\((?<singular>\w+)\|(?<plural>\w+)\)|\((?<plural>\w+)\)");
		return regex.Replace(str, m =>
		{
			if (number.Between(-1, 1))
			{
				return m.Groups["singular"]?.Value ?? "";
			}
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

		List<string> result = new List<string>();
		var lastindex = 0;
		int state = normal;
		for (int i = 0; i < line.Length; i++)
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
		if (str.StartsWith("\"") && str.EndsWith("\""))
		{
			return str.Substring(1, str.Length - 2).Replace("\"\"", "\"");
		}
		return str;
	}

	/// <summary>
	/// Cette procédure prend une chaîne de caractères en entrée et retourne une nouvelle chaîne de caractères dans laquelle certains caractères spéciaux ont été échappés en vue de les utiliser dans une expression régulière.
	/// </summary>
	/// <param name="str">Chaîne à echapper</param>
	/// <returns>Chaîne où les caractères spéciaux sont echappés</returns>
	public static string EscapeForRegex(string str)
	{
		StringBuilder result = new StringBuilder(str.Length * 2);
		foreach (var c in str)
		{
			if (!char.IsLetter(c) && !char.IsDigit(c))
			{
				result.Append('\\');
			}
			result.Append(c);
		}
		return result.ToString();
	}

	public static IEnumerable<string> SplitCommaSeparatedList(this string commaSeparatedValues, char commaChar, params Parenthesis[] depthMarkerChars)
			=> SplitCommaSeparatedList(commaSeparatedValues, commaChar, false, depthMarkerChars);

	public static IEnumerable<string> SplitCommaSeparatedList(this string commaSeparatedValues, char commaChar, bool removeEmptyEntries, params Parenthesis[] depthMarkerChars)
	{
		var lastTypeIndex = 0;
		var depth = new Stack<Parenthesis>();
		for (int i = 0; i < commaSeparatedValues.Length; i++)
		{
			char current = commaSeparatedValues[i];
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
			if ((m = depthMarkerChars.FirstOrDefault(m => m.End[0] == current)) != null)
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
/// Définit une paire de parenthèses
/// </summary>
public class Brackets
{
	public char Open { get; }
	public char Close { get; }

	public Brackets(string brackets) : this(brackets[0], brackets[1]) { }

	public Brackets(char open, char close)
	{
		this.Open = open;
		this.Close = close;
	}

	public static Brackets RoundBrackets { get; } = new Brackets('(', ')');
	public static Brackets SquareBrackets { get; } = new Brackets('[', ']');
	public static Brackets Braces { get; } = new Brackets('{', '}');

	public static Brackets[] All { get; } = new[] { RoundBrackets, SquareBrackets, Braces };

	public override string ToString() => $" {Open} ... {Close} ";
}
