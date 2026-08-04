using System;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Immutable set of limits and policy switches that govern how
/// <see cref="TrueTypeFont.ParseFont(System.ReadOnlySpan{byte}, TrueTypeFontParsingOptions)"/> and its
/// overloads read a font. All limits are explicit, bounded, and validated before the corresponding
/// data is allocated or read, so that a hostile font cannot force unbounded memory use or CPU time.
/// </summary>
/// <remarks>
/// The default values are deliberately generous for real-world fonts (which rarely exceed a few
/// megabytes and a few hundred tables/glyph components) while still bounding worst-case resource
/// usage for a parser embedded in a larger process. Callers parsing fonts from a fully untrusted
/// source should consider lowering them further.
/// </remarks>
public sealed record TrueTypeFontParsingOptions
{
    /// <summary>
    /// Gets the shared default options instance, used whenever a parsing overload is called with
    /// a <see langword="null"/> options argument.
    /// </summary>
    public static TrueTypeFontParsingOptions Default { get; } = new();

    /// <summary>
    /// Gets the validation mode: <see cref="FontValidationMode.Strict"/> (default) rejects any
    /// structural anomaly; <see cref="FontValidationMode.Permissive"/> records diagnostics and
    /// continues when safe to do so.
    /// </summary>
    public FontValidationMode ValidationMode { get; init; } = FontValidationMode.Strict;

    /// <summary>
    /// Gets the maximum total size, in bytes, of the font being parsed (whether supplied as a
    /// byte array, a seekable stream, or copied incrementally from a non-seekable stream).
    /// Defaults to 64 MiB. Exceeding this limit always throws, in both validation modes.
    /// </summary>
    public long MaximumFontBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum size, in bytes, accepted for any single table's declared length.
    /// Defaults to 32 MiB. Exceeding this limit always throws, in both validation modes.
    /// </summary>
    public uint MaximumTableBytes { get; init; } = 32U * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of table directory entries accepted. Defaults to 4096, far above
    /// what any real font declares (the OpenType spec allows up to <see cref="ushort.MaxValue"/>
    /// via <c>numTables</c>, but no legitimate font needs more than a few dozen). Exceeding this
    /// limit always throws, in both validation modes.
    /// </summary>
    public ushort MaximumTables { get; init; } = 4096;

    /// <summary>
    /// Gets the maximum number of 'cmap' subtable directory records accepted. Defaults to 1024.
    /// Exceeding this limit always throws, in both validation modes.
    /// </summary>
    public ushort MaximumCmapSubtables { get; init; } = 1024;

    /// <summary>
    /// Gets the maximum number of components a single compound glyph's own component list may
    /// declare (before following references to other compound glyphs). Defaults to 4096.
    /// </summary>
    public int MaximumCompositeComponents { get; init; } = 4096;

    /// <summary>
    /// Gets the maximum number of instruction bytes accepted for a single compound glyph's
    /// trailing instruction block. Defaults to 64 KiB. The declared instruction length is always
    /// read as a <see cref="ushort"/> (so it can never itself request more than 65,535 bytes), but
    /// this limit is checked and enforced independently, before those bytes are read, rather than
    /// relying only on the coarser whole-table <see cref="MaximumTableBytes"/> bound.
    /// </summary>
    public int MaximumCompositeInstructionBytes { get; init; } = 64 * 1024;

    /// <summary>
    /// Gets the maximum recursion depth allowed while resolving compound glyphs that reference
    /// other compound glyphs. Defaults to 64.
    /// </summary>
    public int MaximumCompositeDepth { get; init; } = 64;

    /// <summary>
    /// Gets the maximum number of components allowed while fully expanding a compound glyph's
    /// component graph (shared across the whole expansion, not reset per recursive call).
    /// Defaults to 100,000.
    /// </summary>
    public int MaximumExpandedComponents { get; init; } = 100_000;

    /// <summary>
    /// Gets the maximum number of points allowed while fully expanding a compound glyph's outline
    /// (shared across the whole expansion, not reset per recursive call). Defaults to 2,000,000.
    /// </summary>
    public int MaximumExpandedPoints { get; init; } = 2_000_000;

    /// <summary>
    /// Gets a value indicating whether the caller-supplied stream passed to
    /// <see cref="TrueTypeFont.ParseFont(System.IO.Stream, TrueTypeFontParsingOptions)"/> or
    /// <see cref="TrueTypeFont.ParseFontAsync(System.IO.Stream, TrueTypeFontParsingOptions, System.Threading.CancellationToken)"/>
    /// should be left open after parsing completes (or fails). Defaults to <see langword="true"/>.
    /// Temporary streams created internally to buffer a non-seekable source are always disposed by
    /// the parser regardless of this setting -- it only governs ownership of the stream the caller
    /// passed in.
    /// </summary>
    public bool LeaveOpen { get; init; } = true;

    /// <summary>
    /// Validates this instance's own field values before it governs any parsing, so a
    /// misconfigured caller (a negative limit, or an undefined <see cref="FontValidationMode"/>)
    /// fails fast with a plain <see cref="ArgumentOutOfRangeException"/> naming the offending
    /// property, instead of producing confusing downstream behavior or a misleading
    /// <see cref="FontParseException"/> that looks like a font-data problem.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative, or <see cref="ValidationMode"/> is not a defined value.</exception>
    internal void EnsureValid()
    {
        if (ValidationMode is not (FontValidationMode.Strict or FontValidationMode.Permissive))
        {
            throw new ArgumentOutOfRangeException(nameof(ValidationMode), ValidationMode, "ValidationMode must be a defined FontValidationMode value.");
        }
        if (MaximumFontBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFontBytes), MaximumFontBytes, "MaximumFontBytes must be non-negative.");
        }
        if (MaximumCompositeComponents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCompositeComponents), MaximumCompositeComponents, "MaximumCompositeComponents must be non-negative.");
        }
        if (MaximumCompositeInstructionBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCompositeInstructionBytes), MaximumCompositeInstructionBytes, "MaximumCompositeInstructionBytes must be non-negative.");
        }
        if (MaximumCompositeDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCompositeDepth), MaximumCompositeDepth, "MaximumCompositeDepth must be non-negative.");
        }
        if (MaximumExpandedComponents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumExpandedComponents), MaximumExpandedComponents, "MaximumExpandedComponents must be non-negative.");
        }
        if (MaximumExpandedPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumExpandedPoints), MaximumExpandedPoints, "MaximumExpandedPoints must be non-negative.");
        }
    }
}
