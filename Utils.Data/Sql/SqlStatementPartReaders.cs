using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Reads the SELECT clause expressions for a statement.
/// </summary>
internal sealed class SelectPartReader
{
    private readonly SqlParser parser;
    private readonly ExpressionListReader expressionListReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the SELECT clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } = ClauseKeywordDefinition.FromKeywords(
        ClauseStart.Select,
        new[] { "SELECT" },
        new[] { "WITH" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public SelectPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        expressionListReader = new ExpressionListReader(this.parser);
    }

    /// <summary>
    /// Reads the SELECT clause up to the next statement section.
    /// </summary>
    /// <returns>The parsed SELECT segment.</returns>
    public SqlSegment ReadSelectPart(params ClauseStart[] clauseTerminators)
    {
        var expressions = expressionListReader.ReadExpressions(
            "SelectExpr",
            true,
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.From,
                    ClauseStart.Where,
                    ClauseStart.GroupBy,
                    ClauseStart.Having,
                    ClauseStart.OrderBy,
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });

        return BuildExpressionListSegment("Select", expressions);
    }

    private SqlSegment BuildExpressionListSegment(string name, IReadOnlyList<ExpressionReadResult> expressions)
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
internal sealed class FromPartReader
{
    private readonly SqlParser parser;
    private readonly TableListReader tableListReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the FROM clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.From, new[] { "FROM" });

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "From";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed FROM segment.
    /// </summary>
    public static Func<SqlSegment, FromPart> PartFactory { get; } = segment => new FromPart(segment);

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="FromPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public FromPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        tableListReader = new TableListReader(this.parser);
    }

    /// <summary>
    /// Attempts to read a FROM clause when present.
    /// </summary>
    /// <returns>The parsed FROM segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadFromPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeKeyword("FROM"))
        {
            return null;
        }

        var tables = tableListReader.ReadTables(
            "FromTable",
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Where,
                    ClauseStart.GroupBy,
                    ClauseStart.Having,
                    ClauseStart.OrderBy,
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Output,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });

        return BuildDelimitedSegment("From", tables);
    }

    private SqlSegment BuildDelimitedSegment(string name, IReadOnlyList<SqlSegment> segments)
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
internal sealed class IntoPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the INTO clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Into, new[] { "INTO" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntoPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public IntoPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads an INTO target when the current token matches the INTO keyword.
    /// </summary>
    /// <returns>The parsed INTO/target segment.</returns>
    public SqlSegment ReadIntoTarget(params ClauseStart[] clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens(
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Output,
                    ClauseStart.Values,
                    ClauseStart.Select,
                    ClauseStart.Returning,
                    ClauseStart.StatementEnd,
                });
        return parser.BuildSegment("Target", tokens);
    }
}

/// <summary>
/// Reads a WHERE predicate.
/// </summary>
internal sealed class WherePartReader
{
    private readonly SqlParser parser;
    private readonly PredicateReader predicateReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the WHERE clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Where, new[] { "WHERE" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Where";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed WHERE segment.
    /// </summary>
    public static Func<SqlSegment, WherePart> PartFactory { get; } = segment => new WherePart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="WherePartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public WherePartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        predicateReader = new PredicateReader(this.parser);
    }

    /// <summary>
    /// Attempts to read a WHERE clause when present.
    /// </summary>
    /// <returns>The parsed WHERE predicate when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadWherePart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeKeyword("WHERE"))
        {
            return null;
        }

        return predicateReader.ReadPredicate(
            "Where",
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.GroupBy,
                    ClauseStart.Having,
                    ClauseStart.OrderBy,
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });
    }
}

/// <summary>
/// Reads GROUP BY expressions.
/// </summary>
internal sealed class GroupByPartReader
{
    private readonly SqlParser parser;
    private readonly ExpressionListReader expressionListReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the GROUP BY clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.GroupBy, new[] { "GROUP", "BY" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "GroupBy";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed GROUP BY segment.
    /// </summary>
    public static Func<SqlSegment, GroupByPart> PartFactory { get; } = segment => new GroupByPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupByPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public GroupByPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        expressionListReader = new ExpressionListReader(this.parser);
    }

    /// <summary>
    /// Attempts to read a GROUP BY clause when present.
    /// </summary>
    /// <returns>The parsed GROUP BY segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadGroupByPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeSegmentKeyword("GROUP BY", out _))
        {
            return null;
        }

        var expressions = expressionListReader.ReadExpressions(
            "GroupByExpr",
            false,
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Having,
                    ClauseStart.OrderBy,
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });

        return BuildExpressionListSegment("GroupBy", expressions);
    }

    private SqlSegment BuildExpressionListSegment(string name, IReadOnlyList<ExpressionReadResult> expressions)
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
internal sealed class HavingPartReader
{
    private readonly SqlParser parser;
    private readonly PredicateReader predicateReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the HAVING clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Having, new[] { "HAVING" });

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Having";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed HAVING segment.
    /// </summary>
    public static Func<SqlSegment, HavingPart> PartFactory { get; } = segment => new HavingPart(segment);

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="HavingPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public HavingPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        predicateReader = new PredicateReader(this.parser);
    }

    /// <summary>
    /// Attempts to read a HAVING clause when present.
    /// </summary>
    /// <returns>The parsed HAVING segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadHavingPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeKeyword("HAVING"))
        {
            return null;
        }

        return predicateReader.ReadPredicate(
            "Having",
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.OrderBy,
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });
    }
}

/// <summary>
/// Reads ORDER BY expressions.
/// </summary>
internal sealed class OrderByPartReader
{
    private readonly SqlParser parser;
    private readonly ExpressionListReader expressionListReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the ORDER BY clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.OrderBy, new[] { "ORDER", "BY" });

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "OrderBy";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed ORDER BY segment.
    /// </summary>
    public static Func<SqlSegment, OrderByPart> PartFactory { get; } = segment => new OrderByPart(segment);

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public OrderByPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        expressionListReader = new ExpressionListReader(this.parser);
    }

    /// <summary>
    /// Attempts to read an ORDER BY clause when present.
    /// </summary>
    /// <returns>The parsed ORDER BY segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadOrderByPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeSegmentKeyword("ORDER BY", out _))
        {
            return null;
        }

        var expressions = expressionListReader.ReadExpressions(
            "OrderByExpr",
            false,
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Limit,
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });

        return BuildExpressionListSegment("OrderBy", expressions);
    }

    private SqlSegment BuildExpressionListSegment(string name, IReadOnlyList<ExpressionReadResult> expressions)
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
internal sealed class LimitPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the LIMIT clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Limit, new[] { "LIMIT" });

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Limit";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed LIMIT segment.
    /// </summary>
    public static Func<SqlSegment, LimitPart> PartFactory { get; } = segment => new LimitPart(segment);

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public LimitPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Attempts to read a LIMIT clause when present.
    /// </summary>
    /// <returns>The parsed LIMIT segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadLimitPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeKeyword("LIMIT"))
        {
            return null;
        }

        var tokens = parser.ReadSectionTokens(
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Offset,
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });
        return parser.BuildSegment("Limit", tokens);
    }
}

/// <summary>
/// Reads an OFFSET clause when present.
/// </summary>
internal sealed class OffsetPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the OFFSET clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Offset, new[] { "OFFSET" });

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Offset";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed OFFSET segment.
    /// </summary>
    public static Func<SqlSegment, OffsetPart> PartFactory { get; } = segment => new OffsetPart(segment);

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public OffsetPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Attempts to read an OFFSET clause when present.
    /// </summary>
    /// <returns>The parsed OFFSET segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadOffsetPart(params ClauseStart[] clauseTerminators)
    {
        if (!parser.TryConsumeKeyword("OFFSET"))
        {
            return null;
        }

        var tokens = parser.ReadSectionTokens(
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Returning,
                    ClauseStart.Using,
                    ClauseStart.SetOperator,
                    ClauseStart.StatementEnd,
                });
        return parser.BuildSegment("Offset", tokens);
    }
}

/// <summary>
/// Reads VALUES content for INSERT statements.
/// </summary>
internal sealed class ValuesPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the VALUES clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Values, new[] { "VALUES" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValuesPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public ValuesPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads the VALUES clause when the VALUES keyword has already been consumed.
    /// </summary>
    /// <returns>The parsed VALUES segment.</returns>
    public SqlSegment ReadValuesPart(params ClauseStart[] clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens(
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Returning,
                    ClauseStart.StatementEnd,
                });
        return parser.BuildSegment("Values", tokens);
    }
}

/// <summary>
/// Reads OUTPUT clauses for DML statements.
/// </summary>
internal sealed class OutputPartReader
{
    private readonly SqlParser parser;
    private readonly ExpressionListReader expressionListReader;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the OUTPUT clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Output, new[] { "OUTPUT" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutputPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public OutputPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        expressionListReader = new ExpressionListReader(this.parser);
    }

    /// <summary>
    /// Reads an OUTPUT clause after the OUTPUT keyword has been consumed.
    /// </summary>
    /// <param name="segmentName">The logical name to give the resulting segment.</param>
    /// <param name="terminators">Clause boundaries that end the OUTPUT clause.</param>
    /// <returns>The parsed OUTPUT segment.</returns>
    public SqlSegment ReadOutputPart(string segmentName, params ClauseStart[] clauseTerminators)
    {
        var expressions = expressionListReader.ReadExpressions(
            segmentName + "Expr",
            true,
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.Returning,
                    ClauseStart.StatementEnd,
                });
        return BuildExpressionListSegment(segmentName, expressions);
    }

    private SqlSegment BuildExpressionListSegment(string name, IReadOnlyList<ExpressionReadResult> expressions)
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
/// Reads RETURNING clauses.
/// </summary>
internal sealed class ReturningPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of the RETURNING clause.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } =
        ClauseKeywordDefinition.FromKeywords(ClauseStart.Returning, new[] { "RETURNING" });

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturningPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public ReturningPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads a RETURNING clause after the keyword has been consumed.
    /// </summary>
    /// <returns>The parsed RETURNING segment.</returns>
    public SqlSegment ReadReturningPart(params ClauseStart[] clauseTerminators)
    {
        var tokens = parser.ReadSectionTokens(
            clauseTerminators.Length > 0
                ? clauseTerminators
                : new[]
                {
                    ClauseStart.StatementEnd,
                });
        return parser.BuildSegment("Returning", tokens);
    }
}

/// <summary>
/// Reads trailing set operator clauses such as UNION.
/// </summary>
internal sealed class SetOperatorPartReader
{
    private static readonly string[] SetOperators =
    {
        "UNION",
        "EXCEPT",
        "INTERSECT",
    };

    /// <summary>
    /// Gets the keyword metadata describing how to detect the start of set operator clauses.
    /// </summary>
    public static ClauseKeywordDefinition KeywordDefinition { get; } = ClauseKeywordDefinition.FromKeywords(
        ClauseStart.SetOperator,
        new[] { "UNION" },
        new[] { "EXCEPT" },
        new[] { "INTERSECT" });

    private readonly SqlParser parser;

    /// <summary>
    /// Gets the clause-start keyword that activates the reader.
    /// </summary>
    public ClauseStart ClauseKeyword => KeywordDefinition.ClauseKeyword;

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Tail";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed tail segment.
    /// </summary>
    public static Func<SqlSegment, TailPart> PartFactory { get; } = segment => new TailPart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="SetOperatorPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public SetOperatorPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Attempts to read a trailing set operator clause when present.
    /// </summary>
    /// <returns>The parsed set operator segment when found; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadTailPart()
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
internal sealed class DeletePartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Target";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed DELETE target segment.
    /// </summary>
    public static Func<SqlSegment, DeletePart> PartFactory { get; } = segment => new DeletePart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public DeletePartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Attempts to read a DELETE target when the FROM keyword has not yet been encountered.
    /// </summary>
    /// <returns>The parsed DELETE target when present; otherwise, <c>null</c>.</returns>
    public SqlSegment? TryReadDeleteTarget()
    {
        if (parser.CheckKeyword("FROM"))
        {
            return null;
        }

        var tokens = new List<SqlToken>();
        while (!parser.IsAtEnd && !parser.CheckKeyword("FROM") && parser.Peek().Text != ";")
        {
            tokens.Add(parser.Read());
        }

        return tokens.Count == 0 ? null : parser.BuildSegment("Target", tokens);
    }
}

/// <summary>
/// Reads the UPDATE target prior to the SET keyword.
/// </summary>
internal sealed class UpdatePartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Gets the name of the part produced by the reader.
    /// </summary>
    public static string PartName => "Target";

    /// <summary>
    /// Gets the factory that creates a typed part from the parsed UPDATE target segment.
    /// </summary>
    public static Func<SqlSegment, UpdatePart> PartFactory { get; } = segment => new UpdatePart(segment);

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public UpdatePartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads the UPDATE target until the SET keyword is encountered.
    /// </summary>
    /// <returns>The parsed UPDATE target segment.</returns>
    public SqlSegment ReadUpdateTarget()
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
internal sealed class SetPartReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetPartReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public SetPartReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads the SET clause after the SET keyword has been consumed.
    /// </summary>
    /// <returns>The parsed SET segment.</returns>
    public SqlSegment ReadSetPart()
    {
        var tokens = parser.ReadSectionTokens(
            ClauseStart.Output,
            ClauseStart.From,
            ClauseStart.Where,
            ClauseStart.Returning,
            ClauseStart.StatementEnd);
        return parser.BuildSegment("Set", tokens);
    }
}
