using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Collections;
using Utils.Objects;
using Utils.String;

namespace Utils.String
{
    /// <summary>
    /// Provides extension methods for string manipulation, trimming, prefix/suffix removal, 
    /// wildcard matching, default-value substitutions, etc.
    /// </summary>
    public static class StringExtensions
    {
        #region Like

        /// <summary>
        /// Compares a string against a wildcard pattern (supports '*' and '?').
        /// </summary>
        /// <param name="value">String to compare.</param>
        /// <param name="pattern">
        /// Pattern to match. The '*' wildcard matches any sequence of characters. 
        /// The '?' wildcard matches a single character.
        /// </param>
        /// <param name="ignoreCase">If true, performs a case-insensitive comparison.</param>
        /// <param name="textInfo">
        /// Optional <see cref="TextInfo"/> used for case conversion when <paramref name="ignoreCase"/> is true. 
        /// Defaults to <see cref="CultureInfo.CurrentCulture"/> if not provided.
        /// </param>
        /// <returns>True if the string matches the pattern; otherwise, false.</returns>
        public static bool Like(this string value, string pattern, bool ignoreCase = false, TextInfo textInfo = null)
        {
            value.Arg().MustNotBeNull();
            pattern.Arg().MustNotBeNull();
            return value.AsSpan().Like(pattern, ignoreCase, textInfo);
        }

        /// <summary>
        /// Compares a span of characters against a wildcard pattern (supports '*' and '?').
        /// </summary>
        /// <param name="value">Span of characters to compare.</param>
        /// <param name="pattern">Pattern to match.</param>
        /// <param name="ignoreCase">If true, performs a case-insensitive comparison.</param>
        /// <param name="cultureInfo">
        /// Optional <see cref="CultureInfo"/> used to retrieve <see cref="TextInfo"/> for case conversion 
        /// when <paramref name="ignoreCase"/> is true. Defaults to <see cref="CultureInfo.CurrentCulture"/> if not provided.
        /// </param>
        /// <returns>True if the span matches the pattern; otherwise, false.</returns>
        public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase = false, CultureInfo cultureInfo = null)
        {
            pattern.Arg().MustNotBeNull();
            cultureInfo ??= CultureInfo.CurrentCulture;
            return value.Like(pattern, ignoreCase, cultureInfo.TextInfo);
        }

        /// <summary>
        /// Performs the actual wildcard matching on a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">Span of characters to compare.</param>
        /// <param name="pattern">Pattern to match.</param>
        /// <param name="ignoreCase">If true, performs a case-insensitive comparison.</param>
        /// <param name="textInfo">TextInfo used to handle case conversions if <paramref name="ignoreCase"/> is true.</param>
        /// <returns>True if the span matches the pattern; otherwise, false.</returns>
        public static bool Like(this ReadOnlySpan<char> value, string pattern, bool ignoreCase, TextInfo textInfo)
        {
            pattern.Arg().MustNotBeNull();
            if (pattern == "*") return true;
            textInfo ??= CultureInfo.CurrentCulture.TextInfo;

            Func<char, char, bool> equals = ignoreCase
                ? (x, y) => textInfo.ToLower(x) == textInfo.ToLower(y)
                : (x, y) => x == y;

            int valueIndex = 0, wildcardIndex = 0;
            int valueNext = 0, wildcardNext = 0;

            while (valueIndex < value.Length && wildcardIndex < pattern.Length && pattern[wildcardIndex] != '*')
            {
                if (pattern[wildcardIndex] != '?' && !equals(value[valueIndex], pattern[wildcardIndex]))
                    return false;
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
                        return true; // A trailing '*' matches everything.
                    valueNext += 1;
                }
                else if (pattern[wildcardIndex] == '?' || equals(value[valueIndex], pattern[wildcardIndex]))
                {
                    wildcardIndex++;
                    valueIndex++;
                    if (wildcardIndex >= pattern.Length && valueIndex < value.Length && pattern[wildcardNext] == '*')
                        wildcardIndex = wildcardNext + 1;
                }
                else
                {
                    wildcardIndex = wildcardNext + 1;
                    valueIndex = valueNext++;
                }
            }

            while (wildcardIndex < pattern.Length && pattern[wildcardIndex] == '*')
            {
                wildcardIndex++;
            }

            return wildcardIndex >= pattern.Length && valueIndex >= value.Length;
        }

        #endregion

        #region Trimming

        /// <summary>
        /// Removes from the start and end of the string all characters for which the specified function returns true.
        /// </summary>
        public static string Trim(this string s, Func<char, bool> trimTester)
        {
            s.Arg().MustNotBeNull();
            trimTester.Arg().MustNotBeNull();
            return s.AsSpan().Trim(trimTester).ToString();
        }

        /// <summary>
        /// Removes from the start of the string all characters for which the specified function returns true.
        /// </summary>
        public static string TrimStart(this string s, Func<char, bool> trimTester)
        {
            s.Arg().MustNotBeNull();
            trimTester.Arg().MustNotBeNull();
            return s.AsSpan().TrimStart(trimTester).ToString();
        }

        /// <summary>
        /// Removes from the end of the string all characters for which the specified function returns true.
        /// </summary>
        public static string TrimEnd(this string s, Func<char, bool> trimTester)
        {
            s.Arg().MustNotBeNull();
            trimTester.Arg().MustNotBeNull();
            return s.AsSpan().TrimEnd(trimTester).ToString();
        }

        #endregion

        #region Add Prefix/Suffix
        /// <summary>
        /// Adds the specified prefix to the string if it's not already present.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="prefix">The prefix to add if absent.</param>
        /// <returns>A new string with the prefix present.</returns>
        public static string AddPrefix(this string s, string prefix)
        {
            // If the prefix is null or empty, there's nothing to add.
            if (string.IsNullOrEmpty(prefix))
                return s;
            // If s is null, treat it like an empty string so we can safely add the prefix.
            s ??= string.Empty;
            // Only add the prefix if the string does not already start with it.
            return s.StartsWith(prefix) ? s : prefix + s;
        }

        /// <summary>
        /// Adds the specified suffix to the string if it's not already present.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="suffix">The suffix to add if absent.</param>
        /// <returns>A new string with the suffix present.</returns>
        public static string AddSuffix(this string s, string suffix)
        {
            // If the suffix is null or empty, there's nothing to add.
            if (string.IsNullOrEmpty(suffix))
                return s;
            // If s is null, treat it like an empty string so we can safely add the suffix.
            s ??= string.Empty;
            // Only add the suffix if the string does not already end with it.
            return s.EndsWith(suffix) ? s : s + suffix;
        }

        /// <summary>
        /// Adds the specified prefix to the string if it's not already present,
        /// using the specified <see cref="StringComparison"/> for checking.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="prefix">The prefix to add if absent.</param>
        /// <param name="comparison">The <see cref="StringComparison"/> option to use when checking if <paramref name="s"/> starts with <paramref name="prefix"/>.</param>
        /// <returns>A new string with the prefix present.</returns>
        public static string AddPrefix(this string s, string prefix, StringComparison comparison = StringComparison.Ordinal)
        {
            // If the prefix is null or empty, there's nothing to add.
            if (string.IsNullOrEmpty(prefix))
                return s;

            // If 's' is null, treat it like an empty string so we can safely add the prefix.
            s ??= string.Empty;

            // Only add the prefix if the string does not already start with it,
            // according to the specified comparison.
            return s.StartsWith(prefix, comparison) ? s : prefix + s;
        }

        /// <summary>
        /// Adds the specified suffix to the string if it's not already present,
        /// using the specified <see cref="StringComparison"/> for checking.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="suffix">The suffix to add if absent.</param>
        /// <param name="comparison">The <see cref="StringComparison"/> option to use when checking if <paramref name="s"/> ends with <paramref name="suffix"/>.</param>
        /// <returns>A new string with the suffix present.</returns>
        public static string AddSuffix(this string s, string suffix, StringComparison comparison = StringComparison.Ordinal)
        {
            // If the suffix is null or empty, there's nothing to add.
            if (string.IsNullOrEmpty(suffix))
                return s;

            // If 's' is null, treat it like an empty string so we can safely add the suffix.
            s ??= string.Empty;

            // Only add the suffix if the string does not already end with it,
            // according to the specified comparison.
            return s.EndsWith(suffix, comparison) ? s : s + suffix;
        }

        #endregion

        #region Remove Prefix/Suffix

        /// <summary>
        /// Removes the specified prefix from the string if present.
        /// </summary>
        public static string RemovePrefix(this string s, string prefix)
        {
            if (!s.StartsWith(prefix)) return s;
            return s[prefix.Length..];
        }

        /// <summary>
        /// Removes the specified prefix from the span if present.
        /// </summary>
        public static ReadOnlySpan<char> RemovePrefix(this ReadOnlySpan<char> s, string prefix)
        {
            if (!s.StartsWith(prefix)) return s;
            return s[prefix.Length..];
        }

        /// <summary>
        /// Removes the specified suffix from the string if present.
        /// </summary>
        public static string RemoveSuffix(this string s, string suffix)
        {
            if (!s.EndsWith(suffix)) return s;
            return s.Mid(-suffix.Length);
        }

        /// <summary>
        /// Removes the specified suffix from the span if present.
        /// </summary>
        public static ReadOnlySpan<char> RemoveSuffix(this ReadOnlySpan<char> s, string suffix)
        {
            if (!s.EndsWith(suffix)) return s;
            return s.Mid(-suffix.Length);
        }

        #endregion

        #region Mid / Left / Right

        /// <summary>
        /// Retrieves a substring from this instance. The substring starts at a specified zero-based character position and has a defined length.
        /// </summary>
        public static string Mid(this string s, int start, int length)
        {
            if (s is null) return null;
            return s.AsSpan().Mid(start, length).ToString();
        }

        /// <summary>
        /// Retrieves a substring from this instance starting at a specified zero-based character position until the end of the string.
        /// </summary>
        public static string Mid(this string s, int start)
        {
            if (s is null) return null;
            return s.AsSpan().Mid(start).ToString();
        }

        /// <summary>
        /// Retrieves a substring from the end of this instance. The substring has the specified length.
        /// </summary>
        public static string Right(this string s, int length)
        {
            if (s is null) return null;
            if (length > s.Length) return s;
            // Use s.Mid(-length) to get last 'length' chars
            return s.Mid(-length);
        }

        /// <summary>
        /// Retrieves a substring from the beginning (first character) of this instance, of the specified length.
        /// </summary>
        public static string Left(this string s, int length)
            => s.Mid(0, length);

        #endregion

        #region Case Conversions

        /// <summary>
        /// Turns the first letter of the string to uppercase. Optionally converts the remainder of the string to lowercase.
        /// </summary>
        public static string FirstLetterUpperCase(this string text, bool endToLowerCase = false)
        {
            if (text.IsNullOrEmpty())
                return text;
            return text.Mid(0, 1).ToUpper()
                + (endToLowerCase ? text.Mid(1).ToLower() : text.Mid(1));
        }

        #endregion

        #region Null/Empty/Whitespace Checks

        /// <summary>
        /// Returns true if <paramref name="text"/> is null or an empty string.
        /// (Equivalent to <see cref="string.IsNullOrEmpty(string)"/>).
        /// </summary>
        public static bool IsNullOrEmpty(this string text)
            => string.IsNullOrEmpty(text);

        /// <summary>
        /// Returns true if <paramref name="text"/> is null or consists only of white-space characters.
        /// (Equivalent to <see cref="string.IsNullOrWhiteSpace(string)"/>).
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string text)
            => string.IsNullOrWhiteSpace(text);

        #endregion

        #region Default Value Handling

        /// <summary>
        /// Returns <paramref name="text"/> if it is not null or empty; otherwise returns <paramref name="defaultValue"/>.
        /// </summary>
        public static string ToDefaultIfNullOrEmpty(this string text, string defaultValue)
            => string.IsNullOrEmpty(text) ? defaultValue : text;

        /// <summary>
        /// Returns <paramref name="text"/> if it is not null or whitespace; otherwise returns <paramref name="defaultValue"/>.
        /// </summary>
        public static string ToDefaultIfNullOrWhiteSpace(this string text, string defaultValue)
            => string.IsNullOrWhiteSpace(text) ? defaultValue : text;

        /// <summary>
        /// Returns <paramref name="defaultValue"/> if <paramref name="text"/> is found in <paramref name="candidates"/>; 
        /// otherwise returns <paramref name="text"/>.
        /// </summary>
        public static string ToDefaultIfIn(this string text, IEnumerable<string> candidates, string defaultValue)
        {
            if (text is null) return null;
            if (candidates is null) return text;
            return candidates.Contains(text) ? defaultValue : text;
        }

        /// <summary>
        /// Returns <paramref name="defaultValue"/> if <paramref name="text"/> is *not* found in <paramref name="candidates"/>; 
        /// otherwise returns <paramref name="text"/>.
        /// </summary>
        public static string ToDefaultIfNotIn(this string text, IEnumerable<string> candidates, string defaultValue)
        {
            if (text is null) return null;
            if (candidates is null) return defaultValue;
            return candidates.Contains(text) ? text : defaultValue;
        }

        #endregion

        #region IsNumber

        /// <summary>
        /// Checks if the string represents a number based on the specified <see cref="NumberFormatInfo"/>.
        /// </summary>
        public static bool IsNumber(this string text, NumberFormatInfo format = null)
        {
            format ??= CultureInfo.CurrentCulture.NumberFormat;
            if (text.IsNullOrWhiteSpace()) return false;

            var digits = format.NativeDigits.Select(d => d[0]).ToArray();
            text = text.Trim();

            if (text[0] != format.NegativeSign[0]
                && text[0].NotIn(digits)
                && text[0] != format.NumberDecimalSeparator[0])
                return false;

            var decimalSeparated = text[0] == format.NumberDecimalSeparator[0];
            for (var i = 1; i < text.Length; i++)
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
        /// Checks if the string represents a number based on the specified <see cref="CultureInfo"/>.
        /// </summary>
        public static bool IsNumber(this string text, CultureInfo culture)
            => text.IsNumber(culture.NumberFormat);

        #endregion

        #region Special Characters

        /// <summary>
        /// Removes characters from the string that do not pass the given condition. 
        /// If a character is removed, it is replaced with the specified replacement character (if any).
        /// </summary>
        public static string PurgeString(this string s, Func<char, bool> keepFunction, char? replacement = null)
        {
            keepFunction.Arg().MustNotBeNull();
            if (s is null) return null;

            var result = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (keepFunction(c))
                    result.Append(c);
                else if (replacement.HasValue)
                {
                    result.Append(replacement.Value);
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Removes the specified special characters from the string.
        /// </summary>
        public static string RemoveSpecialChars(this string s, string specialChars, char? replacement = null)
            => s.RemoveSpecialChars(specialChars?.ToCharArray(), replacement);

        /// <summary>
        /// Removes the specified special characters from the string.
        /// </summary>
        public static string RemoveSpecialChars(this string s, char[] specialChars, char? replacement = null)
        {
            specialChars.Arg().MustNotBeNull();
            return s.PurgeString(c => !specialChars.Contains(c), replacement);
        }

        /// <summary>
        /// Keeps only the specified characters in the string, removing all others.
        /// </summary>
        public static string KeepOnlyChars(this string s, string chars, char? replacement = null)
            => s.KeepOnlyChars(chars?.ToCharArray(), replacement);

        /// <summary>
        /// Keeps only the specified characters in the string, removing all others.
        /// </summary>
        public static string KeepOnlyChars(this string s, char[] chars, char? replacement = null)
        {
            chars.Arg().MustNotBeNull();
            return s.PurgeString(c => chars.Contains(c), replacement);
        }

        #endregion

        #region Align

        /// <summary>
        /// Creates a new string with a specified width. 
        /// If <paramref name="length"/> is positive, the original string is right-aligned; 
        /// if negative, it is left-aligned.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <param name="length">The width of the output string. Positive for right alignment, negative for left.</param>
        /// <returns>A new string of the specified width with the original string aligned accordingly.</returns>
        public static string Align(this string s, int length)
        {
            s ??= "";
            if (s.Length >= int.Abs(length)) return s;

            if (length > 0)
                return new string(' ', length - s.Length) + s;

            // length < 0
            return s + new string(' ', 0 - length - s.Length);
        }
        #endregion
    }
}
