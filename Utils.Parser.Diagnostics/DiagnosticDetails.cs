using System;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Represents immutable content for a parser diagnostic.
/// </summary>
public sealed record DiagnosticDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticDetails"/> record.
    /// </summary>
    /// <param name="descriptor">Diagnostic descriptor.</param>
    /// <param name="message">Resolved diagnostic message.</param>
    /// <param name="ruleName">Optional rule name context.</param>
    /// <param name="exception">Optional related exception.</param>
    public DiagnosticDetails(
        ParserDiagnosticDescriptor descriptor,
        string message,
        string? ruleName = null,
        Exception? exception = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        RuleName = ruleName;
        Exception = exception;
    }

    /// <summary>
    /// Gets the diagnostic descriptor.
    /// </summary>
    public ParserDiagnosticDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the resolved diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional rule name context.
    /// </summary>
    public string? RuleName { get; }

    /// <summary>
    /// Gets the optional related exception.
    /// </summary>
    public Exception? Exception { get; }
}
