using System;
using System.Collections.Generic;
using Utils.Parser.Source;

namespace Utils.Parser.Diagnostics.EmbeddedCode;

/// <summary>
/// Represents raw embedded target-language code exactly as it was read from the grammar.
/// </summary>
public sealed class RawEmbeddedCode
{
    /// <summary>
    /// Initializes a new raw embedded-code value.
    /// </summary>
    /// <param name="text">Raw embedded-code text without ANTLR delimiters.</param>
    public RawEmbeddedCode(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the raw embedded-code text without ANTLR delimiters.</summary>
    public string Text { get; }

    /// <summary>Returns the raw embedded-code text for diagnostics and transformer input only.</summary>
    public override string ToString() => Text;
}

/// <summary>
/// Represents embedded target-language code produced by <see cref="ParserEmbeddedCodeTransformationService"/>.
/// </summary>
public sealed class TransformedEmbeddedCode
{
    /// <summary>
    /// Initializes a transformed embedded-code value after a transformer has run and diagnostics have been validated.
    /// </summary>
    /// <param name="text">Transformed embedded-code text ready for generated emission or expression compilation.</param>
    /// <param name="diagnostics">Non-blocking diagnostics produced by the transformer.</param>
    internal TransformedEmbeddedCode(string text, IReadOnlyList<ParserEmbeddedCodeDiagnostic> diagnostics)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>Gets the transformed embedded-code text ready for generated emission or expression compilation.</summary>
    public string Text { get; }

    /// <summary>Gets non-blocking diagnostics reported by the transformer.</summary>
    public IReadOnlyList<ParserEmbeddedCodeDiagnostic> Diagnostics { get; }

    /// <summary>Returns the transformed embedded-code text for final emission or compilation.</summary>
    public override string ToString() => Text;
}

/// <summary>
/// Describes the caller path that requested an embedded-code transformation.
/// </summary>
public enum ParserEmbeddedCodeTransformationPath
{
    /// <summary>The transformation is used by generated-source emission.</summary>
    GeneratedCodeEmission,

    /// <summary>The transformation is used by runtime expression compilation.</summary>
    RuntimeCompilation
}

/// <summary>
/// Contains structured failure metadata used by the central transformation service.
/// </summary>
public sealed class ParserEmbeddedCodeTransformationFailureContext
{
    /// <summary>Gets the path that requested the transformation.</summary>
    public ParserEmbeddedCodeTransformationPath Path { get; set; }

    /// <summary>Gets the embedded-code location being transformed.</summary>
    public ParserEmbeddedCodeLocation Location { get; set; }

    /// <summary>Gets the owning grammar name when available.</summary>
    public string? GrammarName { get; set; }

    /// <summary>Gets the owning parser rule name when available.</summary>
    public string? RuleName { get; set; }

    /// <summary>Gets the source span associated with the embedded code when available.</summary>
    public SourceSpan? Span { get; set; }
}

/// <summary>
/// Exception thrown by the central embedded-code transformation service when transformation fails.
/// </summary>
public class ParserEmbeddedCodeTransformationException : Exception
{
    /// <summary>
    /// Initializes a new transformation exception with structured diagnostic metadata.
    /// </summary>
    /// <param name="message">Stable failure message.</param>
    /// <param name="diagnosticCode">Diagnostic code when one is available.</param>
    /// <param name="diagnosticMessage">Diagnostic message when one is available.</param>
    /// <param name="path">Transformation path that requested the embedded-code transformation.</param>
    /// <param name="location">Embedded-code location being transformed.</param>
    /// <param name="grammarName">Owning grammar name when available.</param>
    /// <param name="ruleName">Owning parser rule name when available.</param>
    /// <param name="span">Source span when available.</param>
    /// <param name="innerException">Original transformer exception when transformation failed by throwing.</param>
    public ParserEmbeddedCodeTransformationException(
        string message,
        string? diagnosticCode,
        string? diagnosticMessage,
        ParserEmbeddedCodeTransformationPath path,
        ParserEmbeddedCodeLocation location,
        string? grammarName,
        string? ruleName,
        SourceSpan? span,
        Exception? innerException = null)
        : base(message, innerException)
    {
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        Path = path;
        Location = location;
        GrammarName = grammarName;
        RuleName = ruleName;
        Span = span;
    }

    /// <summary>Gets the diagnostic code when one is available.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the diagnostic message when one is available.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets the transformation path that requested the embedded-code transformation.</summary>
    public ParserEmbeddedCodeTransformationPath Path { get; }

    /// <summary>Gets the embedded-code location being transformed.</summary>
    public ParserEmbeddedCodeLocation Location { get; }

    /// <summary>Gets the owning grammar name when available.</summary>
    public string? GrammarName { get; }

    /// <summary>Gets the owning rule name when available.</summary>
    public string? RuleName { get; }

    /// <summary>Gets the source span when available.</summary>
    public SourceSpan? Span { get; }
}

/// <summary>
/// Executes embedded-code transformations and materializes validated transformed-code values.
/// </summary>
public static class ParserEmbeddedCodeTransformationService
{
    /// <summary>
    /// Runs a transformer exactly once for one raw embedded-code fragment and returns a typed transformed-code value.
    /// </summary>
    /// <param name="transformer">Transformer selected by the caller.</param>
    /// <param name="rawCode">Raw embedded-code value read from the grammar.</param>
    /// <param name="context">Transformation context containing passive grammar metadata.</param>
    /// <param name="failureContext">Structured failure metadata for deterministic errors.</param>
    /// <returns>A transformed embedded-code value produced from the transformer result.</returns>
    public static TransformedEmbeddedCode TransformOrThrow(
        IParserEmbeddedCodeTransformer transformer,
        RawEmbeddedCode rawCode,
        ParserEmbeddedCodeTransformationContext context,
        ParserEmbeddedCodeTransformationFailureContext failureContext)
    {
        if (transformer is null) throw new ArgumentNullException(nameof(transformer));
        if (rawCode is null) throw new ArgumentNullException(nameof(rawCode));
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (failureContext is null) throw new ArgumentNullException(nameof(failureContext));

        context.Code = rawCode.Text;
        ParserEmbeddedCodeTransformationResult result;
        try
        {
            result = transformer.Transform(context);
        }
        catch (Exception exception)
        {
            throw CreateException("Embedded-code transformer threw an exception.", null, null, failureContext, exception);
        }

        if (result is null)
        {
            throw CreateException("Embedded-code transformer returned null.", null, null, failureContext, null);
        }

        IReadOnlyList<ParserEmbeddedCodeDiagnostic> diagnostics = result.Diagnostics ?? Array.Empty<ParserEmbeddedCodeDiagnostic>();
        ParserEmbeddedCodeDiagnostic? error = FindFirstError(diagnostics);
        if (error is not null)
        {
            string diagnosticMessage = string.IsNullOrWhiteSpace(error.Message) ? "Embedded-code transformer reported an error." : error.Message;
            string message = FormatDiagnosticMessage(error.Code, diagnosticMessage);
            throw CreateException(message, error.Code, diagnosticMessage, failureContext, null, error.Span);
        }

        if (result.Code is null)
        {
            throw CreateException("Embedded-code transformer returned null code.", null, null, failureContext, null);
        }

        return new TransformedEmbeddedCode(result.Code, CopyNonNullDiagnostics(diagnostics));
    }

    /// <summary>
    /// Finds the first error diagnostic while ignoring null diagnostic entries defensively.
    /// </summary>
    /// <param name="diagnostics">Diagnostics returned by the transformer.</param>
    /// <returns>The first blocking diagnostic, or <c>null</c>.</returns>
    private static ParserEmbeddedCodeDiagnostic? FindFirstError(IReadOnlyList<ParserEmbeddedCodeDiagnostic> diagnostics)
    {
        foreach (ParserEmbeddedCodeDiagnostic? diagnostic in diagnostics)
        {
            if (diagnostic?.Severity == ParserEmbeddedCodeDiagnosticSeverity.Error)
            {
                return diagnostic;
            }
        }

        return null;
    }

    /// <summary>
    /// Copies diagnostics that are safe to expose on the transformed-code value.
    /// </summary>
    /// <param name="diagnostics">Diagnostics returned by the transformer.</param>
    /// <returns>Non-null diagnostics preserving warnings and informational entries.</returns>
    private static IReadOnlyList<ParserEmbeddedCodeDiagnostic> CopyNonNullDiagnostics(IReadOnlyList<ParserEmbeddedCodeDiagnostic> diagnostics)
    {
        var copy = new List<ParserEmbeddedCodeDiagnostic>(diagnostics.Count);
        foreach (ParserEmbeddedCodeDiagnostic? diagnostic in diagnostics)
        {
            if (diagnostic is not null)
            {
                copy.Add(diagnostic);
            }
        }

        return copy;
    }

    /// <summary>
    /// Creates a deterministic transformation exception from structured failure metadata.
    /// </summary>
    /// <param name="message">Stable exception message.</param>
    /// <param name="diagnosticCode">Diagnostic code when available.</param>
    /// <param name="diagnosticMessage">Diagnostic message when available.</param>
    /// <param name="failureContext">Failure metadata supplied by the caller.</param>
    /// <param name="innerException">Original exception when available.</param>
    /// <param name="diagnosticSpan">Diagnostic-specific span when available.</param>
    /// <returns>A structured transformation exception.</returns>
    private static ParserEmbeddedCodeTransformationException CreateException(
        string message,
        string? diagnosticCode,
        string? diagnosticMessage,
        ParserEmbeddedCodeTransformationFailureContext failureContext,
        Exception? innerException,
        SourceSpan? diagnosticSpan = null)
    {
        return new ParserEmbeddedCodeTransformationException(
            message,
            diagnosticCode,
            diagnosticMessage,
            failureContext.Path,
            failureContext.Location,
            failureContext.GrammarName,
            failureContext.RuleName,
            diagnosticSpan ?? failureContext.Span,
            innerException);
    }

    /// <summary>
    /// Formats a blocking diagnostic message with a stable code prefix when one is available.
    /// </summary>
    /// <param name="diagnosticCode">Diagnostic code supplied by the transformer.</param>
    /// <param name="diagnosticMessage">Diagnostic message supplied by the transformer.</param>
    /// <returns>Message used for generated and runtime transformation failures.</returns>
    private static string FormatDiagnosticMessage(string? diagnosticCode, string diagnosticMessage)
    {
        return string.IsNullOrWhiteSpace(diagnosticCode)
            ? diagnosticMessage
            : $"{diagnosticCode}: {diagnosticMessage}";
    }
}
