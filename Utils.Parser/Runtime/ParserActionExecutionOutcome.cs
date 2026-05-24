using Utils.Parser.Diagnostics;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a parser action execution outcome with optional diagnostic metadata.
/// </summary>
public sealed record ParserActionExecutionOutcome
{
    /// <summary>
    /// Initializes a new parser action execution outcome.
    /// </summary>
    /// <param name="status">Execution status.</param>
    /// <param name="diagnostic">Optional diagnostic descriptor to emit from parser runtime.</param>
    /// <param name="exception">Optional exception associated with the execution attempt.</param>
    /// <param name="diagnosticArguments">Optional diagnostic formatting arguments.</param>
    public ParserActionExecutionOutcome(
        ParserActionExecutionStatus status,
        ParserDiagnosticDescriptor? diagnostic = null,
        Exception? exception = null,
        IReadOnlyList<object?>? diagnosticArguments = null)
    {
        Status = status;
        Diagnostic = diagnostic;
        Exception = exception;
        DiagnosticArguments = diagnosticArguments ?? [];
    }

    /// <summary>
    /// Gets the execution status.
    /// </summary>
    public ParserActionExecutionStatus Status { get; }

    /// <summary>
    /// Gets an optional diagnostic descriptor.
    /// </summary>
    public ParserDiagnosticDescriptor? Diagnostic { get; }

    /// <summary>
    /// Gets optional diagnostic arguments. Never <c>null</c>.
    /// </summary>
    public IReadOnlyList<object?> DiagnosticArguments { get; }

    /// <summary>
    /// Gets an optional exception that can be attached to diagnostics.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets an executed action outcome.
    /// </summary>
    public static ParserActionExecutionOutcome Executed { get; } = new(ParserActionExecutionStatus.Executed);

    /// <summary>
    /// Creates a conservative non-executed outcome with no detailed diagnostic metadata.
    /// </summary>
    /// <returns>Non-executed outcome without explicit diagnostic.</returns>
    public static ParserActionExecutionOutcome NotExecuted()
    {
        return new ParserActionExecutionOutcome(ParserActionExecutionStatus.NotExecuted);
    }

    /// <summary>
    /// Creates a non-executed outcome with explicit diagnostic metadata.
    /// </summary>
    /// <param name="diagnostic">Diagnostic descriptor to emit.</param>
    /// <param name="exception">Optional execution exception.</param>
    /// <param name="diagnosticArguments">Optional diagnostic formatting arguments.</param>
    /// <returns>Non-executed outcome with detailed diagnostic metadata.</returns>
    public static ParserActionExecutionOutcome NotExecuted(
        ParserDiagnosticDescriptor diagnostic,
        Exception? exception,
        params object?[] diagnosticArguments)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new ParserActionExecutionOutcome(
            ParserActionExecutionStatus.NotExecuted,
            diagnostic,
            exception,
            diagnosticArguments);
    }
}
