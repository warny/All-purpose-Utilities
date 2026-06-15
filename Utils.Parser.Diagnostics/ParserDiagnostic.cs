using System;
using Utils.Parser.Source;

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
        Details = new DiagnosticDetails(
            descriptor ?? throw new ArgumentNullException(nameof(descriptor)),
            message ?? throw new ArgumentNullException(nameof(message)),
            ruleName,
            exception);
        Span = CreateSpan(spanStart, spanLength);
    }

    /// <summary>
    /// Initializes a new diagnostic instance.
    /// </summary>
    /// <param name="details">Diagnostic details.</param>
    /// <param name="span">Optional source span.</param>
    /// <param name="location">Optional source location.</param>
    public ParserDiagnostic(
        DiagnosticDetails details,
        DiagnosticSpan? span = null,
        SourceCodeLocation? location = null)
    {
        Details = details ?? throw new ArgumentNullException(nameof(details));
        Span = span;
        Location = location;
    }

    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    public DiagnosticDetails Details { get; }

    /// <summary>
    /// Gets the optional source span.
    /// </summary>
    public DiagnosticSpan? Span { get; }

    /// <summary>
    /// Gets the optional human-readable source location.
    /// </summary>
    public SourceCodeLocation? Location { get; }

    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    public ParserDiagnosticDescriptor Descriptor => Details.Descriptor;

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
    public string Message => Details.Message;


    /// <summary>
    /// Gets the optional rule name context.
    /// </summary>
    public string? RuleName => Details.RuleName;

    /// <summary>
    /// Gets the optional related exception.
    /// </summary>
    public Exception? Exception => Details.Exception;

    /// <summary>
    /// Formats the diagnostic in file/line/column style.
    /// </summary>
    public string ToDisplayString()
    {
        if (Location is null)
        {
            return $"{Code}: {Message}";
        }

        return $"{Location}: {Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
    }

    /// <summary>
    /// Creates a diagnostic span from legacy nullable start and length values.
    /// </summary>
    /// <param name="spanStart">Optional source span start position.</param>
    /// <param name="spanLength">Optional source span length.</param>
    /// <returns>The composed diagnostic span, or <see langword="null"/> when no span was provided.</returns>
    private static DiagnosticSpan? CreateSpan(int? spanStart, int? spanLength)
    {
        if (spanStart.HasValue != spanLength.HasValue)
        {
            throw new ArgumentException("spanStart and spanLength must both be provided or both be null.");
        }

        return spanStart.HasValue
            ? new DiagnosticSpan(spanStart.Value, spanLength!.Value)
            : null;
    }
}
