using System.Collections.Generic;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Represents a syntax colorization descriptor loaded from a descriptor file.
/// </summary>
public sealed class SyntaxColorizationDescriptor
{
    /// <summary>
    /// Gets the file extensions declared by the descriptor.
    /// </summary>
    public List<string> FileExtensions { get; } = new List<string>();

    /// <summary>
    /// Gets the StringSyntax extension names declared by the descriptor.
    /// </summary>
    public List<string> StringSyntaxExtensions { get; } = new List<string>();

    /// <summary>
    /// Gets the mapping entries for classifications and rule names.
    /// </summary>
    public List<SyntaxColorizationDescriptorEntry> Entries { get; } = new List<SyntaxColorizationDescriptorEntry>();
}

/// <summary>
/// Represents one section of a syntax colorization descriptor.
/// </summary>
public sealed class SyntaxColorizationDescriptorEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorizationDescriptorEntry"/> class.
    /// </summary>
    /// <param name="classification">Classification name associated with this section.</param>
    public SyntaxColorizationDescriptorEntry(string classification)
    {
        Classification = classification;
    }

    /// <summary>
    /// Gets the classification name.
    /// </summary>
    public string Classification { get; }

    /// <summary>
    /// Gets the rule names included in this section.
    /// </summary>
    public List<string> Rules { get; } = new List<string>();
}
