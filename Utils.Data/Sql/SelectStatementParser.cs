using System;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses SELECT statements by delegating token consumption to a shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class SelectStatementParser
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser managing token access and helper utilities.</param>
    public SelectStatementParser(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Parses a SELECT statement.
    /// </summary>
    /// <param name="withClause">The WITH clause associated with the SELECT statement, if any.</param>
    /// <returns>The built <see cref="SqlSelectStatement"/>.</returns>
    public SqlSelectStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("SELECT");
        bool isDistinct = parser.TryConsumeKeyword("DISTINCT");
        var selectReader = new SelectPartReader(parser);
        var fromReader = new FromPartReader(parser);
        var whereReader = new WherePartReader(parser);
        var groupByReader = new GroupByPartReader(parser);
        var havingReader = new HavingPartReader(parser);
        var orderByReader = new OrderByPartReader(parser);
        var limitReader = new LimitPartReader(parser);
        var offsetReader = new OffsetPartReader(parser);
        var returningReader = new ReturningPartReader(parser);
        var setOperatorReader = new SetOperatorPartReader(parser);

        var clauseTerminators = BuildClauseTerminators(
            fromReader.ClauseKeyword,
            whereReader.ClauseKeyword,
            groupByReader.ClauseKeyword,
            havingReader.ClauseKeyword,
            orderByReader.ClauseKeyword,
            limitReader.ClauseKeyword,
            offsetReader.ClauseKeyword,
            returningReader.ClauseKeyword,
            setOperatorReader.ClauseKeyword);

        var selectPart = selectReader.ReadSelectPart(clauseTerminators);

        var fromPart = fromReader.TryReadFromPart(GetRemainingTerminators(clauseTerminators, 1));

        var wherePart = whereReader.TryReadWherePart(GetRemainingTerminators(clauseTerminators, 2));

        var groupByPart = groupByReader.TryReadGroupByPart(GetRemainingTerminators(clauseTerminators, 3));

        var havingPart = havingReader.TryReadHavingPart(GetRemainingTerminators(clauseTerminators, 4));

        var orderByPart = orderByReader.TryReadOrderByPart(GetRemainingTerminators(clauseTerminators, 5));

        var limitPart = limitReader.TryReadLimitPart(GetRemainingTerminators(clauseTerminators, 6));

        var offsetPart = offsetReader.TryReadOffsetPart(GetRemainingTerminators(clauseTerminators, 7));

        var tailPart = setOperatorReader.TryReadTailPart();

        return new SqlSelectStatement(
            selectPart,
            fromPart,
            wherePart,
            groupByPart,
            havingPart,
            orderByPart,
            limitPart,
            offsetPart,
            tailPart,
            withClause,
            isDistinct);
    }

    /// <summary>
    /// Builds the ordered list of clause terminators using the provided clause keywords and the statement end marker.
    /// </summary>
    /// <param name="clauseKeywords">The clause keywords that may terminate the current part.</param>
    /// <returns>An ordered array of clause terminators.</returns>
    private static ClauseStart[] BuildClauseTerminators(params ClauseStart[] clauseKeywords)
    {
        var terminators = new ClauseStart[clauseKeywords.Length + 1];
        Array.Copy(clauseKeywords, terminators, clauseKeywords.Length);
        terminators[^1] = ClauseStart.StatementEnd;
        return terminators;
    }

    /// <summary>
    /// Retrieves a subset of clause terminators beginning at the specified index.
    /// </summary>
    /// <param name="terminators">The ordered terminators.</param>
    /// <param name="startIndex">The index to start from.</param>
    /// <returns>The remaining terminators starting at <paramref name="startIndex"/>.</returns>
    private static ClauseStart[] GetRemainingTerminators(ClauseStart[] terminators, int startIndex)
    {
        var remainingLength = terminators.Length - startIndex;
        if (remainingLength <= 0)
        {
            return Array.Empty<ClauseStart>();
        }

        var remaining = new ClauseStart[remainingLength];
        Array.Copy(terminators, startIndex, remaining, 0, remainingLength);
        return remaining;
    }
}
