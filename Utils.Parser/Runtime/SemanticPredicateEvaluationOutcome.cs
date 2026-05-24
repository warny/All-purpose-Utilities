using Utils.Parser.Diagnostics;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a semantic predicate evaluation outcome with optional diagnostic metadata.
/// </summary>
public sealed record SemanticPredicateEvaluationOutcome
{
    /// <summary>
    /// Initializes a new semantic predicate evaluation outcome.
    /// </summary>
    /// <param name="status">Evaluation status.</param>
    /// <param name="diagnostic">Optional diagnostic descriptor to emit from parser runtime.</param>
    /// <param name="exception">Optional exception associated with the evaluation attempt.</param>
    /// <param name="diagnosticArguments">Optional diagnostic formatting arguments.</param>
    public SemanticPredicateEvaluationOutcome(
        SemanticPredicateEvaluationStatus status,
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
    /// Gets the evaluation status.
    /// </summary>
    public SemanticPredicateEvaluationStatus Status { get; }

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
    /// Gets a successful predicate outcome.
    /// </summary>
    public static SemanticPredicateEvaluationOutcome Satisfied { get; } = new(SemanticPredicateEvaluationStatus.Satisfied);

    /// <summary>
    /// Gets a rejected predicate outcome.
    /// </summary>
    public static SemanticPredicateEvaluationOutcome Rejected { get; } = new(SemanticPredicateEvaluationStatus.Rejected);

    /// <summary>
    /// Creates a conservative non-evaluated outcome with no detailed diagnostic metadata.
    /// </summary>
    /// <returns>Non-evaluated outcome without explicit diagnostic.</returns>
    public static SemanticPredicateEvaluationOutcome NotEvaluated()
    {
        return new SemanticPredicateEvaluationOutcome(SemanticPredicateEvaluationStatus.NotEvaluated);
    }

    /// <summary>
    /// Creates a non-evaluated outcome with explicit diagnostic metadata.
    /// </summary>
    /// <param name="diagnostic">Diagnostic descriptor to emit.</param>
    /// <param name="exception">Optional evaluation exception.</param>
    /// <param name="diagnosticArguments">Optional diagnostic formatting arguments.</param>
    /// <returns>Non-evaluated outcome with detailed diagnostic metadata.</returns>
    public static SemanticPredicateEvaluationOutcome NotEvaluated(
        ParserDiagnosticDescriptor diagnostic,
        Exception? exception,
        params object?[] diagnosticArguments)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new SemanticPredicateEvaluationOutcome(
            SemanticPredicateEvaluationStatus.NotEvaluated,
            diagnostic,
            exception,
            diagnosticArguments);
    }
}
