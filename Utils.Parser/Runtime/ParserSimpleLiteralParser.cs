using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Parser.Runtime;

/// <summary>
/// Parses the deliberately limited literal syntax supported by positional parser rule-call binding.
/// This parser does not evaluate expressions and is not a complete C# or ANTLR literal parser.
/// </summary>
public static class ParserSimpleLiteralParser
{
    private static readonly Regex IntegerPattern = new(
        @"^[+-]?\d+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex FloatingPointPattern = new(
        @"^[+-]?(?:(?:\d+\.\d*)|(?:\d*\.\d+)|(?:\d+[eE][+-]?\d+)|(?:\d+\.\d*[eE][+-]?\d+)|(?:\d*\.\d+[eE][+-]?\d+))$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly IReadOnlyDictionary<char, char> EscapeCharacters = new Dictionary<char, char>
    {
        ['\\'] = '\\',
        ['"'] = '"',
        ['\''] = '\'',
        ['n'] = '\n',
        ['r'] = '\r',
        ['t'] = '\t',
        ['0'] = '\0'
    };

    /// <summary>
    /// Attempts to parse a null, Boolean, integer, floating-point, string, or character literal.
    /// Surrounding whitespace is ignored. Integers use <see cref="int"/> when possible and
    /// <see cref="long"/> otherwise; decimal floating-point values use <see cref="double"/>.
    /// </summary>
    /// <param name="rawText">Raw call-site argument text.</param>
    /// <param name="value">Parsed literal value when successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> when <paramref name="rawText"/> is a supported literal.</returns>
    public static bool TryParse(string rawText, out object? value)
    {
        value = null;
        if (rawText is null)
        {
            return false;
        }

        string text = rawText.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text == "null")
        {
            return true;
        }

        if (text == "true")
        {
            value = true;
            return true;
        }

        if (text == "false")
        {
            value = false;
            return true;
        }

        if (IntegerPattern.IsMatch(text))
        {
            if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int integer))
            {
                value = integer;
                return true;
            }

            if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long longInteger))
            {
                value = longInteger;
                return true;
            }

            return false;
        }

        if (FloatingPointPattern.IsMatch(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatingPoint)
            && double.IsFinite(floatingPoint))
        {
            value = floatingPoint;
            return true;
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            return TryDecodeQuotedValue(text, '"', out value);
        }

        if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'')
        {
            if (!TryDecodeQuotedValue(text, '\'', out object? decoded)
                || decoded is not string characterText
                || characterText.Length != 1)
            {
                return false;
            }

            value = characterText[0];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Decodes the small escape set accepted inside a quoted string or character literal.
    /// </summary>
    /// <param name="text">Complete quoted source text.</param>
    /// <param name="quote">Opening and closing quote character.</param>
    /// <param name="value">Decoded string when successful.</param>
    /// <returns><c>true</c> when the quoted value is well formed and uses only supported escapes.</returns>
    private static bool TryDecodeQuotedValue(string text, char quote, out object? value)
    {
        value = null;
        var builder = new StringBuilder(text.Length - 2);
        for (int index = 1; index < text.Length - 1; index++)
        {
            char current = text[index];
            if (current == quote)
            {
                return false;
            }

            if (current != '\\')
            {
                if (current is '\r' or '\n')
                {
                    return false;
                }

                builder.Append(current);
                continue;
            }

            index++;
            if (index >= text.Length - 1 || !EscapeCharacters.TryGetValue(text[index], out char escaped))
            {
                return false;
            }

            builder.Append(escaped);
        }

        value = builder.ToString();
        return true;
    }
}
