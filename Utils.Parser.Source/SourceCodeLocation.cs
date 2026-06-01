using System;

namespace Utils.Parser.Source;

/// <summary>
/// Represents a human-readable source-code location for diagnostics, display, and tooling.
/// </summary>
/// <remarks>
/// This contract requires a file path and 1-based line/column coordinates, but intentionally
/// carries no absolute source offset. It must not be treated as equivalent to
/// <see cref="SourceLocation" />, which is a runtime/source-buffer coordinate contract.
/// </remarks>
public class SourceCodeLocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCodeLocation"/> class.
    /// </summary>
    /// <param name="filePath">Required source file path for diagnostics, display, or tooling.</param>
    /// <param name="line">1-based human-readable line number.</param>
    /// <param name="column">1-based human-readable column number.</param>
    public SourceCodeLocation(string filePath, int line, int column)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null, empty, or whitespace.", nameof(filePath));
        }

        if (line <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line must be strictly positive.");
        }

        if (column <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be strictly positive.");
        }

        FilePath = filePath;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the required source file path for diagnostics, display, or tooling.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the 1-based human-readable line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based human-readable column number.
    /// </summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString() => $"{FilePath}({Line},{Column})";
}
