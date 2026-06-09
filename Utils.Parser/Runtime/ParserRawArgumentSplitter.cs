using System;
using System.Collections.Generic;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides syntactic top-level splitting of raw rule-call argument text preserved from
/// <c>callee[...]</c> grammar clauses.
/// Splitting is purely syntactic: no argument is evaluated, no parameter is bound, and no seed is set.
/// </summary>
public static class ParserRawArgumentSplitter
{
    /// <summary>
    /// Splits raw rule-call argument text into top-level argument slices at commas,
    /// respecting nested parentheses, square brackets, braces, and quoted strings.
    /// </summary>
    /// <param name="rawArguments">
    /// Raw argument text without the outer <c>[</c> and <c>]</c> delimiters,
    /// as stored in <see cref="ParserRuleCallResult.RawArguments"/>.
    /// </param>
    /// <returns>
    /// A read-only list of trimmed argument slices. Returns an empty list when
    /// <paramref name="rawArguments"/> is empty or contains only whitespace.
    /// Empty segments (e.g. from <c>a,,b</c> or a trailing comma) are preserved.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawArguments"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>Nesting is tracked for <c>()</c>, <c>[]</c>, and <c>{}</c>. Angle brackets <c>&lt;&gt;</c>
    /// are not tracked to avoid misinterpreting comparison operators.</para>
    /// <para>Quoted strings (<c>"..."</c> and <c>'...'</c>) are respected; backslash escapes
    /// (<c>\"</c>, <c>\'</c>, <c>\\</c>) inside strings are recognized. Verbatim C# strings
    /// (<c>@"..."</c>) are not supported.</para>
    /// <para>Unbalanced brackets or quotes are handled conservatively: the remainder of the input
    /// is absorbed into the current segment without throwing.</para>
    /// <para>This method does not evaluate expressions, infer types, or bind arguments to parameters.</para>
    /// </remarks>
    public static IReadOnlyList<string> SplitTopLevel(string rawArguments)
    {
        ArgumentNullException.ThrowIfNull(rawArguments);

        if (string.IsNullOrWhiteSpace(rawArguments))
            return [];

        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int depth = 0;
        char? inQuote = null;
        int i = 0;

        while (i < rawArguments.Length)
        {
            char c = rawArguments[i];

            if (inQuote is not null)
            {
                // Inside a quoted string: look for backslash escapes and closing quote.
                if (c == '\\' && i + 1 < rawArguments.Length)
                {
                    current.Append(c);
                    current.Append(rawArguments[++i]);
                    i++;
                    continue;
                }

                if (c == inQuote)
                    inQuote = null;

                current.Append(c);
                i++;
                continue;
            }

            // Not inside a quoted string.
            switch (c)
            {
                case '"':
                case '\'':
                    inQuote = c;
                    current.Append(c);
                    break;

                case '(':
                case '[':
                case '{':
                    depth++;
                    current.Append(c);
                    break;

                case ')':
                case ']':
                case '}':
                    if (depth > 0) depth--;
                    current.Append(c);
                    break;

                case ',':
                    if (depth == 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                    break;

                default:
                    current.Append(c);
                    break;
            }

            i++;
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}
