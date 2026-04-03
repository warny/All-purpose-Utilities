using System;
using System.IO;
using Utils.Parser.Bootstrap;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Parses syntax colorization descriptor files.
/// </summary>
public sealed class SyntaxColorizationDescriptorFileParser
{
    /// <summary>
    /// Parses a descriptor file from disk.
    /// </summary>
    /// <param name="filePath">Path of the descriptor file to parse.</param>
    /// <returns>The parsed descriptor.</returns>
    public SyntaxColorizationDescriptor ParseFile(string filePath)
    {
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
