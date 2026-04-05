using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Utils.Parser.Bootstrap;

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
    public SyntaxColorizationDescriptor ParseContent([StringSyntax("SyntaxColorisation")] string content)
    {
        SyntaxColorisationDocument parsedDocument = SyntaxColorisationGrammar.Parse(content);
        var descriptor = new SyntaxColorizationDescriptor();

        descriptor.FileExtensions.AddRange(parsedDocument.FileExtensions);
        descriptor.StringSyntaxExtensions.AddRange(parsedDocument.StringSyntaxExtensions);

        foreach (SyntaxColorisationSection section in parsedDocument.Sections)
        {
            var entry = new SyntaxColorizationDescriptorEntry(section.Classification);
            entry.Rules.AddRange(section.Rules);
            descriptor.Entries.Add(entry);
        }

        return descriptor;
    }
}
