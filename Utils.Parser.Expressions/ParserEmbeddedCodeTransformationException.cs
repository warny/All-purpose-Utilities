using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Source;

namespace Utils.Parser.Expressions;

/// <summary>
/// Exception thrown when an embedded-code transformer reports an error before dynamic expression compilation.
/// </summary>
public sealed class ParserEmbeddedCodeTransformationException : Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException
{
    /// <summary>
    /// Initializes a new transformation exception.
    /// </summary>
    /// <param name="message">Transformation diagnostic message.</param>
    public ParserEmbeddedCodeTransformationException(string message)
        : base(message, null, message, ParserEmbeddedCodeLocation.InlineAction, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new transformation exception from the shared structured transformation failure.
    /// </summary>
    /// <param name="message">Stable failure message.</param>
    /// <param name="diagnosticCode">Diagnostic code when available.</param>
    /// <param name="diagnosticMessage">Diagnostic message when available.</param>
    /// <param name="location">Embedded-code location being transformed.</param>
    /// <param name="grammarName">Owning grammar name when available.</param>
    /// <param name="ruleName">Owning rule name when available.</param>
    /// <param name="span">Source span when available.</param>
    /// <param name="innerException">Original transformer exception when available.</param>
    public ParserEmbeddedCodeTransformationException(
        string message,
        string? diagnosticCode,
        string? diagnosticMessage,
        ParserEmbeddedCodeLocation location,
        string? grammarName,
        string? ruleName,
        SourceSpan? span,
        Exception? innerException = null)
        : base(message, diagnosticCode, diagnosticMessage, location, grammarName, ruleName, span, innerException)
    {
    }
}
