using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Sql Part Reader Interface
/// </summary>
internal interface IPartReader
{
	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the GROUP BY clause.
	/// </summary>
	ClauseKeywordDefinition KeywordDefinition { get; }

    /// <summary>
    /// Get the keyword sequences that identify the start of the clause.
    /// </summary>
    IReadOnlyList<IReadOnlyList<string>> KeywordSequences => KeywordDefinition.KeywordSequences;
    /// <summary>
    /// Reads the clause when the current token matches the clause-start keyword.
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="clauseTerminators"></param>
    /// <returns></returns>
    SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators);
    
    /// <summary>
    /// Gets the clause identifier.
    /// </summary>
    ClauseStart Clause => KeywordDefinition.Clause;

    /// <summary>
    /// Gets the name of the part represented by this instance.
    /// </summary>
	string PartName => Clause.ToString();


}

/// <summary>
/// Sql Part Reader Interface
/// </summary>
internal interface IPartReader<T> : IPartReader
    where T : SqlStatementPart
{
	/// <summary>
	/// Gets the factory that creates a typed part from the parsed FROM segment.
	/// </summary>
	Func<SqlSegment, T> PartFactory { get; }
}



/// <summary>
/// Reads the SELECT clause expressions for a statement.
/// </summary>
[DebuggerDisplay("SELECT")]
internal sealed class SelectPartReader : IPartReader<SelectPart>
{
	public static IPartReader<SelectPart> Singleton { get; } = new SelectPartReader();
	
    public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Select, 
            [
                ["SELECT"],
                ["WITH"]
            ]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

    public Func<SqlSegment, SelectPart> PartFactory { get; } = segment => new SelectPart(segment);

	/// <summary>
	/// Initializes a new instance of the <see cref="SelectPartReader"/> class.
	/// </summary>
	/// <param name="parser">The parser supplying token access.</param>
	private SelectPartReader() { }

    /// <summary>
    /// Reads the SELECT clause up to the next statement section.
    /// </summary>
    /// <returns>The parsed SELECT segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
        var expressionListReader = new ExpressionListReader(parser);
        var expressions = expressionListReader.ReadExpressions(
            "SelectExpr",
            true,
            [..clauseTerminators]);

        return BuildExpressionListSegment(parser, "Select", expressions);
    }

    private SqlSegment BuildExpressionListSegment(SqlParser parser, string name, IReadOnlyList<ExpressionReadResult> expressions)
    {
        var tokens = new List<SqlToken>();
        for (int i = 0; i < expressions.Count; i++)
        {
            if (i > 0)
            {
                tokens.Add(new SqlToken(",", ",", false, false));
            }

            tokens.AddRange(expressions[i].Tokens);
        }

        return parser.BuildSegment(name, tokens);
    }
}

/// <summary>
/// Reads table sources for a FROM clause.
/// </summary>
[DebuggerDisplay("FROM")]
internal sealed class FromPartReader : IPartReader<FromPart>
{
	public static IPartReader<FromPart> Singleton { get; } = new FromPartReader();

	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.From, ["FROM"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed FROM segment.
	/// </summary>
	public Func<SqlSegment, FromPart> PartFactory { get; } = segment => new FromPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="FromPartReader"/> class.
    /// </summary>
    private FromPartReader() { }

    /// <summary>
    /// Attempts to read a FROM clause when present.
    /// </summary>
    /// <returns>The parsed FROM segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
		var tableListReader = new TableListReader(parser);

		if (!parser.TryConsumeKeyword("FROM")) return null;

        var tables = tableListReader.ReadTables(
            "FromTable",
            [..clauseTerminators]);

        return BuildDelimitedSegment(parser, "From", tables);
    }

    private SqlSegment BuildDelimitedSegment(SqlParser parser, string name, IReadOnlyList<SqlSegment> segments)
    {
        var parts = new List<ISqlSegmentPart>();
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                parts.Add(new SqlTokenPart(","));
            }

            foreach (var part in segments[i].Parts)
            {
                parts.Add(part);
            }
        }

        return new SqlSegment(name, parts, parser.SyntaxOptions);
    }
}

/// <summary>
/// Reads table sources for a FROM clause.
/// </summary>
[DebuggerDisplay("USING")]
internal sealed class UsingPartReader : IPartReader<FromPart>
{
	public static IPartReader<FromPart> Singleton { get; } = new UsingPartReader();

	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Using, ["USING"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed FROM segment.
	/// </summary>
	public Func<SqlSegment, FromPart> PartFactory { get; } = segment => new FromPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="FromPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private UsingPartReader() { }

	/// <summary>
	/// Attempts to read a FROM clause when present.
	/// </summary>
	/// <returns>The parsed FROM segment when found; otherwise, <c>null</c>.</returns>
	public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
		var tableListReader = new TableListReader(parser);
		if (!parser.TryConsumeKeyword("USING")) return null;

		var tables = tableListReader.ReadTables(
			"UsingTable",
			[.. clauseTerminators]);

		return BuildDelimitedSegment(parser, "From", tables);
	}

	private SqlSegment BuildDelimitedSegment(SqlParser parser, string name, IReadOnlyList<SqlSegment> segments)
	{
		var parts = new List<ISqlSegmentPart>();
		for (int i = 0; i < segments.Count; i++)
		{
			if (i > 0)
			{
				parts.Add(new SqlTokenPart(","));
			}

			foreach (var part in segments[i].Parts)
			{
				parts.Add(part);
			}
		}

		return new SqlSegment(name, parts, parser.SyntaxOptions);
	}
}

/// <summary>
/// Reads an INTO clause target for SELECT or INSERT statements.
/// </summary>
[DebuggerDisplay("INTO")]
internal sealed class IntoPartReader : IPartReader<IntoPart>
{
	public static IPartReader<IntoPart> Singleton { get; } = new IntoPartReader();

	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Into, ["INTO"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

    public Func<SqlSegment, IntoPart> PartFactory { get; } = segment => new IntoPart(segment);

	/// <summary>
	/// Initializes a new instance of the <see cref="IntoPartReader"/> class.
	/// </summary>
	/// <param name="parser">The parser supplying token access.</param>
	private IntoPartReader() { }

    /// <summary>
    /// Reads an INTO target when the current token matches the INTO keyword.
    /// </summary>
    /// <returns>The parsed INTO/target segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens([..clauseTerminators]);
        if (tokens == null || tokens.Count == 0) return null;
        return parser.BuildSegment("Target", tokens);
    }
}

/// <summary>
/// Reads a WHERE predicate.
/// </summary>
[DebuggerDisplay("WHERE")]
internal sealed class WherePartReader : IPartReader<WherePart>
{
	public static IPartReader<WherePart> Singleton { get; } = new WherePartReader();

	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Where, ["WHERE"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

    public Func<SqlSegment, WherePart> PartFactory { get; } = clause => new WherePart(clause);

    /// <summary>
    /// Initializes a new instance of the <see cref="WherePartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private WherePartReader() { }

    /// <summary>
    /// Attempts to read a WHERE clause when present.
    /// </summary>
    /// <returns>The parsed WHERE predicate when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var predicateReader = new PredicateReader(parser);
        return predicateReader.ReadPredicate("Where",  [..clauseTerminators]);
    }
}

/// <summary>
/// Reads GROUP BY expressions.
/// </summary>
[DebuggerDisplay("GROUP BY")]
internal sealed class GroupByPartReader : IPartReader<GroupByPart>
{
	public static IPartReader<GroupByPart> Singleton { get; } = new GroupByPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the GROUP BY clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.GroupBy, [["GROUP", "BY"]]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed GROUP BY segment.
	/// </summary>
	public Func<SqlSegment, GroupByPart> PartFactory { get; } = segment => new GroupByPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupByPartReader"/> class.
    /// </summary>
    private GroupByPartReader() { }

    /// <summary>
    /// Attempts to read a GROUP BY clause when present.
    /// </summary>
    /// <returns>The parsed GROUP BY segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var expressionListReader = new ExpressionListReader(parser);
        var expressions = expressionListReader.ReadExpressions("GroupByExpr", false, [.. clauseTerminators]);

        return BuildExpressionListSegment(parser, "GroupBy", expressions);
    }

    private SqlSegment BuildExpressionListSegment(SqlParser parser, string name, IReadOnlyList<ExpressionReadResult> expressions)
    {
        var tokens = new List<SqlToken>();
        for (int i = 0; i < expressions.Count; i++)
        {
            if (i > 0)
            {
                tokens.Add(new SqlToken(",", ",", false, false));
            }

            tokens.AddRange(expressions[i].Tokens);
        }

        return parser.BuildSegment(name, tokens);
    }
}

/// <summary>
/// Reads a HAVING predicate.
/// </summary>
[DebuggerDisplay("HAVING")]
internal sealed class HavingPartReader : IPartReader<HavingPart>
{
	public static IPartReader<HavingPart> Singleton { get; } = new HavingPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the HAVING clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Having, ["HAVING"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed HAVING segment.
	/// </summary>
	public Func<SqlSegment, HavingPart> PartFactory { get; } = segment => new HavingPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="HavingPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private HavingPartReader() { }

    /// <summary>
    /// Attempts to read a HAVING clause when present.
    /// </summary>
    /// <returns>The parsed HAVING segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var predicateReader = new PredicateReader(parser);
        return predicateReader.ReadPredicate("Having", [.. clauseTerminators]);
    }
}

/// <summary>
/// Reads ORDER BY expressions.
/// </summary>
[DebuggerDisplay("ORDER BY")]
internal sealed class OrderByPartReader : IPartReader<OrderByPart>
{
	public static IPartReader<OrderByPart> Singleton { get; } = new OrderByPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the ORDER BY clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.OrderBy, ["ORDER", "BY"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed ORDER BY segment.
	/// </summary>
	public Func<SqlSegment, OrderByPart> PartFactory { get; } = segment => new OrderByPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private OrderByPartReader() { }

    /// <summary>
    /// Attempts to read an ORDER BY clause when present.
    /// </summary>
    /// <returns>The parsed ORDER BY segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var expressionListReader = new ExpressionListReader(parser);
        var expressions = expressionListReader.ReadExpressions(
            "OrderByExpr",
            false,
            [..clauseTerminators]);

        return BuildExpressionListSegment(parser, "OrderBy", expressions);
    }

    private SqlSegment BuildExpressionListSegment(SqlParser parser, string name, IReadOnlyList<ExpressionReadResult> expressions)
    {
        var tokens = new List<SqlToken>();
        for (int i = 0; i < expressions.Count; i++)
        {
            if (i > 0)
            {
                tokens.Add(new SqlToken(",", ",", false, false));
            }

            tokens.AddRange(expressions[i].Tokens);
        }

        return parser.BuildSegment(name, tokens);
    }
}

/// <summary>
/// Reads a LIMIT clause when present.
/// </summary>
[DebuggerDisplay("LIMIT")]
internal sealed class LimitPartReader : IPartReader<LimitPart>
{
	public static IPartReader<LimitPart> Singleton { get; } = new LimitPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the LIMIT clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Limit, ["LIMIT"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed LIMIT segment.
	/// </summary>
	public Func<SqlSegment, LimitPart> PartFactory { get; } = segment => new LimitPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private LimitPartReader() { }

    /// <summary>
    /// Attempts to read a LIMIT clause when present.
    /// </summary>
    /// <returns>The parsed LIMIT segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens([..clauseTerminators]);
        return parser.BuildSegment("Limit", tokens);
    }
}

/// <summary>
/// Reads an OFFSET clause when present.
/// </summary>
[DebuggerDisplay("OFFSET")]
internal sealed class OffsetPartReader : IPartReader<OffsetPart>
{
	public static IPartReader<OffsetPart> Singleton { get; } = new OffsetPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the OFFSET clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Offset, ["OFFSET"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed OFFSET segment.
	/// </summary>
	public Func<SqlSegment, OffsetPart> PartFactory { get; } = segment => new OffsetPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    private OffsetPartReader() { }

    /// <summary>
    /// Attempts to read an OFFSET clause when present.
    /// </summary>
    /// <returns>The parsed OFFSET segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens(
            [..clauseTerminators]);
        return parser.BuildSegment("Offset", tokens);
    }
}

/// <summary>
/// Reads VALUES content for INSERT statements.
/// </summary>
[DebuggerDisplay("VALUES")]
internal sealed class ValuesPartReader : IPartReader<ValuesPart>
{
	public static IPartReader<ValuesPart> Singleton { get; } = new ValuesPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the VALUES clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Values, ["VALUES"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

    public Func<SqlSegment, ValuesPart> PartFactory { get; } = part => new ValuesPart(part);

    /// <summary>
    /// Initializes a new instance of the <see cref="ValuesPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public ValuesPartReader() { }

    /// <summary>
    /// Reads the VALUES clause when the VALUES keyword has already been consumed.
    /// </summary>
    /// <returns>The parsed VALUES segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens([..clauseTerminators]);
        return parser.BuildSegment("Values", tokens);
    }
}

/// <summary>
/// Reads OUTPUT clauses for DML statements.
/// </summary>
[DebuggerDisplay("OUTPUT")]
internal sealed class OutputPartReader : IPartReader
{
	public static IPartReader Singleton { get; } = new OutputPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the OUTPUT clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Output, ["OUTPUT"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Initializes a new instance of the <see cref="OutputPartReader"/> class.
	/// </summary>
	private OutputPartReader() { }

    /// <summary>
    /// Reads an OUTPUT clause after the OUTPUT keyword has been consumed.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    /// <param name="clauseTerminators">Clause boundaries that end the OUTPUT clause.</param>
    /// <returns>The parsed OUTPUT segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
        var expressionListReader = new ExpressionListReader(parser);
		var expressions = expressionListReader.ReadExpressions("OutputExpr", true, [..clauseTerminators]);
        return BuildExpressionListSegment(parser, "Output", expressions);
    }

    private SqlSegment BuildExpressionListSegment(SqlParser parser, string name, IReadOnlyList<ExpressionReadResult> expressions, bool mandatory = false)
    {
        if (!mandatory && expressions.Count == 0) return null;

		var tokens = new List<SqlToken>();
        for (int i = 0; i < expressions.Count; i++)
        {
            if (i > 0)
            {
                tokens.Add(new SqlToken(",", ",", false, false));
            }

            tokens.AddRange(expressions[i].Tokens);
        }

        return parser.BuildSegment(name, tokens);
    }
}

/// <summary>
/// Reads RETURNING clauses.
/// </summary>
[DebuggerDisplay("RETURNING")]
internal sealed class ReturningPartReader : IPartReader
{
	public static IPartReader Singleton { get; } = new ReturningPartReader();

	/// <summary>
	/// Gets the keyword metadata describing how to detect the start of the RETURNING clause.
	/// </summary>
	public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Returning, ["RETURNING"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Initializes a new instance of the <see cref="ReturningPartReader"/> class.
	/// </summary>
	/// <param name="parser">The parser supplying token access.</param>
	private ReturningPartReader() { }

    /// <summary>
    /// Reads a RETURNING clause after the keyword has been consumed.
    /// </summary>
    /// <returns>The parsed RETURNING segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
    {
		var tokens = parser.ReadSectionTokens([..clauseTerminators]);
        return parser.BuildSegment("Returning", tokens);
    }
}

/// <summary>
/// Reads trailing set operator clauses such as UNION.
/// </summary>
[DebuggerDisplay("UNION|EXCEPT|INTERSECT")]
internal sealed class SetOperatorPartReader : IPartReader<TailPart>
{
	public static IPartReader<TailPart> Singleton { get; } = new SetOperatorPartReader();

	private static readonly string[] SetOperators =
    {
        "UNION",
        "EXCEPT",
        "INTERSECT",
    };

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of set operator clauses.
    /// </summary>
    public ClauseKeywordDefinition KeywordDefinition { get; } = ClauseKeywordDefinition.FromKeywords(
        ClauseStart.SetOperator,
        ["UNION"],
        ["EXCEPT"],
        ["INTERSECT"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed tail segment.
	/// </summary>
	public Func<SqlSegment, TailPart> PartFactory { get; } = segment => new TailPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="SetOperatorPartReader"/> class.
    /// </summary>
    private SetOperatorPartReader() { }

    /// <summary>
    /// Attempts to read a trailing set operator clause when present.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    /// <returns>The parsed set operator segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
        foreach (string keyword in SetOperators)
        {
            if (parser.TryConsumeSegmentKeyword(keyword, out var consumedTokens))
            {
                var tokens = new List<SqlToken>(consumedTokens);
                tokens.AddRange(parser.ReadSectionTokens(ClauseStart.StatementEnd));
                return parser.BuildSegment("Tail", tokens);
            }
        }

        return null;
    }
}

/// <summary>
/// Reads the optional DELETE target preceding FROM.
/// </summary>
internal sealed class DeletePartReader : IPartReader<DeletePart>
{
    public static IPartReader<DeletePart> Singleton { get; } = new DeletePartReader();

    public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Delete, ["DELETE"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed DELETE target segment.
	/// </summary>
	public Func<SqlSegment, DeletePart> PartFactory { get; } = segment => new DeletePart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePartReader"/> class.
    /// </summary>
    private DeletePartReader() { }


	/// <summary>
	/// Attempts to read a DELETE target when the FROM keyword has not yet been encountered.
	/// </summary>
    /// <param name="parser">The parser supplying token access.</param>
	/// <returns>The parsed DELETE target when present; otherwise, <c>null</c>.</returns>
	public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
        var tokens = new List<SqlToken>();
        while (!parser.IsAtEnd && !parser.CheckKeyword("FROM") && parser.Peek().Text != ";")
        {
            tokens.Add(parser.Read());
        }

        return parser.BuildSegment("Target", tokens);
    }
}

/// <summary>
/// Reads the UPDATE target prior to the SET keyword.
/// </summary>
internal sealed class UpdatePartReader : IPartReader
{
	public static IPartReader Singleton { get; } = new UpdatePartReader();

	/// <summary>
	/// Gets the factory that creates a typed part from the parsed UPDATE target segment.
	/// </summary>
	public Func<SqlSegment, UpdatePart> PartFactory { get; } = segment => new UpdatePart(segment);

    public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Update, ["UPDATE"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Initializes a new instance of the <see cref="UpdatePartReader"/> class.
	/// </summary>
	private UpdatePartReader() { }

    /// <summary>
    /// Reads the UPDATE target until the SET keyword is encountered.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    /// <returns>The parsed UPDATE target segment.</returns>
    public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
        var tokens = new List<SqlToken>();
        while (!parser.IsAtEnd && !parser.CheckKeyword("SET") && parser.Peek().Text != ";")
        {
            tokens.Add(parser.Read());
        }

        return parser.BuildSegment("Target", tokens);
    }
}

/// <summary>
/// Reads the SET clause content for UPDATE statements.
/// </summary>
internal sealed class SetPartReader : IPartReader
{
	public static IPartReader Singleton { get; } = new SetPartReader();

	/// <summary>
	/// Initializes a new instance of the <see cref="SetPartReader"/> class.
	/// </summary>
	private SetPartReader() { }

    public ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Set, ["SET"]);

	/// <inheritdoc />
	public ClauseStart Clause => KeywordDefinition.Clause;

	/// <inheritdoc />
	public string PartName => Clause.ToString();

	/// <summary>
	/// Reads the SET clause after the SET keyword has been consumed.
	/// </summary>
	/// <param name="parser">The parser supplying token access.</param>
	/// <returns>The parsed SET segment.</returns>
	public SqlSegment? TryRead(SqlParser parser, params IEnumerable<ClauseStart> clauseTerminators)
	{
        var tokens = parser.ReadSectionTokens([..clauseTerminators]);
        return parser.BuildSegment("Set", tokens);
    }
}
