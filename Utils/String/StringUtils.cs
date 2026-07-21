using System;
using System.Collections.Generic;
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
    /// Removes the first pair of brackets from the provided string when it matches the supplied opening and closing characters.
    /// </summary>
    /// <param name="str">Source string to inspect.</param>
    /// <param name="openingBracket">Expected opening bracket character.</param>
    /// <param name="closingBracket">Expected closing bracket character.</param>
    /// <returns>The input string without its outermost matching brackets.</returns>
    public static string TrimBrackets(this string str, char openingBracket, char closingBracket)
            => TrimBrackets(str, new Brackets(openingBracket, closingBracket));

    /// <summary>
    /// Removes the first pair of identical brackets from the provided string.
    /// </summary>
    /// <param name="str">Source string to inspect.</param>
    /// <param name="bracket">Bracket character that is used as both opening and closing delimiter.</param>
    /// <returns>The input string without its outermost matching brackets.</returns>
    public static string TrimBrackets(this string str, char bracket)
            => TrimBrackets(str, new Brackets(bracket, bracket));

    /// <summary>
    /// Removes the outermost brackets from the provided string using the supplied bracket definitions.
    /// </summary>
    /// <param name="str">Source string to inspect.</param>
    /// <param name="brackets">Possible bracket definitions that can wrap the string.</param>
    /// <returns>The input string without its outermost matching brackets.</returns>
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
    /// Parses a command-line string into individual arguments using the following grammar:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Tokens are separated by one or more unquoted space characters.</description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       A double-quote character (<c>"</c>) starts a quoted region; content inside is
    ///       included verbatim including spaces. A doubled quote (<c>""</c>) inside a quoted
    ///       region is an escape sequence that yields a single literal <c>"</c> in the result.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>An explicitly quoted empty argument (<c>""</c>) is preserved as an empty string.</description>
    ///   </item>
    ///   <item>
    ///     <description>An unterminated quote is a format error (#57).</description>
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="line">Command-line string to parse.</param>
    /// <returns>Array of individual arguments.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="line"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the command line contains an unterminated quoted string (#57).</exception>
    public static string[] ParseCommandLine(string line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));

        var result = new List<string>();
        var current = new StringBuilder();

        // Whether the current token contains at least one character (including from quoting).
        // This allows "" to produce an empty string argument rather than being silently dropped.
        bool hasToken = false;

        bool inQuote = false;
        int quoteStart = -1;

        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];

            if (inQuote)
            {
                if (c == '"')
                {
                    // Peek ahead: a doubled quote inside a quoted region is an escape for a
                    // literal " character (#58).  The pair is consumed atomically so that the
                    // second quote does not close/reopen the string.
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;  // consume both quotes as one unit
                        continue;
                    }

                    // Single closing quote: end of quoted region.
                    inQuote = false;
                    i++;
                    continue;
                }

                // Any other character is part of the quoted content.
                current.Append(c);
                i++;
            }
            else
            {
                if (c == ' ')
                {
                    // Unquoted space: flush current token (if any) and skip run of spaces.
                    if (hasToken)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }
                    i++;
                }
                else if (c == '"')
                {
                    // Opening quote: mark that we have a token (even if quoted content is empty,
                    // we want to preserve "" as an explicit empty argument).
                    inQuote = true;
                    quoteStart = i;
                    hasToken = true;
                    i++;
                }
                else
                {
                    // Regular unquoted character.
                    current.Append(c);
                    hasToken = true;
                    i++;
                }
            }
        }

        // Unterminated quote is a format error (#57).
        if (inQuote)
            throw new FormatException(
                $"Unterminated quoted string starting at index {quoteStart} in command line: {line}");

        // Flush the last token.
        if (hasToken)
            result.Add(current.ToString());

        return [.. result];
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
    /// Splits a comma-separated string while respecting nested markers such as brackets or braces,
    /// with control over empty entries.
    /// </summary>
    /// <param name="commaSeparatedValues">The string containing comma-separated values.</param>
    /// <param name="commaChar">The character that separates values.</param>
    /// <param name="removeEmptyEntries">True to omit empty results from the output; otherwise false.</param>
    /// <param name="depthMarkerChars">The markers that define nesting boundaries.</param>
    /// <returns>A sequence of values extracted from the string.</returns>
    /// <exception cref="FormatException">
    /// Thrown when a closing marker is encountered without a matching opening marker, or when
    /// one or more opening markers remain unclosed at the end of the string (#59, #60).
    /// </exception>
    public static IEnumerable<string> SplitCommaSeparatedList(this string commaSeparatedValues, char commaChar, bool removeEmptyEntries, params Parenthesis[] depthMarkerChars)
    {
        var lastTypeIndex = 0;
        var depth = new Stack<Parenthesis>();

        for (var i = 0; i < commaSeparatedValues.Length; i++)
        {
            var current = commaSeparatedValues[i];

            // Check for an opening marker.  The full Start token is matched at the current
            // position so that multi-character delimiters are handled correctly (#60).
            Parenthesis opener = null;
            foreach (var m in depthMarkerChars)
            {
                if (i + m.Start.Length <= commaSeparatedValues.Length &&
                    commaSeparatedValues.AsSpan(i, m.Start.Length).Equals(m.Start, StringComparison.Ordinal))
                {
                    opener = m;
                    break;
                }
            }

            if (opener != null)
            {
                // For symmetric markers (Start == End) that are already open, treat the current
                // occurrence as a closing marker rather than re-opening a new level.
                if (depth.Count > 0 && opener.Start == opener.End && depth.Peek().Start == opener.Start)
                {
                    depth.Pop();
                }
                else
                {
                    depth.Push(opener);
                }
                i += opener.Start.Length - 1; // advance past the full token (loop will add 1)
                continue;
            }

            // Check for a closing marker.  Full End token is matched (#60).
            Parenthesis closer = null;
            foreach (var m in depthMarkerChars)
            {
                // Skip symmetric markers here — they are handled as openers above.
                if (m.Start == m.End) continue;

                if (i + m.End.Length <= commaSeparatedValues.Length &&
                    commaSeparatedValues.AsSpan(i, m.End.Length).Equals(m.End, StringComparison.Ordinal))
                {
                    closer = m;
                    break;
                }
            }

            if (closer != null)
            {
                // Guard against stack underflow (#59).
                if (depth.Count == 0)
                    throw new FormatException(
                        $"Unexpected closing marker '{closer.End}' at index {i} without a matching opening marker.");

                Parenthesis top = depth.Pop();

                // Verify that the closing marker matches the innermost opening marker (#60).
                if (!string.Equals(top.End, closer.End, StringComparison.Ordinal))
                    throw new FormatException(
                        $"Mismatched markers: expected closing '{top.End}' but found '{closer.End}' at index {i}.");

                i += closer.End.Length - 1;
                continue;
            }

            if (current == commaChar && depth.Count == 0)
            {
                var value = commaSeparatedValues[lastTypeIndex..i];
                if (!removeEmptyEntries || !string.IsNullOrEmpty(value)) yield return value;
                lastTypeIndex = i + 1;
            }
        }

        // Reject unclosed markers (#59).
        if (depth.Count > 0)
        {
            var unclosed = depth.Pop();
            throw new FormatException(
                $"Unclosed marker '{unclosed.Start}' was not closed before the end of the string.");
        }

        var lastValue = commaSeparatedValues[lastTypeIndex..];
        if (!removeEmptyEntries || !string.IsNullOrEmpty(lastValue)) yield return lastValue;
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
    /// <param name="brackets">
    /// A string containing exactly two characters: the opening and the closing bracket.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="brackets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="brackets"/> does not have exactly two characters (#61).</exception>
    public Brackets(string brackets)
    {
        if (brackets is null) throw new ArgumentNullException(nameof(brackets));
        if (brackets.Length != 2)
            throw new ArgumentException(
                $"A bracket string must contain exactly two characters (opening and closing), but got length {brackets.Length}.",
                nameof(brackets));
        this.Open = brackets[0];
        this.Close = brackets[1];
    }

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
    /// Gets a read-only snapshot of all built-in bracket pairs.
    /// Each call returns a fresh defensive copy so callers cannot mutate the global state (#61).
    /// </summary>
    public static Brackets[] All => [RoundBrackets, SquareBrackets, Braces];

    /// <summary>
    /// Returns a string that represents the bracket pair.
    /// </summary>
    /// <returns>A string describing the bracket pair.</returns>
    public override string ToString() => $" {Open} ... {Close} ";
}
