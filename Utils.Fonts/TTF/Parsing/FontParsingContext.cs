using System.Collections.Generic;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Carries the active <see cref="TrueTypeFontParsingOptions"/> and accumulated
/// <see cref="FontDiagnostic"/> list through a single font parse. Reachable from a parsed
/// <see cref="TrueTypeFont"/> instance (via <see cref="TrueTypeFont.ParsingContext"/>) so that
/// nested table/glyph parsers can apply the same strict/permissive policy and resource limits
/// without threading an extra parameter through every <c>ReadData</c> override.
/// </summary>
internal sealed class FontParsingContext
{
    private readonly List<FontDiagnostic> diagnostics = [];

    /// <summary>
    /// Initializes a new parsing context for the given options.
    /// </summary>
    /// <param name="options">The options governing this parse.</param>
    public FontParsingContext(TrueTypeFontParsingOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Gets the options governing this parse.
    /// </summary>
    public TrueTypeFontParsingOptions Options { get; }

    /// <summary>
    /// Gets the diagnostics accumulated so far.
    /// </summary>
    public IReadOnlyList<FontDiagnostic> Diagnostics => diagnostics;

    /// <summary>
    /// Reports a policy-level anomaly (duplicate tag, checksum mismatch, malformed subtable,
    /// alignment, ...). In <see cref="FontValidationMode.Strict"/> this always throws a
    /// <see cref="FontParseException"/>; in <see cref="FontValidationMode.Permissive"/> the
    /// diagnostic is recorded and the caller is expected to continue in a safe, well-defined way.
    /// </summary>
    public void ReportError(FontDiagnosticCode code, string message, Tag? tag = null, long? offset = null, long? length = null)
    {
        var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, message, tag, offset, length);
        if (Options.ValidationMode == FontValidationMode.Strict)
        {
            throw new FontParseException(diagnostic);
        }
        diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Records a warning-level anomaly. Never throws, in either validation mode.
    /// </summary>
    public void ReportWarning(FontDiagnosticCode code, string message, Tag? tag = null, long? offset = null, long? length = null)
        => diagnostics.Add(new FontDiagnostic(code, FontDiagnosticSeverity.Warning, message, tag, offset, length));

    /// <summary>
    /// Reports a memory-safety, range, or allocation-limit violation. Always throws a
    /// <see cref="FontParseException"/>, regardless of <see cref="FontValidationMode"/> -- these
    /// anomalies are never safe to continue past.
    /// </summary>
    public static void Reject(FontDiagnosticCode code, string message, Tag? tag = null, long? offset = null, long? length = null)
        => throw new FontParseException(new FontDiagnostic(code, FontDiagnosticSeverity.Error, message, tag, offset, length));
}
