using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses DELETE statements relying on a shared <see cref="SqlParser"/> instance.
/// </summary>
internal sealed class DeleteStatementParser : StatementParserBase
{

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser providing token access.</param>
    public DeleteStatementParser(SqlParser parser) : base(parser) { }

    /// <summary>
    /// Parses a DELETE statement.
    /// </summary>
    /// <param name="withClause">The WITH clause bound to the statement, if present.</param>
    /// <returns>The parsed <see cref="SqlDeleteStatement"/>.</returns>
    public SqlDeleteStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("DELETE");

        var segments = new Dictionary<ClauseStart, SqlSegment>
        {
            [DeleteReader.Clause] = DeleteReader.TryRead(
                parser,
                FromReader.Clause,
                UsingReader.Clause,
                OutputReader.Clause,
                WhereReader.Clause,
                ReturningReader.Clause,
                ClauseStart.StatementEnd),
        };

        ReadSegments(
            segments,
            FromReader,
            UsingReader,
            OutputReader,
            WhereReader,
            ReturningReader);

        if (segments.GetValueOrDefault(DeleteReader.Clause) == null
            && segments.GetValueOrDefault(OutputReader.Clause) != null
            && segments.GetValueOrDefault(FromReader.Clause) is SqlSegment fromSegment)
        {
            segments[DeleteReader.Clause] = new SqlSegment("Target", fromSegment.Parts, parser.SyntaxOptions);
        }

        return new SqlDeleteStatement(
            segments.GetValueOrDefault(DeleteReader.Clause),
            segments.GetValueOrDefault(FromReader.Clause),
            segments.GetValueOrDefault(UsingReader.Clause),
            segments.GetValueOrDefault(WhereReader.Clause),
            segments.GetValueOrDefault(OutputReader.Clause),
            segments.GetValueOrDefault(ReturningReader.Clause),
            withClause);

    }

}
