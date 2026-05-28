using System;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Represents a zero-based source span for a diagnostic.
/// </summary>
public sealed record DiagnosticSpan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticSpan"/> record.
    /// </summary>
    /// <param name="start">Zero-based source start index.</param>
    /// <param name="length">Source span length.</param>
    public DiagnosticSpan(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start must be greater than or equal to zero.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than or equal to zero.");
        }

        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the zero-based source start index.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the source span length.
    /// </summary>
    public int Length { get; }
}
