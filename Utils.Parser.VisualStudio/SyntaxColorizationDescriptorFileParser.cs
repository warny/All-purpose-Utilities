using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Parses syntax colorization descriptor files.
/// </summary>
public sealed class SyntaxColorizationDescriptorFileParser
{
    /// <summary>Maximum size of a descriptor file that will be parsed. Files larger than this are rejected.</summary>
    public const long MaxFileSizeBytes = 1 * 1024 * 1024; // 1 MB

    /// <summary>
    /// Parses a descriptor file from disk.
    /// </summary>
    /// <param name="filePath">Path of the descriptor file to parse.</param>
    /// <returns>The parsed descriptor.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file exceeds <see cref="MaxFileSizeBytes"/>.
    /// </exception>
    public SyntaxColorizationDescriptor ParseFile(string filePath)
    {
        var info = new FileInfo(filePath);
        if (info.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Descriptor file '{filePath}' is {info.Length:N0} bytes, which exceeds the " +
                $"{MaxFileSizeBytes / 1024:N0} KB limit. The file will not be loaded.");
        }

        string content = File.ReadAllText(filePath);
        return ParseContent(content);
    }

    /// <summary>
    /// Parses descriptor content from text.
    /// </summary>
    /// <param name="content">Descriptor content.</param>
    /// <returns>The parsed descriptor.</returns>
    public SyntaxColorizationDescriptor ParseContent(string content)
    {
        var descriptor = new SyntaxColorizationDescriptor();
        SyntaxColorizationDescriptorEntry? currentEntry = null;
        using var reader = new StringReader(content);
        int lineNumber = 0;
        string? rawLine;
        while ((rawLine = reader.ReadLine()) != null)
        {
            lineNumber++;
            string line = RemoveComments(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("@", StringComparison.Ordinal))
            {
                ParseDirective(descriptor, line, lineNumber);
                currentEntry = null;
                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal))
            {
                string classification = TrimQuoted(line.Substring(0, line.Length - 1).Trim());
                currentEntry = new SyntaxColorizationDescriptorEntry(classification);
                descriptor.Entries.Add(currentEntry);
                continue;
            }

            if (currentEntry == null)
            {
                throw new InvalidOperationException($"Line {lineNumber}: expected a section before declaring rules.");
            }

            foreach (string rule in SplitRules(line))
            {
                currentEntry.Rules.Add(rule);
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Parses one directive and updates the target descriptor.
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
    /// Splits a rule line by the <c>|</c> separator.
    /// </summary>
    /// <param name="line">Rule line content.</param>
    /// <returns>Normalized rule names.</returns>
    private static IEnumerable<string> SplitRules(string line)
    {
        return line
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));
    }

    /// <summary>
    /// Removes leading and trailing quotes when present.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Unquoted value.</returns>
    private static string TrimQuoted(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }
}
