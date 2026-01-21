using System;
using System.Collections.Generic;
using Utils.Collections;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses INSERT statements using a shared <see cref="SqlParser"/> helper context.
/// </summary>
internal sealed class InsertStatementParser : StatementParserBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsertStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser providing token utilities.</param>
    public InsertStatementParser(SqlParser parser) : base(parser) { }

    /// <summary>
    /// Parses an INSERT statement.
    /// </summary>
    /// <param name="withClause">The optional WITH clause attached to the statement.</param>
    /// <returns>The created <see cref="SqlInsertStatement"/>.</returns>
    public SqlInsertStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("INSERT");
        if (!parser.TryConsumeKeyword("INTO"))
        {
            parser.ExpectKeyword("INTO");
        }

        var segments = new Dictionary<ClauseStart, SqlSegment>
        {
            [IntoReader.Clause] = IntoReader.TryRead(
                parser,
                OutputReader.Clause,
                ValuesReader.Clause,
                ClauseStart.Select,
                ReturningReader.Clause,
                ClauseStart.StatementEnd),
        };

        if (parser.CheckKeyword("OUTPUT"))
        {
            segments[OutputReader.Clause] = OutputReader.TryRead(
                parser,
                ValuesReader.Clause,
                ClauseStart.Select,
                ReturningReader.Clause,
                ClauseStart.StatementEnd);
        }

        if (parser.CheckKeyword("VALUES"))
        {
            ReadSegments(
                segments,
                ValuesReader,
                ReturningReader);
            return new SqlInsertStatement(
               segments.GetValueOrDefault(IntoReader.Clause),
               segments.GetValueOrDefault(ValuesReader.Clause),
               null,
               segments.GetValueOrDefault(OutputReader.Clause),
               segments.GetValueOrDefault(ReturningReader.Clause),
			   withClause
			);
		}
        else if (parser.CheckKeyword("SELECT") || parser.CheckKeyword("WITH"))
        {
			var sourceQuery = parser.ParseStatement();
            ReadSegments(
                segments,
                ReturningReader);

			return new SqlInsertStatement(
			   segments.GetValueOrDefault(IntoReader.Clause),
               null,
			   sourceQuery,
			   segments.GetValueOrDefault(OutputReader.Clause),
			   segments.GetValueOrDefault(ReturningReader.Clause),
			   withClause
			);
		}
        throw new SqlParseException("Expected VALUES or SELECT clause in INSERT statement.");
		/*
		var targetSegment = IntoReader.TryRead([
            OutputReader.Clause,
            ValuesReader.Clause,
            ClauseStart.Select,
            ReturningReader.Clause,
            ClauseStart.StatementEnd]);
        SqlSegment? valuesSegment = null;
        SqlStatement? sourceQuery = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        int returningIndex = parser.FindClauseIndex("RETURNING");

        if (parser.TryConsumeKeyword("OUTPUT"))
        {
            outputSegment = OutputReader.TryRead([
                ValuesReader.Clause,
                ClauseStart.Select,
                ReturningReader.Clause,
                ClauseStart.StatementEnd]);
        }

        if (parser.CheckKeyword("VALUES"))
        {
            parser.ExpectKeyword("VALUES");
            valuesSegment = ValuesReader.TryRead(ReturningReader.Clause, ClauseStart.StatementEnd);
        }
        else if (parser.CheckKeyword("SELECT") || parser.CheckKeyword("WITH"))
        {
            int end = returningIndex >= 0 ? returningIndex : parser.Tokens.Count;
            var sourceTokens = parser.Tokens.GetRange(parser.Position, end - parser.Position);
            var subParser = new SqlParser(sourceTokens, parser.SyntaxOptions);
            sourceQuery = subParser.ParseStatement();
            subParser.ConsumeOptionalTerminator();
            subParser.EnsureEndOfInput();
            parser.Position = end;
        }

        if (parser.TryConsumeKeyword("RETURNING"))
        {
            returningSegment = ReturningReader.TryRead(ClauseStart.StatementEnd);
        }

        return new SqlInsertStatement(targetSegment, valuesSegment, sourceQuery, outputSegment, returningSegment, withClause);
        */
	}
}
