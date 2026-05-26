namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Creates deterministic ANTLR4 prequel name sets using ordinal comparers.
/// </summary>
internal static class Antlr4NameSet
{
    /// <summary>
    /// Builds a read-only set from <paramref name="names"/> preserving ordinal comparison semantics.
    /// </summary>
    /// <param name="names">Names to include in the set.</param>
    /// <returns>An ordinal, read-only hash set.</returns>
    public static IReadOnlySet<string> Create(IEnumerable<string> names)
    {
        return new HashSet<string>(names, StringComparer.Ordinal);
    }
}
