using System;
using System.Collections.Generic;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides syntactic top-level splitting of raw rule-call argument text into named key–value pairs,
/// supporting forms such as <c>value: 42</c> and <c>value = 42</c>.
/// Splitting is purely syntactic: no value is evaluated, no parameter is bound.
/// </summary>
public static class ParserRawNamedArgumentSplitter
{
    /// <summary>
    /// Splits raw rule-call argument text into a name–value dictionary at top-level separators.
    /// </summary>
    /// <param name="rawArguments">
    /// Raw argument text without outer delimiters, as stored in
    /// <see cref="ParserRuleCallResult.RawArguments"/>.
    /// </param>
    /// <param name="separatorMode">
    /// Which separator characters to recognise. Defaults to <see cref="ParserRawNamedArgumentSeparatorMode.ColonOrEquals"/>.
    /// </param>
    /// <returns>
    /// A dictionary mapping trimmed argument names to trimmed raw value text.
    /// Returns an empty dictionary when <paramref name="rawArguments"/> is empty or whitespace-only.
    /// When the same name appears more than once, the last entry wins.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawArguments"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">
    /// Thrown when any top-level slice has no separator, or when a separator is found but the key is empty.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Uses <see cref="ParserRawArgumentSplitter.SplitTopLevel"/> to split on top-level commas first,
    /// so commas inside nested expressions or quoted strings are correctly ignored.
    /// </para>
    /// <para>
    /// Separators (<c>:</c> or <c>=</c>) inside nested parentheses, brackets, braces, or quoted strings
    /// are not treated as the key–value separator.
    /// </para>
    /// <para>
    /// No value is evaluated and no type is inferred. <c>"hello"</c> stays as <c>"hello"</c> in the output.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, string> SplitNamedTopLevel(
        string rawArguments,
        ParserRawNamedArgumentSeparatorMode separatorMode = ParserRawNamedArgumentSeparatorMode.ColonOrEquals)
    {
        ArgumentNullException.ThrowIfNull(rawArguments);

        if (string.IsNullOrWhiteSpace(rawArguments))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var slices = ParserRawArgumentSplitter.SplitTopLevel(rawArguments);
        var result = new Dictionary<string, string>(slices.Count, StringComparer.Ordinal);

        foreach (var slice in slices)
        {
            int sepIdx = FindTopLevelSeparator(slice, separatorMode);
            if (sepIdx < 0)
                throw new FormatException(
                    $"Named argument slice '{slice}' has no separator. " +
                    $"Expected ':' or '=' at the top level (mode: {separatorMode}).");

            string key = slice[..sepIdx].Trim();
            string value = slice[(sepIdx + 1)..].Trim();

            if (key.Length == 0)
                throw new FormatException(
                    $"Named argument slice '{slice}' has an empty key.");

            result[key] = value; // duplicate keys: last wins
        }

        return result;
    }

    private static int FindTopLevelSeparator(string slice, ParserRawNamedArgumentSeparatorMode mode)
    {
        int depth = 0;
        char? inQuote = null;

        for (int i = 0; i < slice.Length; i++)
        {
            char c = slice[i];

            if (inQuote is not null)
            {
                if (c == '\\' && i + 1 < slice.Length) { i++; continue; }
                if (c == inQuote) inQuote = null;
                continue;
            }

            switch (c)
            {
                case '"': case '\'': inQuote = c; break;
                case '(': case '[': case '{': depth++; break;
                case ')': case ']': case '}': if (depth > 0) depth--; break;
                default:
                    if (depth == 0)
                    {
                        bool matchColon  = c == ':' && mode != ParserRawNamedArgumentSeparatorMode.EqualsOnly;
                        bool matchEquals = c == '=' && mode != ParserRawNamedArgumentSeparatorMode.ColonOnly;
                        if (matchColon || matchEquals) return i;
                    }
                    break;
            }
        }

        return -1;
    }
}
