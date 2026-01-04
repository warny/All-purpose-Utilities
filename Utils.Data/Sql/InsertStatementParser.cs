using System;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses INSERT statements using a shared <see cref="SqlParser"/> helper context.
/// </summary>
internal sealed class InsertStatementParser
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="InsertStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser providing token utilities.</param>
    public InsertStatementParser(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Parses an INSERT statement.
    /// </summary>
    /// <param name="withClause">The optional WITH clause attached to the statement.</param>
    /// <returns>The created <see cref="SqlInsertStatement"/>.</returns>
    public SqlInsertStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("INSERT");
        if (parser.TryConsumeKeyword("INTO") == false)
        {
            parser.ExpectKeyword("INTO");
        }

        var intoReader = new IntoPartReader(parser);
        var valuesReader = new ValuesPartReader(parser);
        var outputReader = new OutputPartReader(parser);
        var returningReader = new ReturningPartReader(parser);

        var targetSegment = intoReader.ReadIntoTarget(
            outputReader.ClauseKeyword,
            valuesReader.ClauseKeyword,
            ClauseStart.Select,
            returningReader.ClauseKeyword,
            ClauseStart.StatementEnd);
        SqlSegment? valuesSegment = null;
        SqlStatement? sourceQuery = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        int returningIndex = parser.FindClauseIndex("RETURNING");

        if (parser.TryConsumeKeyword("OUTPUT"))
        {
            outputSegment = outputReader.ReadOutputPart(
                "Output",
                valuesReader.ClauseKeyword,
                ClauseStart.Select,
                returningReader.ClauseKeyword,
                ClauseStart.StatementEnd);
        }

        if (parser.CheckKeyword("VALUES"))
        {
            parser.ExpectKeyword("VALUES");
            valuesSegment = valuesReader.ReadValuesPart(returningReader.ClauseKeyword, ClauseStart.StatementEnd);
        }
        else if (parser.CheckKeyword("SELECT") || parser.CheckKeyword("WITH"))
        {
            int end = returningIndex >= 0 ? returningIndex : parser.Tokens.Count;
            var sourceTokens = parser.Tokens.GetRange(parser.Position, end - parser.Position);
            var subParser = new SqlParser(sourceTokens, parser.SyntaxOptions);
            sourceQuery = subParser.ParseStatementWithOptionalCte();
            subParser.ConsumeOptionalTerminator();
            subParser.EnsureEndOfInput();
            parser.Position = end;
        }

        if (parser.TryConsumeKeyword("RETURNING"))
        {
            returningSegment = returningReader.ReadReturningPart(ClauseStart.StatementEnd);
        }

        return new SqlInsertStatement(targetSegment, valuesSegment, sourceQuery, outputSegment, returningSegment, withClause);
    }
}
