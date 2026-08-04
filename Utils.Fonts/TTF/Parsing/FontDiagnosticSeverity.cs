namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Severity of a <see cref="FontDiagnostic"/>.
/// </summary>
public enum FontDiagnosticSeverity
{
    /// <summary>
    /// The anomaly does not compromise the correctness of the parsed data (e.g. an unusual but
    /// harmless alignment). Recorded for observability only.
    /// </summary>
    Warning,

    /// <summary>
    /// The anomaly indicates the input violates the format contract (e.g. a checksum mismatch, a
    /// duplicate tag, a malformed subtable). In <see cref="FontValidationMode.Strict"/> this always
    /// aborts parsing with a <see cref="FontParseException"/>; in <see cref="FontValidationMode.Permissive"/>
    /// it is recorded and parsing continues only when doing so remains memory-safe.
    /// </summary>
    Error
}
