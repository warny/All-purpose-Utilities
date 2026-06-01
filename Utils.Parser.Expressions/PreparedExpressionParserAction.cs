using Utils.Parser.EmbeddedCode;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Represents an expression-backed parser inline action that was compiled during embedded-code preparation.
/// </summary>
public sealed class PreparedExpressionParserAction
{
    private readonly Action<ParserActionExecutionContext> _action;

    /// <summary>
    /// Initializes a new prepared parser action artifact.
    /// </summary>
    /// <param name="source">Embedded-code source that produced this artifact.</param>
    /// <param name="preparationContext">Preparation context used to compile this artifact.</param>
    /// <param name="action">Compiled action delegate that executes against the current runtime context.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <c>null</c>.</exception>
    public PreparedExpressionParserAction(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext preparationContext,
        Action<ParserActionExecutionContext> action)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        PreparationContext = preparationContext ?? throw new ArgumentNullException(nameof(preparationContext));
        _action = action ?? throw new ArgumentNullException(nameof(action));
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
    /// Executes the compiled action against the supplied runtime context without recompiling source text.
    /// </summary>
    /// <param name="context">Current parser runtime context.</param>
    /// <returns>A parser action execution outcome compatible with parser runtime policy results.</returns>
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            _action(context);
            return ParserActionExecutionOutcome.Executed;
        }
        catch (Exception exception)
        {
            return ParserActionExecutionOutcome.NotExecuted(
                ParserDiagnostics.EmbeddedCodeCompilationFailed,
                exception,
                "parser action",
                exception.Message);
        }
    }
}
