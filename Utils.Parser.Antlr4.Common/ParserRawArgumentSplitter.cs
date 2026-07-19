using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Parser.Antlr4.Common;

/// <summary>Provides syntactic top-level splitting of raw rule-call argument text.</summary>
public static class ParserRawArgumentSplitter
{
    /// <summary>Splits raw argument text on top-level commas while preserving nested and quoted commas.</summary>
    public static IReadOnlyList<string> SplitTopLevel(string rawArguments)
    {
        if (rawArguments is null) throw new ArgumentNullException(nameof(rawArguments));
        if (string.IsNullOrWhiteSpace(rawArguments)) return Array.Empty<string>();
        var result = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        char? inQuote = null;
        for (int i = 0; i < rawArguments.Length; i++)
        {
            char c = rawArguments[i];
            if (inQuote is not null)
            {
                if (c == '\\' && i + 1 < rawArguments.Length) { current.Append(c); current.Append(rawArguments[++i]); continue; }
                if (c == inQuote) inQuote = null;
                current.Append(c); continue;
            }
            switch (c)
            {
                case '"': case '\'': inQuote = c; current.Append(c); break;
                case '(': case '[': case '{': depth++; current.Append(c); break;
                case ')': case ']': case '}': if (depth > 0) depth--; current.Append(c); break;
                case ',': if (depth == 0) { result.Add(current.ToString().Trim()); current.Clear(); } else current.Append(c); break;
                default: current.Append(c); break;
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }
}
