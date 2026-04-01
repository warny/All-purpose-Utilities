using System.Collections.Generic;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Represents a syntax colorization descriptor declared in a text file.
/// </summary>
internal sealed class SyntaxColorizationDescriptor
{
    /// <summary>
    /// Gets the file extensions declared by the descriptor.
    /// </summary>
    public List<string> FileExtensions { get; } = new List<string>();

    /// <summary>
    /// Gets the StringSyntax names declared by the descriptor.
    /// </summary>
    public List<string> StringSyntaxExtensions { get; } = new List<string>();

    /// <summary>
    /// Gets the mapping between classification names and grammar rules.
    /// </summary>
    public List<SyntaxColorizationEntry> Entries { get; } = new List<SyntaxColorizationEntry>();
}

/// <summary>
/// Represents a single classification section in a syntax colorization descriptor.
/// </summary>
internal sealed class SyntaxColorizationEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorizationEntry"/> class.
    /// </summary>
    /// <param name="classificationName">Classification name that should be returned.</param>
    public SyntaxColorizationEntry(string classificationName)
    {
        ClassificationName = classificationName;
    }

    /// <summary>
    /// Gets the classification name.
    /// </summary>
    public string ClassificationName { get; }

    /// <summary>
    /// Gets the grammar rules mapped to the classification.
    /// </summary>
    public List<string> Rules { get; } = new List<string>();
}
