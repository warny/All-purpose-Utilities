using System;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses DELETE statements relying on a shared <see cref="SqlParser"/> instance.
/// </summary>
internal sealed class DeleteStatementParser
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser providing token access.</param>
    public DeleteStatementParser(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Parses a DELETE statement.
    /// </summary>
    /// <param name="withClause">The WITH clause bound to the statement, if present.</param>
    /// <returns>The parsed <see cref="SqlDeleteStatement"/>.</returns>
    public SqlDeleteStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("DELETE");
        var deleteTargetReader = new DeletePartReader(parser);
        var fromReader = new FromPartReader(parser);
        var whereReader = new WherePartReader(parser);
        var outputReader = new OutputPartReader(parser);
        var returningReader = new ReturningPartReader(parser);

        var targetSegment = deleteTargetReader.TryReadDeleteTarget();

        var fromSegment = fromReader.TryReadFromPart(
            outputReader.ClauseKeyword,
            ClauseStart.Using,
            whereReader.ClauseKeyword,
            returningReader.ClauseKeyword,
            ClauseStart.StatementEnd);
        if (fromSegment == null)
        {
            throw new SqlParseException("Expected FROM clause in DELETE statement.");
        }

        SqlSegment? usingSegment = null;
        SqlSegment? whereSegment = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        if (parser.TryConsumeKeyword("OUTPUT"))
        {
            outputSegment = outputReader.ReadOutputPart(
                "Output",
                ClauseStart.Using,
                whereReader.ClauseKeyword,
                returningReader.ClauseKeyword,
                ClauseStart.StatementEnd);
        }

        if (parser.TryConsumeKeyword("USING"))
        {
            var usingTokens = parser.ReadSectionTokens(ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
            usingSegment = parser.BuildSegment("Using", usingTokens);
        }

        whereSegment = whereReader.TryReadWherePart(returningReader.ClauseKeyword, ClauseStart.StatementEnd);

        if (parser.TryConsumeKeyword("RETURNING"))
        {
            returningSegment = returningReader.ReadReturningPart(ClauseStart.StatementEnd);
        }

        return new SqlDeleteStatement(targetSegment, fromSegment, usingSegment, whereSegment, outputSegment, returningSegment, withClause);
    }
}
