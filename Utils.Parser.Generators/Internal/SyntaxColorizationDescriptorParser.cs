using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
        if (TryParseWithBootstrap(source, out SyntaxColorizationDescriptor? descriptor) && descriptor != null)
        {
            return descriptor;
        }

        return ParseWithLegacyRules(source);
    }

    /// <summary>
    /// Tries to parse descriptor content using <c>Utils.Parser.Bootstrap.SyntaxColorisationGrammar</c>.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <param name="descriptor">Parsed descriptor when successful.</param>
    /// <returns><see langword="true"/> when the bootstrap parser is available and successful.</returns>
    private static bool TryParseWithBootstrap(string source, out SyntaxColorizationDescriptor? descriptor)
    {
        descriptor = null;

        try
        {
            Assembly? parserAssembly = ResolveParserAssembly();

            if (parserAssembly == null)
            {
                return false;
            }

            Type? grammarType = parserAssembly.GetType("Utils.Parser.Bootstrap.SyntaxColorisationGrammar", throwOnError: false);
            MethodInfo? parseMethod = grammarType?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parseMethod == null)
            {
                return false;
            }

            object? document = parseMethod.Invoke(null, new object[] { source });
            if (document == null)
            {
                return false;
            }

            descriptor = MapBootstrapDocument(document);
            return true;
        }
        catch
        {
            descriptor = null;
            return false;
        }
    }

    /// <summary>
    /// Resolves the <c>Utils.Parser</c> assembly from the current load context or from known probing paths.
    /// </summary>
    /// <returns>The resolved parser assembly, or <see langword="null"/> when unavailable.</returns>
    private static Assembly? ResolveParserAssembly()
    {
        Assembly? loadedAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Utils.Parser", StringComparison.OrdinalIgnoreCase));

        if (loadedAssembly != null)
        {
            return loadedAssembly;
        }

        try
        {
            Type? grammarType = Type.GetType("Utils.Parser.Bootstrap.SyntaxColorisationGrammar, Utils.Parser", throwOnError: false);
            return grammarType?.Assembly;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses descriptor content with a minimal built-in parser when the bootstrap parser is unavailable.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>The parsed descriptor.</returns>
    private static SyntaxColorizationDescriptor ParseWithLegacyRules(string source)
    {
        var descriptor = new SyntaxColorizationDescriptor();
        SyntaxColorizationEntry? currentEntry = null;

        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("@", StringComparison.Ordinal))
            {
                currentEntry = null;
                ParseDirective(trimmed, descriptor);
                continue;
            }

            int sectionSeparatorIndex = trimmed.IndexOf(':');
            if (sectionSeparatorIndex >= 0)
            {
                string classification = Unquote(trimmed.Substring(0, sectionSeparatorIndex).Trim());
                currentEntry = new SyntaxColorizationEntry(classification);
                descriptor.Entries.Add(currentEntry);
                AddRulesFromSegment(trimmed.Substring(sectionSeparatorIndex + 1), currentEntry);
                continue;
            }

            if (currentEntry != null)
            {
                AddRulesFromSegment(trimmed, currentEntry);
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Parses one descriptor directive line.
    /// </summary>
    /// <param name="trimmedDirectiveLine">Trimmed directive line.</param>
    /// <param name="descriptor">Target descriptor.</param>
    private static void ParseDirective(string trimmedDirectiveLine, SyntaxColorizationDescriptor descriptor)
    {
        trimmedDirectiveLine = StripInlineComment(trimmedDirectiveLine).Trim();
        int separatorIndex = trimmedDirectiveLine.IndexOf(':');
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException($"Invalid directive syntax: {trimmedDirectiveLine}");
        }

        string name = trimmedDirectiveLine.Substring(0, separatorIndex).Trim().TrimStart('@');
        string value = Unquote(trimmedDirectiveLine.Substring(separatorIndex + 1).Trim());

        if (name.Equals("FileExtension", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.FileExtensions.Add(value);
            return;
        }

        if (name.Equals("StringSyntaxExtension", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.StringSyntaxExtensions.Add(value);
            return;
        }

        throw new InvalidOperationException($"Unsupported directive '{name}'.");
    }

    /// <summary>
    /// Adds rules parsed from one rule segment (<c>A | B</c>).
    /// </summary>
    /// <param name="segment">Rule segment text.</param>
    /// <param name="entry">Target descriptor entry.</param>
    private static void AddRulesFromSegment(string segment, SyntaxColorizationEntry entry)
    {
        segment = StripInlineComment(segment);
        foreach (string rawRule in segment.Split('|'))
        {
            string rule = Unquote(rawRule.Trim());
            if (rule.Length > 0)
            {
                entry.Rules.Add(rule);
            }
        }
    }

    /// <summary>
    /// Removes surrounding quote characters when present.
    /// </summary>
    /// <param name="value">Raw value text.</param>
    /// <returns>Unquoted value.</returns>
    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    /// <summary>
    /// Removes trailing <c>//</c> or <c>#</c> comments from a descriptor line segment.
    /// </summary>
    /// <param name="value">Raw line segment.</param>
    /// <returns>Segment without trailing comments.</returns>
    private static string StripInlineComment(string value)
    {
        int slashComment = value.IndexOf("//", StringComparison.Ordinal);
        int hashComment = value.IndexOf('#');
        int cutIndex;

        if (slashComment >= 0 && hashComment >= 0)
        {
            cutIndex = Math.Min(slashComment, hashComment);
        }
        else if (slashComment >= 0)
        {
            cutIndex = slashComment;
        }
        else
        {
            cutIndex = hashComment;
        }

        return cutIndex >= 0 ? value.Substring(0, cutIndex) : value;
    }

    /// <summary>
    /// Maps a bootstrap syntax colorisation document to generator descriptor model.
    /// </summary>
    /// <param name="document">Bootstrap parser document instance.</param>
    /// <returns>Mapped descriptor.</returns>
    private static SyntaxColorizationDescriptor MapBootstrapDocument(object document)
    {
        var descriptor = new SyntaxColorizationDescriptor();

        PropertyInfo fileExtensionsProperty = document.GetType().GetProperty("FileExtensions")!;
        PropertyInfo stringSyntaxExtensionsProperty = document.GetType().GetProperty("StringSyntaxExtensions")!;
        PropertyInfo sectionsProperty = document.GetType().GetProperty("Sections")!;

        foreach (string extension in (IEnumerable<string>)fileExtensionsProperty.GetValue(document)!)
        {
            descriptor.FileExtensions.Add(extension);
        }

        foreach (string extension in (IEnumerable<string>)stringSyntaxExtensionsProperty.GetValue(document)!)
        {
            descriptor.StringSyntaxExtensions.Add(extension);
        }

        foreach (object section in (IEnumerable<object>)sectionsProperty.GetValue(document)!)
        {
            PropertyInfo classificationProperty = section.GetType().GetProperty("Classification")!;
            PropertyInfo rulesProperty = section.GetType().GetProperty("Rules")!;

            var entry = new SyntaxColorizationEntry((string)classificationProperty.GetValue(section)!);
            foreach (string rule in (IEnumerable<string>)rulesProperty.GetValue(section)!)
            {
                entry.Rules.Add(rule);
            }

            descriptor.Entries.Add(entry);
        }

        return descriptor;
    }
}
