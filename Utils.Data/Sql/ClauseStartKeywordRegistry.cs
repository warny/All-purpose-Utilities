using System.Collections.Generic;

namespace Utils.Data.Sql;

/// <summary>
/// Provides keyword metadata for clause boundaries so <see cref="SqlParser"/> can detect clause transitions
/// without hardcoded keyword checks.
/// </summary>
internal static class ClauseStartKeywordRegistry
{
    /// <summary>
    /// Gets the default mapping between clause identifiers and the keyword sequences that start the clause.
    /// </summary>
    public static IReadOnlyDictionary<ClauseStart, IReadOnlyList<IReadOnlyList<string>>> KnownClauseKeywords { get; } =
        BuildKnownClauseKeywords();

    /// <summary>
    /// Attempts to retrieve the keyword sequences that mark the beginning of the specified clause.
    /// </summary>
    /// <param name="clauseStart">The clause identifier.</param>
    /// <param name="keywordSequences">The keyword sequences associated with the clause.</param>
    /// <returns><c>true</c> when the clause metadata is available; otherwise, <c>false</c>.</returns>
    public static bool TryGetClauseKeywords(
        ClauseStart clauseStart,
        out IReadOnlyList<IReadOnlyList<string>> keywordSequences)
    {
        return KnownClauseKeywords.TryGetValue(clauseStart, out keywordSequences!);
    }

    private static IReadOnlyDictionary<ClauseStart, IReadOnlyList<IReadOnlyList<string>>> BuildKnownClauseKeywords()
    {
        var definitions = new List<ClauseKeywordDefinition>
        {
            SelectPartReader.KeywordDefinition,
            FromPartReader.KeywordDefinition,
            IntoPartReader.KeywordDefinition,
            WherePartReader.KeywordDefinition,
            GroupByPartReader.KeywordDefinition,
            HavingPartReader.KeywordDefinition,
            OrderByPartReader.KeywordDefinition,
            LimitPartReader.KeywordDefinition,
            OffsetPartReader.KeywordDefinition,
            ValuesPartReader.KeywordDefinition,
            OutputPartReader.KeywordDefinition,
            ReturningPartReader.KeywordDefinition,
            SetOperatorPartReader.KeywordDefinition,
            ClauseKeywordDefinition.FromKeywords(ClauseStart.Using, new[] { "USING" }),
        };

        var map = new Dictionary<ClauseStart, IReadOnlyList<IReadOnlyList<string>>>();
        foreach (var definition in definitions)
        {
            map[definition.ClauseKeyword] = definition.KeywordSequences;
        }

        return map;
    }
}

/// <summary>
/// Represents the keyword sequences that start a specific SQL clause.
/// </summary>
internal sealed class ClauseKeywordDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClauseKeywordDefinition"/> class.
    /// </summary>
    /// <param name="clauseKeyword">The clause identifier.</param>
    /// <param name="keywordSequences">The keyword sequences that open the clause.</param>
    public ClauseKeywordDefinition(
        ClauseStart clauseKeyword,
        IReadOnlyList<IReadOnlyList<string>> keywordSequences)
    {
        ClauseKeyword = clauseKeyword;
        KeywordSequences = keywordSequences;
    }

    /// <summary>
    /// Gets the clause identifier associated with the keywords.
    /// </summary>
    public ClauseStart ClauseKeyword { get; }

    /// <summary>
    /// Gets the keyword sequences that mark the start of the clause.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> KeywordSequences { get; }

    /// <summary>
    /// Creates a clause keyword definition from the specified keyword sequences.
    /// </summary>
    /// <param name="clauseKeyword">The clause identifier.</param>
    /// <param name="keywordSequences">The keyword sequences that start the clause.</param>
    /// <returns>The created <see cref="ClauseKeywordDefinition"/>.</returns>
    public static ClauseKeywordDefinition FromKeywords(ClauseStart clauseKeyword, params string[][] keywordSequences)
    {
        return new ClauseKeywordDefinition(clauseKeyword, keywordSequences);
    }
}
