using System;

namespace Utils.Parser.Source;

/// <summary>
/// Represents a human-readable source-code range for diagnostics, display, and tooling.
/// </summary>
/// <remarks>
/// This contract extends <see cref="SourceCodeLocation" /> with a display/diagnostic length,
/// but intentionally carries no absolute source offset. It must not be treated as equivalent to
/// <see cref="SourceSpan" />, which is a runtime/source-buffer range contract.
/// </remarks>
public sealed class SourceCodeRange : SourceCodeLocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCodeRange"/> class.
    /// </summary>
    /// <param name="filePath">Required source file path.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
    /// <param name="length">Length intended for display or diagnostics for the range.</param>
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
    /// Gets the length intended for display or diagnostics for the range.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc />
    public override string ToString() => $"{FilePath}({Line},{Column},{Length})";
}
