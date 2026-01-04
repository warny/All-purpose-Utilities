using System;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses UPDATE statements using the shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class UpdateStatementParser
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser for token management.</param>
    public UpdateStatementParser(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Parses an UPDATE statement.
    /// </summary>
    /// <param name="withClause">The optional WITH clause attached to the statement.</param>
    /// <returns>The parsed <see cref="SqlUpdateStatement"/>.</returns>
    public SqlUpdateStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("UPDATE");
        var updateTargetReader = new UpdatePartReader(parser);
        var outputReader = new OutputPartReader(parser);
        var fromReader = new FromPartReader(parser);
        var whereReader = new WherePartReader(parser);
        var returningReader = new ReturningPartReader(parser);

        var targetSegment = updateTargetReader.ReadUpdateTarget();
        parser.ExpectKeyword("SET");
        var setSegment = new SetPartReader(parser).ReadSetPart();

        SqlSegment? outputSegment = null;
        SqlSegment? fromSegment = null;
        SqlSegment? whereSegment = null;
        SqlSegment? returningSegment = null;

        if (parser.TryConsumeKeyword("OUTPUT"))
        {
            outputSegment = outputReader.ReadOutputPart(
                "Output",
                fromReader.ClauseKeyword,
                whereReader.ClauseKeyword,
                returningReader.ClauseKeyword,
                ClauseStart.StatementEnd);
        }

        fromSegment = fromReader.TryReadFromPart(
            whereReader.ClauseKeyword,
            returningReader.ClauseKeyword,
            ClauseStart.StatementEnd);

        whereSegment = whereReader.TryReadWherePart(returningReader.ClauseKeyword, ClauseStart.StatementEnd);

        if (parser.TryConsumeKeyword("RETURNING"))
        {
            returningSegment = returningReader.ReadReturningPart(ClauseStart.StatementEnd);
        }

        return new SqlUpdateStatement(targetSegment, setSegment, fromSegment, whereSegment, outputSegment, returningSegment, withClause);
    }
}
