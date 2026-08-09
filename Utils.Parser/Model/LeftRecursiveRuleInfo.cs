using System.Collections.Immutable;

namespace Utils.Parser.Model;

/// <summary>
/// Describes a parser rule that uses direct left recursion and how its
/// alternatives are split for seed parsing and left-push extension.
/// </summary>
public sealed record LeftRecursiveRuleInfo
{
    private IReadOnlyList<Alternative> _baseAlternatives = ImmutableArray<Alternative>.Empty;
    private IReadOnlyList<Alternative> _recursiveAlternatives = ImmutableArray<Alternative>.Empty;

    /// <summary>Gets the parser rule described by this entry.</summary>
    public required Rule Rule { get; init; }

    /// <summary>Gets an immutable snapshot of alternatives usable as initial seed nodes.</summary>
    public required IReadOnlyList<Alternative> BaseAlternatives
    {
        get => _baseAlternatives;
        init => _baseAlternatives = value.ToImmutableArray();
    }

    /// <summary>Gets an immutable snapshot of direct left-recursive alternatives.</summary>
    public required IReadOnlyList<Alternative> RecursiveAlternatives
    {
        get => _recursiveAlternatives;
        init => _recursiveAlternatives = value.ToImmutableArray();
    }
}
