using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Executes parser inline actions by running prepared expression artifacts from an explicit registry.
/// </summary>
public sealed class PreparedExpressionParserActionExecutor : IParserActionExecutor
{
    private readonly PreparedExpressionEmbeddedCodeRegistry _registry;

    /// <summary>
    /// Initializes a new executor that consumes already-prepared parser action artifacts.
    /// </summary>
    /// <param name="registry">Registry that maps runtime action contexts to prepared artifacts.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is <c>null</c>.</exception>
    public PreparedExpressionParserActionExecutor(PreparedExpressionEmbeddedCodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _registry.TryGetParserAction(context, out var artifact) && artifact is not null
            ? artifact.Execute(context)
            : ParserActionExecutionOutcome.NotExecuted();
    }
}
