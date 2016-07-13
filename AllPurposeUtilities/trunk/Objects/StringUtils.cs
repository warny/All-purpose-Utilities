using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Utils.Mathematics;

namespace Utils.Objects
{
	public static class StringUtils
	{
		/// <summary>
		/// Compare une chaîne par rapport à une séquence d'échappement
		/// </summary>
		/// <param name="str">Chaîne à comparer</param>
		/// <param name="pattern">Séquence</param>
		/// <param name="ignoreCase">true : ignore la casse</param>
		/// <returns>true si la chaîne correspond</returns>
		public static bool Like( this string str, string pattern, bool ignoreCase )
		{
			return LikeOperator.LikeString(str, pattern, ignoreCase ? CompareMethod.Text : CompareMethod.Binary);
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
		/// <param name="String">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un System.String équivalent à la sous-chaîne de longueur length qui commence
		/// au premier caractère de cette instance, ou System.String.Empty si startIndex est
		/// égal à la longueur de cette instance et length est égal à zéro.
		/// </returns>
		public static string Left( this string String, int length )
		{
			return Mid(String, 0, length);
		}

		/// <summary>
		/// Récupère une sous-chaîne de cette instance. La sous-chaîne démarre au premier caractère et a une longueur définie.
		/// </summary>
		/// <param name="String">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un System.String équivalent à la sous-chaîne de longueur length qui contient les caractère de la fin de la chaîne de caractère
		/// pour une logneur équivalente à length
		/// </returns>
		public static string Right( this string String, int length )
		{
			if (length > String.Length) return String;
			return String.Substring(String.Length - length);
		}

		/// <summary>
		/// Turn the first letter of a string to uppercase
		/// </summary>
		/// <param name="text">text to transform</param>
		/// <returns></returns>
		public static string FirstLetterUpperCase( this string text )
		{
			return text.FirstLetterUpperCase(false);
		}

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
		public static bool IsNullOrEmpty( this string text )
		{
			return string.IsNullOrEmpty(text);
		}

		/// <summary>
		/// Returns true if text is null or contains only white spaces
		/// (same as System.String.IsNullOrWhiteSpace(...))
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static bool IsNullOrWhiteSpace( this string text )
		{
			return string.IsNullOrWhiteSpace(text);
		}

		/// <summary>
		/// Vérifie si la chaîne représente un nombre
		/// </summary>
		/// <param name="text"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public static bool IsNumber( this string text, System.Globalization.NumberFormatInfo format = null )
		{
			format = format ?? System.Globalization.CultureInfo.CurrentCulture.NumberFormat;
			for (int i = 0 ; i < text.Length ; i++) {
				if (i==0) {
					if (text[i]==format.NegativeSign[0]) continue;
				}
				if (text[i].ToString().In(format.NativeDigits)) continue;
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
		public static bool IsNumber( this string text, System.Globalization.CultureInfo culture )
		{
			return IsNumber(text, culture.NumberFormat);
		}

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

		private static Brackets roundBrackets = new Brackets('(', ')');
		private static Brackets squareBrackets = new Brackets('(', ')');
		private static Brackets braces = new Brackets('(', ')');
		public static  Brackets[] all = new[] { roundBrackets, squareBrackets, braces };

		public static Brackets RoundBrackets => roundBrackets;
		public static Brackets SquareBrackets => squareBrackets;
		public static Brackets Braces => braces;

		public static Brackets[] All => all;
	}
}
