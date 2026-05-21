namespace Utils.Parser.Runtime;

/// <summary>
/// Lightweight descriptor capturing the conservative structural token prefix for a single grammar alternative.
/// Produced by <see cref="AlternativeStructuralPrefixExtractor"/> during grammar-level preparation,
/// this descriptor carries only ordered token name strings and contains no
/// <see cref="Utils.Parser.Model.RuleContent"/> references.
/// It is safe to forward through the scheduler pipeline without coupling the scheduler to grammar internals.
/// </summary>
internal readonly record struct AlternativeStructuralDescriptor(
    /// <summary>Zero-based index matching the alternative's position in its parent alternation.</summary>
    int AlternativeIndex,
    /// <summary>
    /// Ordered structural token sequence extracted conservatively from the alternative's content.
    /// Only <see cref="Utils.Parser.Model.RuleRef"/> and <see cref="Utils.Parser.Model.LiteralMatch"/>
    /// elements at the head of a <see cref="Utils.Parser.Model.Sequence"/> are included.
    /// Complex constructs (quantifiers, nested alternations, actions) stop extraction.
    /// This list is read-only; callers must not attempt to cast or mutate it.
    /// </summary>
    IReadOnlyList<string> StructuralTokens);
