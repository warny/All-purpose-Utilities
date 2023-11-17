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
		public static string Mid( this string s, int start, int length )
		{
			if (s is null) return null;
			return s.AsSpan().Mid(start, length).ToString();
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		public static string Mid( this string s, int start )
		{
			if (s==null) return null;
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
		public static string Right( this string s, int length )
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
		public static string FirstLetterUpperCase( this string text, bool endToLowerCase = false)
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
