using Utils.Parser.EmbeddedCode;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Represents an expression-backed semantic predicate that was compiled during embedded-code preparation.
/// </summary>
public sealed class PreparedExpressionSemanticPredicate
{
    private readonly Func<SemanticPredicateEvaluationContext, bool> _predicate;

    /// <summary>
    /// Initializes a new prepared semantic predicate artifact.
    /// </summary>
    /// <param name="source">Embedded-code source that produced this artifact.</param>
    /// <param name="preparationContext">Preparation context used to compile this artifact.</param>
    /// <param name="predicate">Compiled predicate delegate that evaluates against the current runtime context.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <c>null</c>.</exception>
    public PreparedExpressionSemanticPredicate(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext preparationContext,
        Func<SemanticPredicateEvaluationContext, bool> predicate)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        PreparationContext = preparationContext ?? throw new ArgumentNullException(nameof(preparationContext));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>
    /// Gets the embedded-code source that produced this artifact.
    /// </summary>
    public EmbeddedCodeSource Source { get; }

    /// <summary>
    /// Gets the preparation context used to compile this artifact.
    /// </summary>
    public EmbeddedCodePreparationContext PreparationContext { get; }

    /// <summary>
    /// Evaluates the compiled predicate against the supplied runtime context without recompiling source text.
    /// </summary>
    /// <param name="context">Current parser runtime context.</param>
    /// <returns>A semantic predicate outcome compatible with parser runtime policy results.</returns>
    public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            return _predicate(context)
                ? SemanticPredicateEvaluationOutcome.Satisfied
                : SemanticPredicateEvaluationOutcome.Rejected;
        }
        catch (Exception exception)
        {
            return SemanticPredicateEvaluationOutcome.NotEvaluated(
                ParserDiagnostics.EmbeddedCodeCompilationFailed,
                exception,
                "semantic predicate",
                exception.Message);
        }
    }
}
