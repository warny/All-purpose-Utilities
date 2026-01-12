using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Parses SELECT statements by delegating token consumption to a shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class SelectStatementParser : StatementParserBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectStatementParser"/> class.
    /// </summary>
    /// <param name="parser">The underlying parser managing token access and helper utilities.</param>
    public SelectStatementParser(SqlParser parser) : base(parser) { }

    /// <summary>
    /// Parses a SELECT statement.
    /// </summary>
    /// <param name="withClause">The WITH clause associated with the SELECT statement, if any.</param>
    /// <returns>The built <see cref="SqlSelectStatement"/>.</returns>
    public SqlSelectStatement Parse(WithClause? withClause)
    {
        parser.ExpectKeyword("SELECT");
        bool isDistinct = parser.TryConsumeKeyword("DISTINCT");

		var segments = this.ReadSegments(
			SelectReader,
			FromReader,
            WhereReader,
            GroupByReader,
            HavingReader,
            OrderByReader,
            LimitReader,
            OffsetReader,
            ReturningReader,
            SetOperatorReader);

        return new SqlSelectStatement(
            segments.GetValueOrDefault(SelectReader.Clause),
			segments.GetValueOrDefault(FromReader.Clause),
			segments.GetValueOrDefault(WhereReader.Clause),
			segments.GetValueOrDefault(GroupByReader.Clause),
			segments.GetValueOrDefault(HavingReader.Clause),
			segments.GetValueOrDefault(OrderByReader.Clause),
			segments.GetValueOrDefault(LimitReader.Clause),
			segments.GetValueOrDefault(OffsetReader.Clause),
			segments.GetValueOrDefault(SetOperatorReader.Clause),
            withClause,
            isDistinct);
    }

}
