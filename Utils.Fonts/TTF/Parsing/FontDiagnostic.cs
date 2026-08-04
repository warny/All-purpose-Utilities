namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Describes a single structural anomaly found while parsing a TrueType/OpenType font.
/// </summary>
/// <param name="Code">The kind of anomaly.</param>
/// <param name="Severity">Whether the anomaly is informational or a format-contract violation.</param>
/// <param name="Message">A human-readable description, including the concrete values involved.</param>
/// <param name="TableTag">The table the anomaly relates to, if applicable.</param>
/// <param name="Offset">The byte offset (relative to the start of the font) the anomaly relates to, if applicable.</param>
/// <param name="Length">The byte length the anomaly relates to, if applicable.</param>
public sealed record FontDiagnostic(
    FontDiagnosticCode Code,
    FontDiagnosticSeverity Severity,
    string Message,
    Tag? TableTag = null,
    long? Offset = null,
    long? Length = null)
{
    /// <inheritdoc/>
    public override string ToString() =>
        TableTag is { } tag
            ? $"[{Severity}] {Code} ({tag}): {Message}"
            : $"[{Severity}] {Code}: {Message}";
}
