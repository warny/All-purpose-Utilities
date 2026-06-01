using System.Collections.Concurrent;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Stores prepared expression embedded-code artifacts for explicit runtime lookup by parser policies.
/// </summary>
public sealed class PreparedExpressionEmbeddedCodeRegistry
{
    private readonly ConcurrentDictionary<PreparedExpressionEmbeddedCodeKey, PreparedExpressionSemanticPredicate> _semanticPredicates = new();
    private readonly ConcurrentDictionary<PreparedExpressionEmbeddedCodeKey, PreparedExpressionParserAction> _parserActions = new();

    /// <summary>
    /// Adds a prepared semantic predicate using the key derived from its source metadata.
    /// </summary>
    /// <param name="artifact">Prepared semantic predicate artifact.</param>
    /// <returns><c>true</c> when the artifact was added; <c>false</c> when the key already exists.</returns>
    public bool TryAddSemanticPredicate(PreparedExpressionSemanticPredicate artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return TryAddSemanticPredicate(
            PreparedExpressionEmbeddedCodeKey.FromSource(artifact.Source, artifact.PreparationContext.RuleName),
            artifact);
    }

    /// <summary>
    /// Adds a prepared semantic predicate using an explicit key.
    /// </summary>
    /// <param name="key">Prepared artifact lookup key.</param>
    /// <param name="artifact">Prepared semantic predicate artifact.</param>
    /// <returns><c>true</c> when the artifact was added; <c>false</c> when the key already exists.</returns>
    public bool TryAddSemanticPredicate(PreparedExpressionEmbeddedCodeKey key, PreparedExpressionSemanticPredicate artifact)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(artifact);

        EnsureKind(key, EmbeddedCodeKind.SemanticPredicate);

        return _semanticPredicates.TryAdd(key, artifact);
    }

    /// <summary>
    /// Adds a prepared parser action using the key derived from its source metadata.
    /// </summary>
    /// <param name="artifact">Prepared parser action artifact.</param>
    /// <returns><c>true</c> when the artifact was added; <c>false</c> when the key already exists.</returns>
    public bool TryAddParserAction(PreparedExpressionParserAction artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return TryAddParserAction(
            PreparedExpressionEmbeddedCodeKey.FromSource(artifact.Source, artifact.PreparationContext.RuleName),
            artifact);
    }

    /// <summary>
    /// Adds a prepared parser action using an explicit key.
    /// </summary>
    /// <param name="key">Prepared artifact lookup key.</param>
    /// <param name="artifact">Prepared parser action artifact.</param>
    /// <returns><c>true</c> when the artifact was added; <c>false</c> when the key already exists.</returns>
    public bool TryAddParserAction(PreparedExpressionEmbeddedCodeKey key, PreparedExpressionParserAction artifact)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(artifact);

        EnsureKind(key, EmbeddedCodeKind.ParserInlineAction);

        return _parserActions.TryAdd(key, artifact);
    }

    /// <summary>
    /// Looks up a prepared semantic predicate from runtime evaluation metadata.
    /// </summary>
    /// <param name="context">Current semantic-predicate evaluation context.</param>
    /// <param name="artifact">Prepared artifact when found.</param>
    /// <returns><c>true</c> when a matching artifact exists; otherwise <c>false</c>.</returns>
    public bool TryGetSemanticPredicate(SemanticPredicateEvaluationContext context, out PreparedExpressionSemanticPredicate? artifact)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TryGetSemanticPredicate(PreparedExpressionEmbeddedCodeKey.FromSemanticPredicateContext(context), out artifact);
    }

    /// <summary>
    /// Looks up a prepared semantic predicate by exact key.
    /// </summary>
    /// <param name="key">Prepared artifact lookup key.</param>
    /// <param name="artifact">Prepared artifact when found.</param>
    /// <returns><c>true</c> when a matching artifact exists; otherwise <c>false</c>.</returns>
    public bool TryGetSemanticPredicate(PreparedExpressionEmbeddedCodeKey key, out PreparedExpressionSemanticPredicate? artifact)
    {
        ArgumentNullException.ThrowIfNull(key);

        EnsureKind(key, EmbeddedCodeKind.SemanticPredicate);

        return _semanticPredicates.TryGetValue(key, out artifact);
    }

    /// <summary>
    /// Looks up a prepared parser action from runtime execution metadata.
    /// </summary>
    /// <param name="context">Current parser-action execution context.</param>
    /// <param name="artifact">Prepared artifact when found.</param>
    /// <returns><c>true</c> when a matching artifact exists; otherwise <c>false</c>.</returns>
    public bool TryGetParserAction(ParserActionExecutionContext context, out PreparedExpressionParserAction? artifact)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TryGetParserAction(PreparedExpressionEmbeddedCodeKey.FromParserActionContext(context), out artifact);
    }

    /// <summary>
    /// Looks up a prepared parser action by exact key.
    /// </summary>
    /// <param name="key">Prepared artifact lookup key.</param>
    /// <param name="artifact">Prepared artifact when found.</param>
    /// <returns><c>true</c> when a matching artifact exists; otherwise <c>false</c>.</returns>
    public bool TryGetParserAction(PreparedExpressionEmbeddedCodeKey key, out PreparedExpressionParserAction? artifact)
    {
        ArgumentNullException.ThrowIfNull(key);

        EnsureKind(key, EmbeddedCodeKind.ParserInlineAction);

        return _parserActions.TryGetValue(key, out artifact);
    }

    /// <summary>
    /// Validates that a caller-provided key belongs to the artifact category being accessed.
    /// </summary>
    /// <param name="key">Prepared artifact lookup key.</param>
    /// <param name="expectedKind">Expected embedded-code kind for the registry bucket.</param>
    /// <exception cref="ArgumentException">Thrown when the key kind does not match the registry bucket.</exception>
    private static void EnsureKind(PreparedExpressionEmbeddedCodeKey key, EmbeddedCodeKind expectedKind)
    {
        if (key.Kind != expectedKind)
        {
            throw new ArgumentException($"Expected prepared expression embedded-code key kind '{expectedKind}', got '{key.Kind}'.", nameof(key));
        }
    }
}
