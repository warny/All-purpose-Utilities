using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses UPDATE statements using the shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class UpdateStatementParser : StatementParserBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser for token management.</param>
    public UpdateStatementParser(SqlParser parser) : base(parser) { }

    /// <summary>
    /// Parses an UPDATE statement.
    /// </summary>
    /// <param name="withClause">The optional WITH clause attached to the statement.</param>
    /// <returns>The parsed <see cref="SqlUpdateStatement"/>.</returns>
    public SqlUpdateStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("UPDATE");

        var segments = new Dictionary<ClauseStart, SqlSegment>
        {
            [SpdateTargetReader.Clause] = SpdateTargetReader.TryRead(
                parser,
                SetReader.Clause,
                OutputReader.Clause,
                FromReader.Clause,
                WhereReader.Clause,
                ReturningReader.Clause,
                ClauseStart.StatementEnd),
        };

        ReadSegments(
            segments,
            SetReader,
            OutputReader,
            FromReader,
            WhereReader,
            ReturningReader);

        return new SqlUpdateStatement(
            segments.GetValueOrDefault(SpdateTargetReader.Clause),
			segments.GetValueOrDefault(SetReader.Clause),
			segments.GetValueOrDefault(FromReader.Clause),
			segments.GetValueOrDefault(WhereReader.Clause),
            segments.GetValueOrDefault(OutputReader.Clause),
            segments.GetValueOrDefault(ReturningReader.Clause), 
            withClause);
    }
}
