using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Collections;

namespace Utils.Objects
{
	public static class StringExtensions
	{
		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase">true : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like(this string value, string pattern, bool ignoreCase = false, TextInfo textInfo = null)
		{
			value.ArgMustNotBeNull();
			pattern.ArgMustNotBeNull();
			return Like(value.AsSpan(), pattern, ignoreCase, textInfo);
		}

		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase"><see cref="true"/> : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase = false, CultureInfo cultureInfo = null)
		{
			pattern.ArgMustNotBeNull();
			return Like(value, pattern, ignoreCase, cultureInfo.TextInfo);
		}


		/// <summary>
		/// Compares a string to an escape pattern.
		/// </summary>
		/// <param name="str">String to compare</param>
		/// <param name="pattern">Pattern to match</param>
		/// <param name="ignoreCase">true: ignore case</param>
		/// <returns>true if the string matches the pattern</returns>
		public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase = false, TextInfo textInfo = null)
		{
			pattern.ArgMustNotBeNull(); // Ensure the pattern is not null.
			if (pattern == "*") return true; // If the pattern is "*", consider it a match.
			textInfo ??= CultureInfo.CurrentCulture.TextInfo; // Get the TextInfo for culture-specific operations.

			// Define a function 'equals' for character comparison based on 'ignoreCase' option.
			Func<char, char, bool> equals = ignoreCase
				? (x, y) => textInfo.ToLower(x) == textInfo.ToLower(y)
				: (x, y) => x == y;

			// Initialize indices for the string and pattern.
			int valueIndex = 0, wildcardIndex = 0;
			int valueNext = 0, wildcardNext = 0;

			// Compare characters until the first '*' in the pattern.
			while (valueIndex < value.Length && wildcardIndex < pattern.Length && pattern[wildcardIndex] != '*')
			{
				if (pattern[wildcardIndex] != '?' && !equals(value[valueIndex], pattern[wildcardIndex]))
				{
					return false; // If a character doesn't match, return false.
				}
				wildcardIndex++;
				valueIndex++;
			}

			// Compare the rest of the string and pattern.
			while (wildcardIndex < pattern.Length && valueIndex < value.Length)
			{
				if (pattern[wildcardIndex] == '*')
				{
					// Handle '*' in the pattern.
					wildcardNext = wildcardIndex;
					wildcardIndex++;
					if (wildcardIndex >= pattern.Length)
					{
						return true; // If '*' is the last character in the pattern, consider it a match.
					}
					valueNext += 1;
				}
				else if (pattern[wildcardIndex] == '?' || equals(value[valueIndex], pattern[wildcardIndex]))
				{
					// If character matches or '?' in pattern, move both indices.
					wildcardIndex++;
					valueIndex++;
					if (wildcardIndex >= pattern.Length && valueIndex < value.Length && pattern[wildcardNext] == '*')
					{
						wildcardIndex = wildcardNext + 1;
					}
				}
				else
				{
					// Mismatch, reset indices to try matching again.
					wildcardIndex = wildcardNext + 1;
					valueIndex = valueNext++;
				}
			}

			// Handle remaining '*' characters in the pattern.
			while (wildcardIndex < pattern.Length && pattern[wildcardIndex] == '*')
			{
				wildcardIndex++;
			}

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
			return s.AsSpan().Trim(trimTester).ToString();
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
			return s.AsSpan().TrimStart(trimTester).ToString();
		}

		/// <summary>
		/// Supprime de la fin de la chaîne tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer le caractère)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static string TrimEnd(this string s, Func<char, bool> trimTester)
		{
			s.ArgMustNotBeNull();
			trimTester.ArgMustNotBeNull();
			return s.AsSpan().TrimEnd(trimTester).ToString();
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
		public static string Mid(this string s, int start, int length)
		{
			if (s is null) return null;
			return s.AsSpan().Mid(start, length).ToString();
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		public static string Mid(this string s, int start)
		{
			if (s == null) return null;
			return s.AsSpan().Mid(start).ToString();
			;
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
		public static string Right(this string s, int length)
		{
			if (s is null) return null;
			if (length > s.Length) return s;
			return s.Substring(s.Length - length);
		}

		/// <summary>
		/// Turn the first letter of a string to uppercase
		/// </summary>
		/// <param name="text">text to transform</param>
		/// <param name="endToLowerCase">True if the end of input string must be set to lowercase</param>
		/// <returns></returns>
		public static string FirstLetterUpperCase(this string text, bool endToLowerCase = false)
		{
			if (text.IsNullOrEmpty())
			{
				return text;
			}
			else
			{
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
		public static bool IsNumber(this string text, NumberFormatInfo format = null)
		{
			format ??= CultureInfo.CurrentCulture.NumberFormat;
			if (text.IsNullOrWhiteSpace()) return false;
			char[] digits = format.NativeDigits.Select(d => d[0]).ToArray();
			text = text.Trim();
			if (text[0] != format.NegativeSign[0] && text[0].NotIn(digits) && text[0] != format.NumberDecimalSeparator[0]) return false;
			bool decimalSeparated = text[0] == format.NumberDecimalSeparator[0];
			for (int i = 1; i < text.Length; i++)
			{
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
		/// Supprime les caractères spéciaux de la chaîne
		/// </summary>
		/// <param name="s">Chaîne à traiter</param>
		/// <param name="keepFunction">fonction qui indique s'il faut garder un caractère particulier</param>
		/// <param name="replacement">Caractère de remplacement</param>
		/// <returns>chaîne expurgée</returns>
		public static string PurgeString(this string s, Func<char, bool> keepFunction, char? replacement = null)
		{
			keepFunction.ArgMustNotBeNull();
			if (s is null) return null;
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
		/// Create a string of <paramref name="length"/> characters where <paramref name="s"/> is align right or left
		/// </summary>
		/// <param name="s">String to be aligned</param>
		/// <param name="width">Width of the string, align right if positive, left if negative</param>
		/// <returns></returns>
		public static string Align(this string s, int length)
		{
			s ??= "";
			if (int.Abs(s.Length) >= int.Abs(length)) return s;
			if (length > 0) return new string(' ', length - s.Length) + s;
			if (length < 0) return s + new string(' ', -length - s.Length);
			return s;
		}

	}
}
