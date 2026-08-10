using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Lightweight descriptor capturing the conservative structural token prefix for a single grammar alternative.
/// Produced by <see cref="AlternativeStructuralPrefixExtractor"/> during grammar-level preparation,
/// this descriptor carries only ordered token name strings and contains no
/// <see cref="Utils.Parser.Model.RuleContent"/> references.
/// It is safe to forward through the scheduler pipeline without coupling the scheduler to grammar internals.
/// </summary>
internal readonly record struct AlternativeStructuralDescriptor
{
    /// <summary>Initializes a structural descriptor and captures its ordered token snapshot.</summary>
    /// <param name="alternativeIndex">Zero-based alternative index.</param>
    /// <param name="structuralTokens">Ordered structural token names.</param>
    public AlternativeStructuralDescriptor(int alternativeIndex, IReadOnlyList<string> structuralTokens)
    {
        AlternativeIndex = alternativeIndex;
        StructuralTokens = structuralTokens.ToImmutableArray();
    }

    public int AlternativeIndex { get; }

    /// <summary>Gets the immutable structural-token snapshot captured at construction time.</summary>
    public IReadOnlyList<string> StructuralTokens { get; }
}
