using System;

namespace Utils.Parser.Source;

/// <summary>
/// Represents a human-readable source location with a required file path and a length component.
/// </summary>
public sealed class SourceCodeRange : SourceCodeLocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCodeRange"/> class.
    /// </summary>
    /// <param name="filePath">Required source file path.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
    /// <param name="length">Number of display characters covered by the range.</param>
    public SourceCodeRange(string filePath, int line, int column, int length)
        : base(filePath, line, column)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than or equal to zero.");
        }

        Length = length;
    }

    /// <summary>
    /// Gets the number of display characters covered by the range.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc />
    public override string ToString() => $"{FilePath}({Line},{Column},{Length})";
}
