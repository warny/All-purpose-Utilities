using System;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Represents one emitted diagnostic message instance.
/// </summary>
public sealed class ParserDiagnostic
{
    /// <summary>
    /// Initializes a new diagnostic instance.
    /// </summary>
    /// <param name="descriptor">Diagnostic descriptor.</param>
    /// <param name="message">Resolved diagnostic message.</param>
    /// <param name="spanStart">Optional source span start position.</param>
    /// <param name="spanLength">Optional source span length.</param>
    /// <param name="ruleName">Optional rule name context.</param>
    /// <param name="exception">Optional related exception.</param>
    public ParserDiagnostic(
        ParserDiagnosticDescriptor descriptor,
        string message,
        int? spanStart = null,
        int? spanLength = null,
        string? ruleName = null,
        Exception? exception = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        SpanStart = spanStart;
        SpanLength = spanLength;
        RuleName = ruleName;
        Exception = exception;
    }

    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    public ParserDiagnosticDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the diagnostic code.
    /// </summary>
    public string Code => Descriptor.Code;

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public DiagnosticSeverity Severity => Descriptor.Severity;

    /// <summary>
    /// Gets the resolved message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional source span start position.
    /// </summary>
    public int? SpanStart { get; }

    /// <summary>
    /// Gets the optional source span length.
    /// </summary>
    public int? SpanLength { get; }

    /// <summary>
    /// Gets the optional rule name context.
    /// </summary>
    public string? RuleName { get; }

    /// <summary>
    /// Gets the optional related exception.
    /// </summary>
    public Exception? Exception { get; }
}
