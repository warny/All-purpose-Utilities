using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Collections;
using Utils.Mathematics;

namespace Utils.Objects
{
	public static class StringUtils
	{
		private static readonly char[] defaultRandomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ".ToCharArray();

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="length">Longueur de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(int length, char[] characters = null) 
			=> RandomString(length, length, characters);

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="minLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="maxLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(int minLength, int maxLength, char[] characters = null) 
			=> RandomString(new Random(), minLength, maxLength, characters);

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="r">Générateur de nombres aléatoires</param>
		/// <param name="length">Longueur de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(this Random r, int length, char[] characters = null) 
			=> RandomString(r, length, length, characters);

		/// <summary>
		/// Génère une chaîne de caractères aléatoires
		/// </summary>
		/// <param name="r">Générateur de nombres aléatoires</param>
		/// <param name="minLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="maxLength">Longueur minimale de la chaîne à générer</param>
		/// <param name="characters">Caractères à utiliser</param>
		/// <returns>Chaîne aléatoire</returns>
		public static string RandomString(this Random r, int minLength, int maxLength, char[] characters = null)
		{
			r.ArgMustNotBeNull();
			characters ??= defaultRandomCharacters;
			var length = r.Next(minLength, maxLength);

			char[] result = new char[length];
			for (int i = 0; i < length; i++)
			{
				result[i] = characters[r.Next(0, characters.Length - 1)];
			}
			return new string(result);
		}

		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase">true : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like(this string value, string pattern, bool ignoreCase = false, TextInfo textInfo = null) {
			value.ArgMustNotBeNull();
			pattern.ArgMustNotBeNull();
			return Like(value.AsSpan(), pattern, ignoreCase, textInfo);
		}

		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase">true : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase = false, CultureInfo cultureInfo = null) {
			pattern.ArgMustNotBeNull();
			return Like(value, pattern, ignoreCase, cultureInfo.TextInfo);
		}


		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase">true : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase = false, TextInfo textInfo = null)
		{
			pattern.ArgMustNotBeNull();
			if (pattern == "*") return true;
			textInfo ??= CultureInfo.CurrentCulture.TextInfo;

			Func<char, char, bool> equals = ignoreCase
				? (x, y) => textInfo.ToLower(x) == textInfo.ToLower(y)
				: (x, y) => x == y;

			int valueIndex = 0, wildcardIndex = 0;
			int valueNext = 0, wildcardNext = 0;

			while (valueIndex < value.Length && wildcardIndex < pattern.Length && pattern[wildcardIndex] != '*')
			{
				if (pattern[wildcardIndex] != '?' && !Equals(value[valueIndex], pattern[wildcardIndex]))
				{
					return false;
				}
				wildcardIndex++;
				valueIndex++;
			}

			while (wildcardIndex < pattern.Length && valueIndex < value.Length)
			{
				if (pattern[wildcardIndex] == '*')
				{
					wildcardNext = wildcardIndex;
					wildcardIndex++;
					if (wildcardIndex >= pattern.Length)
					{
						return true;
					}
					valueNext += 1;
				}
				else if (pattern[wildcardIndex] == '?' || equals(value[valueIndex], pattern[wildcardIndex]))
				{
					wildcardIndex++;
					valueIndex++;
					if (wildcardIndex >= pattern.Length && valueIndex < value.Length && pattern[wildcardNext] == '*') wildcardIndex = wildcardNext + 1;
				}
				else
				{
					wildcardIndex = wildcardNext + 1;
					valueIndex = valueNext++;
				}
			}

			while (wildcardIndex < pattern.Length && pattern[wildcardIndex] == '*') wildcardIndex++;
			return wildcardIndex >= pattern.Length && valueIndex >= value.Length;
		}

		/// <summary>
		/// Supprime du début et de la fin de la chaîne tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer le caractère)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static string Trim(this string s, Func<char, bool> trimTester)
		{
			s.ArgMustNotBeNull();
			trimTester.ArgMustNotBeNull();

			int start, end = s.Length;
			for (start = 0; start < end; start++)
			{
				if (!trimTester(s[start])) break;
			}
			for (end = s.Length - 1; end > start; end--)
			{
				if (!trimTester(s[end])) break;
			}
			if (start >= end) return "";
			return s.Substring(start, end-start + 1);
		}

		/// <summary>
		/// Supprime du début de la chaîne tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer le caractère)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static string TrimStart(this string s, Func<char, bool> trimTester)
		{
			s.ArgMustNotBeNull();
			trimTester.ArgMustNotBeNull();

			int start, end = s.Length;
			for (start = 0; start < end; start++)
			{
				if (!trimTester(s[start])) break;
			}
			if (start >= end) return "";
			return s.Substring(start, end - start);
		}

		/// <summary>
		/// Supprime de la fin de la chaîne tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer le caractère)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static string TrimEnd(this string s, Func<char, bool> trimTester)
		{
			if (s == null) return null;
			int start = 0, end;
			for (end = s.Length - 1; end > start; end--)
			{
				if (!trimTester(s[end])) break;
			}
			if (start >= end) return "";
			return s.Substring(start, end - start + 1);
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un System.String équivalent à la sous-chaîne de longueur length qui commence
		/// à startIndex dans cette instance, ou System.String.Empty si startIndex est
		/// égal à la longueur de cette instance et length est égal à zéro.
		/// </returns>
		public static string Mid( this string s, int start, int length )
		{
			if (s == null) return null;
			if (length < 0)
			{
				if (start > 0 && -length > start)
				{
					length = start + 1;
					start = 0;
				}
				else 
				{
					start += length + 1;
					length = -length;
				}
			}
			if (start < 0) start = s.Length + start;
			if (start <= -length) return string.Empty;
			if (start < 0) return s.Substring(0, length + start);
			if (start > s.Length) return string.Empty;
			if (start + length > s.Length) return s.Substring(start);
			return s.Substring(start, length);
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		public static string Mid( this string s, int start )
		{
			if (s==null) return null;
			if (start < 0) start = s.Length + start;
			if (start < 0) return s;
			if (start > s.Length) return string.Empty;
			return s.Substring(start);
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre au premier caractère et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un System.String équivalent à la sous-chaîne de longueur length qui commence
		/// au premier caractère de cette instance, ou System.String.Empty si startIndex est
		/// égal à la longueur de cette instance et length est égal à zéro.
		/// </returns>
		public static string Left(this string s, int length) 
			=> Mid(s, 0, length);

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre au premier caractère et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un System.String équivalent à la sous-chaîne de longueur length qui contient les caractère de la fin de la chaîne de caractère
		/// pour une logneur équivalente à length
		/// </returns>
		public static string Right( this string s, int length )
		{
			if (s == null) return null;
			if (length > s.Length) return s;
			return s.Substring(s.Length - length);
		}

		/// <summary>
		/// Turn the first letter of a string to uppercase
		/// </summary>
		/// <param name="text">text to transform</param>
		/// <returns></returns>
		public static string FirstLetterUpperCase(this string text) 
			=> text.FirstLetterUpperCase(false);

		/// <summary>
		/// Turn the first letter of a string to uppercase
		/// </summary>
		/// <param name="text">text to transform</param>
		/// <param name="endToLowerCase">True if the end of input string must be set to lowercase</param>
		/// <returns></returns>
		public static string FirstLetterUpperCase( this string text, bool endToLowerCase )
		{
			if (text.IsNullOrEmpty()) {
				return text;
			} else {
				return (text.Mid(0, 1).ToUpper() + (endToLowerCase ? text.Mid(1).ToLower() : text.Mid(1)));
			}
		}

		/// <summary>
		/// Returns true if text is null or empty string
		/// (same as System.String.IsNullOrEmpty(...))
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static bool IsNullOrEmpty(this string text) 
			=> string.IsNullOrEmpty(text);

		/// <summary>
		/// Returns true if text is null or contains only white spaces
		/// (same as System.String.IsNullOrWhiteSpace(...))
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static bool IsNullOrWhiteSpace(this string text) 
			=> string.IsNullOrWhiteSpace(text);

		/// <summary>
		/// Return <paramref name="text"/> if not null and not empty or <paramref name="defaultValue"/>
		/// </summary>
		/// <param name="text"></param>
		/// <param name="defaultValue"></param>
		/// <returns></returns>
		public static string NotNullOrEmptyOrDefault(this string text, string defaultValue) => string.IsNullOrEmpty(text) ? defaultValue : text;

		/// <summary>
		/// Return <paramref name="text"/> if not null and not white space or <paramref name="defaultValue"/>
		/// </summary>
		/// <param name="text"></param>
		/// <param name="defaultValue"></param>
		/// <returns></returns>
		public static string NotNullOrWhiteSpaceOrDefault(this string text, string defaultValue) => string.IsNullOrWhiteSpace(text) ? defaultValue : text;

		/// <summary>
		/// Vérifie si la chaîne représente un nombre
		/// </summary>
		/// <param name="text"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public static bool IsNumber( this string text, NumberFormatInfo format = null )
		{
			format ??= CultureInfo.CurrentCulture.NumberFormat;
			if (text.IsNullOrWhiteSpace()) return false;
			char[] digits = format.NativeDigits.Select(d => d[0]).ToArray();
			text = text.Trim();
			if (text[0] != format.NegativeSign[0] && text[0].NotIn(digits) && text[0] != format.NumberDecimalSeparator[0]) return false;
			bool decimalSeparated = text[0] == format.NumberDecimalSeparator[0];
			for (int i = 1 ; i < text.Length ; i++) {
				if (!decimalSeparated && text[i] == format.NumberDecimalSeparator[0])
				{
					decimalSeparated = true;
					continue;
				}
				if (text[i].In(digits) || text[i] == format.NumberGroupSeparator[0]) continue;
				return false;
			}
			return true;
		}

		/// <summary>
		/// Vérifie si la chaîne représente un nombre
		/// </summary>
		/// <param name="text"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public static bool IsNumber(this string text, CultureInfo culture) 
			=> IsNumber(text, culture.NumberFormat);

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
		public static string TrimBrackets( string str, params Brackets[] brackets )
		{
			if (brackets.IsNullOrEmptyCollection()) {
				brackets = Brackets.All;
			}
			if (str.IsNullOrWhiteSpace()) return "";

			int start = 0, end = str.Length - 1;
			while (str[end]==' ') end--;

			for (int i = 0 ; i < str.Length ; i++) {
				var c = str[i];
				if (c==' ') {
					start = i + 1;
					continue;
				}
				Brackets currentBrackets = brackets.FirstOrDefault(b => b.Open== c);
				if (currentBrackets==null) break;
				if (str[end] == currentBrackets.Close) {
					start = i + 1;
					end--;
				}
				while (str[end]==' ') end--;
			}

			return str.Substring(start, end - start + 1);
		}


		/// <summary>
		/// Supprime les caractères spéciaux de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="keepFunction">fonction qui indique s'il faut garder un caractère particulier</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string PurgeString(this string s, Func<char, bool> keepFunction, char? replacement = null)
		{
			keepFunction.ArgMustNotBeNull();
			if (s == null) return null;
			StringBuilder result = new StringBuilder(s.Length);
			foreach (var c in s)
			{
				if (keepFunction(c))
				{
					result.Append(c);
				}
				else
				{
					result.Append(replacement);
				}
			}
			return result.ToString();

		}

		/// <summary>
		/// Supprime les caractères spéciaux de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="specialChars">Caractères à supprimer</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string RemoveSpecialChars(this string s, string specialChars, char? replacement = null)
			=> s.RemoveSpecialChars(specialChars?.ToCharArray(), replacement);

		/// <summary>
		/// Supprime les caractères spéciaux de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="specialChars">Caractères à supprimer</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string RemoveSpecialChars(this string s, char[] specialChars, char? replacement = null)
		{
			specialChars.ArgMustNotBeNull();
			return s.PurgeString(c => !specialChars.Contains(c), replacement);
		}

		/// <summary>
		/// Conserve les caractères de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="chars">Caractères à conserver</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string KeepOnlyChars(this string s, string chars, char? replacement = null)
			=> s.KeepOnlyChars(chars?.ToCharArray(), replacement);

		/// <summary>
		/// Conserve les caractères de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="chars">Caractères à conserver</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string KeepOnlyChars(this string s, char[] chars, char? replacement = null)
		{
			chars.ArgMustNotBeNull();
			return s.PurgeString(c => chars.Contains(c), replacement);
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
										result.Add(line.Substring(lastindex, i - lastindex + 1).TrimBrackets('\"'));
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
			result.Add(line.Substring(lastindex).TrimBrackets('\"'));
			return result.Where(r => !r.IsNullOrWhiteSpace()).ToArray();
		}
	}

	/// <summary>
	/// Définit une paire de parenthèses
	/// </summary>
	public class Brackets
	{
		public char Open { get; }
		public char Close { get; }

		public Brackets( string brackets ) : this(brackets[0], brackets[1]) { }

		public Brackets( char open, char close )
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

}
