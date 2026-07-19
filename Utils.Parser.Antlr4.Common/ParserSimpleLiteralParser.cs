using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Parses the narrow simple-literal subset supported by parser rule-call binding.
/// </summary>
public static class ParserSimpleLiteralParser
{
    private static readonly Regex IntegerPattern = new Regex(@"^[+-]?\d+$", RegexOptions.Compiled);
    private static readonly Regex FloatingPointPattern = new Regex(@"^[+-]?(?:\d+\.\d*|\d*\.\d+|\d+[eE][+-]?\d+|(?:\d+\.\d*|\d*\.\d+)[eE][+-]?\d+)$", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<char, char> EscapeCharacters = new Dictionary<char, char>
    {
        ['\\'] = '\\', ['\''] = '\'', ['"'] = '"', ['n'] = '\n', ['r'] = '\r', ['t'] = '\t', ['b'] = '\b', ['f'] = '\f', ['0'] = '\0',
    };

    /// <summary>Attempts to parse a supported literal without evaluating expressions.</summary>
    public static bool TryParse(string rawText, out object? value)
    {
        if (rawText is null) throw new ArgumentNullException(nameof(rawText));
        value = null;
        string text = rawText.Trim();
        if (text.Length == 0) return false;
        if (text == "null") return true;
        if (text == "true") { value = true; return true; }
        if (text == "false") { value = false; return true; }
        if (IntegerPattern.IsMatch(text))
        {
            if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i)) { value = i; return true; }
            if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) { value = l; return true; }
            return false;
        }
        if (FloatingPointPattern.IsMatch(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && (!double.IsNaN(d) && !double.IsInfinity(d))) { value = d; return true; }
        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') return TryDecodeQuotedValue(text, '"', out value);
        if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
        {
            if (!TryDecodeQuotedValue(text, '\'', out object? decoded) || decoded is not string s || s.Length != 1) return false;
            value = s[0]; return true;
        }
        return false;
    }

    /// <summary>Decodes supported quoted-string escapes.</summary>
    private static bool TryDecodeQuotedValue(string text, char quote, out object? value)
    {
        value = null;
        var builder = new StringBuilder(text.Length - 2);
        for (int index = 1; index < text.Length - 1; index++)
        {
            char current = text[index];
            if (current == quote) return false;
            if (current != '\\')
            {
                if (current == '\r' || current == '\n') return false;
                builder.Append(current); continue;
            }
            index++;
            if (index >= text.Length - 1 || !EscapeCharacters.TryGetValue(text[index], out char escaped)) return false;
            builder.Append(escaped);
        }
        value = builder.ToString();
        return true;
    }
}
