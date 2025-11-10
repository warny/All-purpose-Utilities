using System;

namespace Utils.Data.Sql;

/// <summary>
/// Provides formatting configuration for SQL reconstruction.
/// </summary>
public sealed class SqlFormattingOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlFormattingOptions"/> class.
    /// </summary>
    /// <param name="mode">The formatting mode to apply when rebuilding SQL.</param>
    /// <param name="indentSize">The number of spaces used per indentation level.</param>
    public SqlFormattingOptions(SqlFormattingMode mode = SqlFormattingMode.Inline, int indentSize = 4)
    {
        if (indentSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize), indentSize, "Indent size must be greater than or equal to zero.");
        }

        Mode = mode;
        IndentSize = indentSize;
    }

    /// <summary>
    /// Gets the default formatting options (inline mode with an indent size of four spaces).
    /// </summary>
    public static SqlFormattingOptions Default { get; } = new SqlFormattingOptions();

    /// <summary>
    /// Gets the formatting mode used for reconstruction.
    /// </summary>
    public SqlFormattingMode Mode { get; }

    /// <summary>
    /// Gets the number of spaces used per indentation level.
    /// </summary>
    public int IndentSize { get; }
}
