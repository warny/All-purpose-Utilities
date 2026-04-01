using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Parses syntax colorization descriptor files.
/// </summary>
internal static class SyntaxColorizationDescriptorParser
{

    /// <summary>
    /// Parses descriptor text content.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>The parsed descriptor model.</returns>
    public static SyntaxColorizationDescriptor Parse(string source)
    {
        var descriptor = new SyntaxColorizationDescriptor();
        SyntaxColorizationEntry currentEntry = null;

        var lines = source.Replace("\r\n", "\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = RemoveComments(lines[index]).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("@", StringComparison.Ordinal))
            {
                ParseDirective(descriptor, line, index + 1);
                currentEntry = null;
                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal))
            {
                currentEntry = new SyntaxColorizationEntry(TrimTypeName(line.Substring(0, line.Length - 1)));
                descriptor.Entries.Add(currentEntry);
                continue;
            }

            if (currentEntry == null)
            {
                throw new InvalidOperationException($"Line {index + 1}: rule list found before a section header.");
            }

            foreach (string rule in ParseRules(line))
            {
                currentEntry.Rules.Add(rule);
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Parses a descriptor directive line and updates the descriptor.
    /// </summary>
    /// <param name="descriptor">Descriptor to update.</param>
    /// <param name="line">Directive line.</param>
    /// <param name="lineNumber">Current line number.</param>
    private static void ParseDirective(SyntaxColorizationDescriptor descriptor, string line, int lineNumber)
    {
        int separatorIndex = line.IndexOf(':');
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException($"Line {lineNumber}: malformed directive '{line}'.");
        }

        string directive = line.Substring(0, separatorIndex).Trim();
        string value = TrimQuoted(line.Substring(separatorIndex + 1).Trim());

        if (directive.Equals("@FileExtension", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.FileExtensions.Add(value);
            return;
        }

        if (directive.Equals("@StringSyntaxExtension", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.StringSyntaxExtensions.Add(value);
            return;
        }

        throw new InvalidOperationException($"Line {lineNumber}: unsupported directive '{directive}'.");
    }


    /// <summary>
    /// Removes line comments (<c>#</c> and <c>//</c>) while preserving quoted text.
    /// </summary>
    /// <param name="line">Input line.</param>
    /// <returns>Line content without trailing comment.</returns>
    private static string RemoveComments(string line)
    {
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (current == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes)
            {
                if (current == '#')
                {
                    return line.Substring(0, index);
                }

                if (current == '/' && index + 1 < line.Length && line[index + 1] == '/')
                {
                    return line.Substring(0, index);
                }
            }
        }

        return line;
    }

    /// <summary>
    /// Parses a pipe-separated list of rules.
    /// </summary>
    /// <param name="line">Source line.</param>
    /// <returns>Normalized rule names.</returns>
    private static IEnumerable<string> ParseRules(string line)
    {
        return line
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(rule => rule.Trim())
            .Where(rule => !string.IsNullOrWhiteSpace(rule));
    }

    /// <summary>
    /// Trims optional quotes around directive values.
    /// </summary>
    /// <param name="value">Raw directive value.</param>
    /// <returns>Unquoted value.</returns>
    private static string TrimQuoted(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    /// <summary>
    /// Trims section type names and removes optional quotes.
    /// </summary>
    /// <param name="value">Section type value.</param>
    /// <returns>Normalized classification name.</returns>
    private static string TrimTypeName(string value)
    {
        return TrimQuoted(value.Trim());
    }
}
