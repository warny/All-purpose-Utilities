using System;
using System.IO;

namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Thrown when a TrueType/OpenType font fails to parse under
/// <see cref="FontValidationMode.Strict"/>, or when a resource limit configured on
/// <see cref="TrueTypeFontParsingOptions"/> is exceeded (which is fatal in both validation modes).
/// </summary>
/// <remarks>
/// Derives from <see cref="IOException"/> rather than <see cref="InvalidDataException"/>: this
/// target framework's <see cref="InvalidDataException"/> is sealed.
/// </remarks>
public sealed class FontParseException : IOException
{
    /// <summary>
    /// Gets the structured diagnostic describing the failure.
    /// </summary>
    public FontDiagnostic Diagnostic { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontParseException"/> class from a diagnostic.
    /// </summary>
    /// <param name="diagnostic">The diagnostic describing the failure.</param>
    public FontParseException(FontDiagnostic diagnostic)
        : base(diagnostic?.Message ?? throw new ArgumentNullException(nameof(diagnostic)))
    {
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontParseException"/> class from a diagnostic
    /// and an inner exception.
    /// </summary>
    /// <param name="diagnostic">The diagnostic describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public FontParseException(FontDiagnostic diagnostic, Exception innerException)
        : base(diagnostic?.Message ?? throw new ArgumentNullException(nameof(diagnostic)), innerException)
    {
        Diagnostic = diagnostic;
    }
}
