using System;
using Utils.Parser.Model;

namespace Utils.Parser.Resolution;

/// <summary>
/// Holds mutable internal state while a rule kind is being resolved.
/// </summary>
internal sealed class RuleResolutionBuilder
{
    /// <summary>
    /// Initializes a new builder from the public rule being resolved.
    /// </summary>
    /// <param name="source">Rule that provides the immutable public rule data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    public RuleResolutionBuilder(Rule source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Kind = source.Kind;
    }

    /// <summary>
    /// Gets the source rule whose immutable data is preserved during resolution.
    /// </summary>
    public Rule Source { get; }

    /// <summary>
    /// Gets the source rule name.
    /// </summary>
    public string Name => Source.Name;

    /// <summary>
    /// Gets the currently resolved or unresolved rule kind.
    /// </summary>
    public RuleKind Kind { get; private set; }

    /// <summary>
    /// Resolves the rule as the specified non-unresolved kind.
    /// </summary>
    /// <param name="kind">Final lexer or parser kind to assign.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kind"/> is <see cref="RuleKind.Unresolved"/>.</exception>
    /// <exception cref="GrammarValidationException">Thrown when a conflicting kind was already resolved.</exception>
    public void ResolveAs(RuleKind kind)
    {
        if (kind == RuleKind.Unresolved)
        {
            throw new ArgumentException("A rule cannot be resolved as Unresolved.", nameof(kind));
        }

        if (Kind != RuleKind.Unresolved && Kind != kind)
        {
            throw new GrammarValidationException(
                $"Rule '{Name}' was already resolved as {Kind} and cannot be resolved as {kind}.");
        }

        Kind = kind;
    }

    /// <summary>
    /// Builds the final immutable rule with the resolved kind.
    /// </summary>
    /// <returns>A rule that preserves source data and exposes the final kind.</returns>
    public Rule Build()
        => Source with { Kind = Kind };
}
