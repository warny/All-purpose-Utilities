using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Utils.Parser.Bootstrap;

/// <summary>
/// Represents one classification section in a syntax colorisation descriptor.
/// </summary>
public sealed class SyntaxColorisationSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorisationSection"/> class.
    /// </summary>
    /// <param name="classification">Section classification name.</param>
    public SyntaxColorisationSection(string classification)
    {
        Classification = classification;
    }

    /// <summary>
    /// Gets the classification name.
    /// </summary>
    public string Classification { get; }

    /// <summary>
    /// Gets descriptor rules associated with the classification.
    /// </summary>
    public List<string> Rules { get; } = new();
}

/// <summary>
/// Represents a parsed syntax colorisation descriptor document.
/// </summary>
public sealed class SyntaxColorisationDocument
{
    /// <summary>
    /// Gets declared file extensions.
    /// </summary>
    public List<string> FileExtensions { get; } = new();

    /// <summary>
    /// Gets declared StringSyntax extensions.
    /// </summary>
    public List<string> StringSyntaxExtensions { get; } = new();

    /// <summary>
    /// Gets declared classification sections.
    /// </summary>
    public List<SyntaxColorisationSection> Sections { get; } = new();
}

/// <summary>
/// Parses <c>.syntaxcolor</c> descriptor content used by source generators and Visual Studio integration.
/// </summary>
public static class SyntaxColorisationGrammar
{
    /// <summary>
    /// Parses descriptor text from a file.
    /// </summary>
    /// <param name="filePath">Descriptor file path.</param>
    /// <returns>Parsed descriptor document.</returns>
    public static SyntaxColorisationDocument ParseFile(string filePath)
    {
        string content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// Parses descriptor text content.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>Parsed descriptor document.</returns>
    public static SyntaxColorisationDocument Parse([StringSyntax("SyntaxColorisation")] string source)
    {
        var descriptor = new SyntaxColorisationDocument();
        SyntaxColorisationSection? currentSection = null;

        string[] lines = source.Replace("\r\n", "\n").Split('\n');
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
                currentSection = null;
                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal))
            {
                string classification = TrimQuoted(line[..^1].Trim());
                currentSection = new SyntaxColorisationSection(classification);
                descriptor.Sections.Add(currentSection);
                continue;
            }

            if (currentSection == null)
            {
                throw new InvalidOperationException($"Line {index + 1}: rule list found before a section header.");
            }

            foreach (string rule in ParseRules(line))
            {
                currentSection.Rules.Add(rule);
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Parses one descriptor directive line.
    /// </summary>
    /// <param name="document">Descriptor document to update.</param>
    /// <param name="line">Directive line text.</param>
    /// <param name="lineNumber">Current source line number.</param>
    private static void ParseDirective(SyntaxColorisationDocument document, string line, int lineNumber)
    {
        int separatorIndex = line.IndexOf(':');
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException($"Line {lineNumber}: malformed directive '{line}'.");
        }

        string directive = line[..separatorIndex].Trim();
        string value = TrimQuoted(line[(separatorIndex + 1)..].Trim());

        if (directive.Equals("@FileExtension", StringComparison.OrdinalIgnoreCase))
        {
            document.FileExtensions.Add(value);
            return;
        }

        if (directive.Equals("@StringSyntaxExtension", StringComparison.OrdinalIgnoreCase))
        {
            document.StringSyntaxExtensions.Add(value);
            return;
        }

        throw new InvalidOperationException($"Line {lineNumber}: unsupported directive '{directive}'.");
    }

    /// <summary>
    /// Removes line comments while preserving quoted text.
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
                    return line[..index];
                }

                if (current == '/' && index + 1 < line.Length && line[index + 1] == '/')
                {
                    return line[..index];
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
    /// Trims optional quotes around a value.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Unquoted value.</returns>
    private static string TrimQuoted(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        return value;
    }
}
