using Utils.Parser.EmbeddedCode;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Identifies a prepared expression embedded-code artifact using the stable runtime metadata available to parser policies.
/// </summary>
/// <param name="Kind">Embedded-code kind stored in the registry.</param>
/// <param name="RuleName">Name of the parser rule that owns the embedded source.</param>
/// <param name="SourceText">Raw embedded-code source text without ANTLR delimiters.</param>
/// <param name="AlternativeIndex">Zero-based alternative index, or <c>null</c> when unavailable.</param>
/// <param name="ElementIndex">Zero-based element index, or <c>null</c> when unavailable.</param>
public sealed record PreparedExpressionEmbeddedCodeKey(
    EmbeddedCodeKind Kind,
    string RuleName,
    string SourceText,
    int? AlternativeIndex,
    int? ElementIndex)
{
    /// <summary>
    /// Creates a key from prepared-source metadata.
    /// </summary>
    /// <param name="source">Prepared embedded-code source metadata.</param>
    /// <param name="fallbackRuleName">Optional rule name used when <paramref name="source"/> does not carry one.</param>
    /// <returns>A deterministic lookup key for the prepared source.</returns>
    /// <exception cref="ArgumentException">Thrown when no usable rule name is available.</exception>
    public static PreparedExpressionEmbeddedCodeKey FromSource(EmbeddedCodeSource source, string? fallbackRuleName = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PreparedExpressionEmbeddedCodeKey(
            source.Kind,
            RequireRuleName(source.RuleName ?? fallbackRuleName),
            source.SourceText,
            source.AlternativeIndex,
            source.ElementIndex);
    }

    /// <summary>
    /// Creates a semantic-predicate key from runtime evaluation metadata.
    /// </summary>
    /// <param name="context">Current semantic-predicate evaluation context.</param>
    /// <returns>A deterministic lookup key for the current predicate invocation.</returns>
    public static PreparedExpressionEmbeddedCodeKey FromSemanticPredicateContext(SemanticPredicateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new PreparedExpressionEmbeddedCodeKey(
            EmbeddedCodeKind.SemanticPredicate,
            RequireRuleName(context.Rule.Name),
            context.PredicateCode,
            NormalizeRuntimeIndex(context.AlternativeIndex),
            NormalizeRuntimeIndex(context.ElementIndex));
    }

    /// <summary>
    /// Creates a parser-action key from runtime execution metadata.
    /// </summary>
    /// <param name="context">Current parser-action execution context.</param>
    /// <returns>A deterministic lookup key for the current action invocation.</returns>
    public static PreparedExpressionEmbeddedCodeKey FromParserActionContext(ParserActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new PreparedExpressionEmbeddedCodeKey(
            EmbeddedCodeKind.ParserInlineAction,
            RequireRuleName(context.Rule.Name),
            context.ActionCode,
            NormalizeRuntimeIndex(context.AlternativeIndex),
            NormalizeRuntimeIndex(context.ElementIndex));
    }

    /// <summary>
    /// Normalizes runtime indexes where <c>-1</c> represents an unavailable index.
    /// </summary>
    /// <param name="index">Runtime index value.</param>
    /// <returns>The supplied index when available; otherwise <c>null</c>.</returns>
    private static int? NormalizeRuntimeIndex(int index) => index >= 0 ? index : null;

    /// <summary>
    /// Ensures that a key always carries an audit-friendly owning rule name.
    /// </summary>
    /// <param name="ruleName">Rule name candidate.</param>
    /// <returns>The validated rule name.</returns>
    /// <exception cref="ArgumentException">Thrown when the rule name is missing.</exception>
    private static string RequireRuleName(string? ruleName)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new ArgumentException("Prepared expression embedded-code keys require an owning rule name.", nameof(ruleName));
        }

        return ruleName;
    }
}
