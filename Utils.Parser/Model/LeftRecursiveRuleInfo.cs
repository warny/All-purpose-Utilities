namespace Utils.Parser.Model;

/// <summary>
/// Describes a parser rule that uses direct left recursion and how its
/// alternatives are split for seed parsing and left-push extension.
/// </summary>
public sealed record LeftRecursiveRuleInfo
{
    /// <summary>Gets the parser rule described by this entry.</summary>
    public required Rule Rule { get; init; }

    /// <summary>
    /// Gets alternatives that do not start with the rule itself and can be used
    /// as initial seed nodes.
    /// </summary>
    public required IReadOnlyList<Alternative> BaseAlternatives { get; init; }

    /// <summary>
    /// Gets direct left-recursive alternatives that start with the rule itself.
    /// </summary>
    public required IReadOnlyList<Alternative> RecursiveAlternatives { get; init; }
}
