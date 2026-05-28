using System;

namespace Utils.Parser.Source;

/// <summary>
/// Represents a human-readable location in source code.
/// </summary>
public class SourceCodeLocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCodeLocation"/> class.
    /// </summary>
    /// <param name="filePath">Source file path.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
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
    /// Gets the source file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number.
    /// </summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString() => $"{FilePath}({Line},{Column})";
}
