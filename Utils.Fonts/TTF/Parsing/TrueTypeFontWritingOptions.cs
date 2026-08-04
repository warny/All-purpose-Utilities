using System;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Immutable set of limits and policy switches that govern how
/// <see cref="TrueTypeFont.WriteFont()"/> and its stream-based overloads serialize a font.
/// </summary>
public sealed record TrueTypeFontWritingOptions
{
    /// <summary>
    /// Gets the shared default options instance, used whenever a writing overload is called with
    /// a <see langword="null"/> options argument.
    /// </summary>
    public static TrueTypeFontWritingOptions Default { get; } = new();

    /// <summary>
    /// Gets the maximum total size, in bytes, accepted for the serialized font. Defaults to
    /// 64 MiB. Exceeding this limit throws before any output is produced.
    /// </summary>
    public long MaximumOutputBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets a value indicating whether the whole in-memory model (table lengths, glyph lengths,
    /// 'loca' offsets, directory layout) is fully validated before any byte is written. Defaults
    /// to <see langword="true"/>. When enabled, a validation failure never leaves a caller with a
    /// partially written array; when writing directly to a caller-supplied <see cref="System.IO.Stream"/>,
    /// note that a failure partway through can still leave that stream with a partial, invalid font
    /// written to it -- this option only guarantees atomicity for the in-memory layout computation,
    /// not for I/O against the destination stream itself.
    /// </summary>
    public bool ValidateBeforeWrite { get; init; } = true;

    /// <summary>Validates this instance's own field values before it governs any write.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MaximumOutputBytes"/> is negative.</exception>
    internal void EnsureValid()
    {
        if (MaximumOutputBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumOutputBytes), MaximumOutputBytes, "MaximumOutputBytes must be non-negative.");
        }
    }
}
