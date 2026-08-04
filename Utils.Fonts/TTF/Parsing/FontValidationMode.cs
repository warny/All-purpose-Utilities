namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Selects how a TrueType/OpenType parser reacts to structural anomalies found while
/// reading a font. See <see cref="TrueTypeFontParsingOptions.ValidationMode"/>.
/// </summary>
public enum FontValidationMode
{
    /// <summary>
    /// Reject any structural anomaly (invalid fields, out-of-range offsets, checksum mismatches,
    /// duplicate tags, overlaps, malformed subtables, glyph cycles, etc.) by throwing a
    /// <see cref="FontParseException"/>. This is the default and the recommended mode for
    /// untrusted input.
    /// </summary>
    Strict,

    /// <summary>
    /// Record anomalies as structured <see cref="FontDiagnostic"/> entries on the parsed font
    /// instead of throwing, and continue parsing whenever doing so does not require reading
    /// outside a validated range, allocating beyond a configured limit, or otherwise leaving the
    /// parser in an unsafe state. Memory-safety, range, and allocation-limit violations always
    /// throw regardless of this setting -- permissive mode only relaxes policy-level checks
    /// (duplicate tags, checksum mismatches, malformed cmap subtables, alignment, ...).
    /// </summary>
    Permissive
}
