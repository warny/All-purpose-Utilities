using System.Collections.Generic;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Represents a syntax colorization descriptor loaded from a descriptor file.
/// </summary>
public sealed class SyntaxColorizationDescriptor
{
    private readonly List<string> fileExtensions = [];
    private readonly List<string> stringSyntaxExtensions = [];
    private readonly List<SyntaxColorizationDescriptorEntry> entries = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorizationDescriptor"/> class.
    /// </summary>
    public SyntaxColorizationDescriptor()
    {
        FileExtensions = fileExtensions.AsReadOnly();
        StringSyntaxExtensions = stringSyntaxExtensions.AsReadOnly();
        Entries = entries.AsReadOnly();
    }

    /// <summary>
    /// Gets the file extensions declared by the descriptor.
    /// </summary>
    public IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Gets the StringSyntax extension names declared by the descriptor.
    /// </summary>
    public IReadOnlyList<string> StringSyntaxExtensions { get; }

    /// <summary>
    /// Gets the mapping entries for classifications and rule names.
    /// </summary>
    public IReadOnlyList<SyntaxColorizationDescriptorEntry> Entries { get; }

    /// <summary>
    /// Adds one file extension to the descriptor.
    /// </summary>
    /// <param name="fileExtension">File extension to add.</param>
    internal void AddFileExtension(string fileExtension)
    {
        fileExtensions.Add(fileExtension);
    }

    /// <summary>
    /// Adds one StringSyntax extension to the descriptor.
    /// </summary>
    /// <param name="stringSyntaxExtension">StringSyntax extension to add.</param>
    internal void AddStringSyntaxExtension(string stringSyntaxExtension)
    {
        stringSyntaxExtensions.Add(stringSyntaxExtension);
    }

    /// <summary>
    /// Adds one entry to the descriptor.
    /// </summary>
    /// <param name="entry">Entry to add.</param>
    internal void AddEntry(SyntaxColorizationDescriptorEntry entry)
    {
        entries.Add(entry);
    }
}

/// <summary>
/// Represents one section of a syntax colorization descriptor.
/// </summary>
public sealed class SyntaxColorizationDescriptorEntry
{
    private readonly List<string> rules = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorizationDescriptorEntry"/> class.
    /// </summary>
    /// <param name="classification">Classification name associated with this section.</param>
    public SyntaxColorizationDescriptorEntry(string classification)
    {
        Classification = classification;
        Rules = rules.AsReadOnly();
    }

    /// <summary>
    /// Gets the classification name.
    /// </summary>
    public string Classification { get; }

    /// <summary>
    /// Gets the rule names included in this section.
    /// </summary>
    public IReadOnlyList<string> Rules { get; }

    /// <summary>
    /// Adds one rule name to this section.
    /// </summary>
    /// <param name="rule">Rule name to add.</param>
    internal void AddRule(string rule)
    {
        rules.Add(rule);
    }
}
