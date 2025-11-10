using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Provides facilities to analyse SQL statements and reconstruct them from an abstract representation.
/// </summary>
public static class SqlQueryAnalyzer
{
    /// <summary>
    /// Parses the provided SQL text into an abstract representation.
    /// </summary>
    /// <param name="sql">The SQL statement to parse.</param>
    /// <param name="syntaxOptions">Tokenizer and parameter options applied to the parsing process.</param>
    /// <returns>A <see cref="SqlQuery"/> representing the parsed statement.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
    /// <exception cref="SqlParseException">Thrown when the SQL text cannot be parsed.</exception>
    public static SqlQuery Parse(string sql, SqlSyntaxOptions? syntaxOptions = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL text cannot be null or whitespace.", nameof(sql));
        }

        syntaxOptions ??= SqlSyntaxOptions.Default;
        var parser = SqlParser.Create(sql, syntaxOptions);
        SqlStatement statement = parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();
        return new SqlQuery(statement, syntaxOptions);
    }
}

/// <summary>
/// Represents the root of an analysed SQL statement.
/// </summary>
public sealed class SqlQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlQuery"/> class.
    /// </summary>
    /// <param name="rootStatement">The root statement of the SQL query.</param>
    /// <param name="syntaxOptions">Syntax options describing identifier handling for the query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootStatement"/> is null.</exception>
    public SqlQuery(SqlStatement rootStatement, SqlSyntaxOptions? syntaxOptions = null)
    {
        RootStatement = rootStatement ?? throw new ArgumentNullException(nameof(rootStatement));
        SyntaxOptions = syntaxOptions ?? RootStatement.SyntaxOptions;
    }

    /// <summary>
    /// Gets the root statement of the query.
    /// </summary>
    public SqlStatement RootStatement { get; }

    /// <summary>
    /// Gets the syntax options associated with the query.
    /// </summary>
    public SqlSyntaxOptions SyntaxOptions { get; }

    /// <summary>
    /// Gets all statements contained in the query, including the root and nested statements such as CTEs or subqueries.
    /// The collection is rebuilt on each access so that modifications applied to the query tree are reflected immediately.
    /// </summary>
    public IReadOnlyList<SqlStatement> AllStatements => RootStatement.EnumerateStatements().ToList().AsReadOnly();

    /// <summary>
    /// Reconstructs the SQL text from the analysed structure.
    /// </summary>
    /// <param name="options">Formatting options controlling the SQL output.</param>
    /// <returns>A SQL string equivalent to the parsed statement.</returns>
    public string ToSql(SqlFormattingOptions? options = null) => RootStatement.ToSql(options);
}

/// <summary>
/// Represents a parsed SQL statement.
/// </summary>
public abstract class SqlStatement
{
    private readonly List<SqlSegment> segments;
    private readonly ReadOnlyCollection<SqlSegment> readOnlySegments;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlStatement"/> class.
    /// </summary>
    /// <param name="segments">The segments that compose the statement.</param>
    /// <param name="withClause">The optional CTE definitions attached to the statement.</param>
    /// <param name="syntaxOptions">Syntax options governing identifier parsing for the statement.</param>
    protected SqlStatement(IEnumerable<SqlSegment> segments, WithClause? withClause, SqlSyntaxOptions? syntaxOptions = null)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        var segmentList = segments.ToList();
        SyntaxOptions = syntaxOptions ?? segmentList.FirstOrDefault(s => s != null)?.SyntaxOptions ?? SqlSyntaxOptions.Default;

        this.segments = new List<SqlSegment>();
        readOnlySegments = new ReadOnlyCollection<SqlSegment>(this.segments);

        foreach (var segment in segmentList)
        {
            if (segment != null)
            {
                AttachSegment(segment);
            }
        }

        WithClause = withClause;
    }

    /// <summary>
    /// Gets the segments that compose the statement.
    /// </summary>
    public IReadOnlyList<SqlSegment> Segments => readOnlySegments;

    /// <summary>
    /// Gets the syntax options associated with the statement.
    /// </summary>
    public SqlSyntaxOptions SyntaxOptions { get; }

    /// <summary>
    /// Gets the optional WITH clause containing CTE definitions.
    /// </summary>
    public WithClause? WithClause { get; }

    /// <summary>
    /// Enumerates the statement itself and all nested statements.
    /// </summary>
    /// <returns>An enumerable containing this statement followed by nested statements.</returns>
    public IEnumerable<SqlStatement> EnumerateStatements()
    {
        yield return this;
        foreach (var child in GetChildStatements())
        {
            foreach (var descendant in child.EnumerateStatements())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Builds the SQL representation of the statement using the provided formatting options.
    /// </summary>
    /// <param name="options">Formatting options applied to the SQL output.</param>
    /// <returns>The SQL string representing the statement.</returns>
    public string ToSql(SqlFormattingOptions? options = null)
    {
        options ??= SqlFormattingOptions.Default;
        string inline = BuildSql();
        return SqlPrettyPrinter.Format(inline, options, SyntaxOptions);
    }

    /// <summary>
    /// Builds the canonical inline SQL representation of the statement.
    /// </summary>
    /// <returns>The inline SQL string representing the statement.</returns>
    protected abstract string BuildSql();

    /// <summary>
    /// Retrieves the child statements referenced by this statement.
    /// </summary>
    /// <returns>An enumerable of nested statements.</returns>
    protected virtual IEnumerable<SqlStatement> GetChildStatements()
    {
        if (WithClause != null)
        {
            foreach (var definition in WithClause.Definitions)
            {
                yield return definition.Statement;
            }
        }

        foreach (var segment in Segments)
        {
            foreach (var subquery in segment.Subqueries)
            {
                yield return subquery;
            }
        }
    }

    /// <summary>
    /// Registers the provided segment in the internal collection.
    /// </summary>
    /// <param name="segment">The segment to register.</param>
    protected void AttachSegment(SqlSegment segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        segments.Add(segment);
    }

    /// <summary>
    /// Removes the provided segment from the internal collection.
    /// </summary>
    /// <param name="segment">The segment to remove.</param>
    protected void DetachSegment(SqlSegment segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        segments.Remove(segment);
    }

    /// <summary>
    /// Replaces an existing segment with a new instance.
    /// </summary>
    /// <param name="previous">The segment currently registered.</param>
    /// <param name="replacement">The new segment to register.</param>
    protected void ReplaceSegment(SqlSegment? previous, SqlSegment? replacement)
    {
        if (!ReferenceEquals(previous, replacement))
        {
            if (previous != null)
            {
                segments.Remove(previous);
            }

            if (replacement != null)
            {
                segments.Add(replacement);
            }
        }
    }
}

/// <summary>
/// Represents a parsed SELECT statement.
/// </summary>
public sealed class SqlSelectStatement : SqlStatement
{
    private SqlSegment? from;
    private SqlSegment? where;
    private SqlSegment? groupBy;
    private SqlSegment? having;
    private SqlSegment? orderBy;
    private SqlSegment? limit;
    private SqlSegment? offset;
    private SqlSegment? tail;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSelectStatement"/> class.
    /// </summary>
    /// <param name="select">The SELECT segment.</param>
    /// <param name="from">The FROM segment.</param>
    /// <param name="where">The WHERE segment.</param>
    /// <param name="groupBy">The GROUP BY segment.</param>
    /// <param name="having">The HAVING segment.</param>
    /// <param name="orderBy">The ORDER BY segment.</param>
    /// <param name="limit">The LIMIT segment.</param>
    /// <param name="offset">The OFFSET segment.</param>
    /// <param name="tail">Additional segments such as UNION clauses.</param>
    /// <param name="withClause">Optional CTE definitions.</param>
    /// <param name="isDistinct">Indicates whether the SELECT statement is distinct.</param>
    public SqlSelectStatement(
        SqlSegment select,
        SqlSegment? from,
        SqlSegment? where,
        SqlSegment? groupBy,
        SqlSegment? having,
        SqlSegment? orderBy,
        SqlSegment? limit,
        SqlSegment? offset,
        SqlSegment? tail,
        WithClause? withClause,
        bool isDistinct)
        : base(new[]
        {
            select,
            from,
            where,
            groupBy,
            having,
            orderBy,
            limit,
            offset,
            tail,
        }.Where(s => s != null)!.Cast<SqlSegment>(), withClause)
    {
        Select = select ?? throw new ArgumentNullException(nameof(select));
        this.from = from;
        this.where = where;
        this.groupBy = groupBy;
        this.having = having;
        this.orderBy = orderBy;
        this.limit = limit;
        this.offset = offset;
        this.tail = tail;
        IsDistinct = isDistinct;
    }

    /// <summary>
    /// Gets the SELECT segment describing the selected expressions.
    /// </summary>
    public SqlSegment Select { get; }

    /// <summary>
    /// Gets the FROM segment describing the data sources.
    /// </summary>
    public SqlSegment? From => from;

    /// <summary>
    /// Gets the WHERE segment containing the filtering conditions.
    /// </summary>
    public SqlSegment? Where => where;

    /// <summary>
    /// Gets the GROUP BY segment.
    /// </summary>
    public SqlSegment? GroupBy => groupBy;

    /// <summary>
    /// Gets the HAVING segment.
    /// </summary>
    public SqlSegment? Having => having;

    /// <summary>
    /// Gets the ORDER BY segment.
    /// </summary>
    public SqlSegment? OrderBy => orderBy;

    /// <summary>
    /// Gets the LIMIT segment.
    /// </summary>
    public SqlSegment? Limit => limit;

    /// <summary>
    /// Gets the OFFSET segment.
    /// </summary>
    public SqlSegment? Offset => offset;

    /// <summary>
    /// Gets any trailing segments such as UNION clauses.
    /// </summary>
    public SqlSegment? Tail => tail;

    /// <summary>
    /// Gets a value indicating whether the SELECT statement includes DISTINCT.
    /// </summary>
    public bool IsDistinct { get; }

    /// <summary>
    /// Ensures the FROM segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created FROM segment.</returns>
    public SqlSegment EnsureFromSegment()
    {
        return EnsureSegment(ref from, "From");
    }

    /// <summary>
    /// Ensures the WHERE segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created WHERE segment.</returns>
    public SqlSegment EnsureWhereSegment()
    {
        return EnsureSegment(ref where, "Where");
    }

    /// <summary>
    /// Ensures the GROUP BY segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created GROUP BY segment.</returns>
    public SqlSegment EnsureGroupBySegment()
    {
        return EnsureSegment(ref groupBy, "GroupBy");
    }

    /// <summary>
    /// Ensures the HAVING segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created HAVING segment.</returns>
    public SqlSegment EnsureHavingSegment()
    {
        return EnsureSegment(ref having, "Having");
    }

    /// <summary>
    /// Ensures the ORDER BY segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created ORDER BY segment.</returns>
    public SqlSegment EnsureOrderBySegment()
    {
        return EnsureSegment(ref orderBy, "OrderBy");
    }

    /// <summary>
    /// Ensures the LIMIT segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created LIMIT segment.</returns>
    public SqlSegment EnsureLimitSegment()
    {
        return EnsureSegment(ref limit, "Limit");
    }

    /// <summary>
    /// Ensures the OFFSET segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created OFFSET segment.</returns>
    public SqlSegment EnsureOffsetSegment()
    {
        return EnsureSegment(ref offset, "Offset");
    }

    /// <summary>
    /// Ensures the trailing set operator segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created trailing segment.</returns>
    public SqlSegment EnsureTailSegment()
    {
        return EnsureSegment(ref tail, "Tail");
    }

    /// <inheritdoc />
    protected override string BuildSql()
    {
        var builder = new StringBuilder();
        if (WithClause != null)
        {
            builder.Append(WithClause.ToSql());
            builder.Append(' ');
        }

        builder.Append("SELECT");
        if (IsDistinct)
        {
            builder.Append(" DISTINCT");
        }

        builder.Append(' ');
        builder.Append(Select.ToSql());

        if (From != null)
        {
            builder.Append(" FROM ");
            builder.Append(From.ToSql());
        }

        if (Where != null)
        {
            builder.Append(" WHERE ");
            builder.Append(Where.ToSql());
        }

        if (GroupBy != null)
        {
            builder.Append(" GROUP BY ");
            builder.Append(GroupBy.ToSql());
        }

        if (Having != null)
        {
            builder.Append(" HAVING ");
            builder.Append(Having.ToSql());
        }

        if (OrderBy != null)
        {
            builder.Append(" ORDER BY ");
            builder.Append(OrderBy.ToSql());
        }

        if (Limit != null)
        {
            builder.Append(" LIMIT ");
            builder.Append(Limit.ToSql());
        }

        if (Offset != null)
        {
            builder.Append(" OFFSET ");
            builder.Append(Offset.ToSql());
        }

        if (Tail != null)
        {
            builder.Append(' ');
            builder.Append(Tail.ToSql());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ensures the referenced segment exists and registers it when necessary.
    /// </summary>
    /// <param name="segment">Reference to the target segment property.</param>
    /// <param name="name">Name of the segment to create when missing.</param>
    /// <returns>The ensured segment instance.</returns>
    private SqlSegment EnsureSegment(ref SqlSegment? segment, string name)
    {
        if (segment == null)
        {
            segment = SqlSegment.CreateEmpty(name, SyntaxOptions);
            AttachSegment(segment);
        }

        return segment;
    }
}

/// <summary>
/// Represents a parsed INSERT statement.
/// </summary>
public sealed class SqlInsertStatement : SqlStatement
{
    private SqlSegment? values;
    private SqlSegment? returning;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlInsertStatement"/> class.
    /// </summary>
    /// <param name="target">The target segment identifying the destination of the insert.</param>
    /// <param name="values">The VALUES segment when the insert uses literal values.</param>
    /// <param name="sourceQuery">The statement used as data source when the insert uses a query.</param>
    /// <param name="returning">The RETURNING segment if present.</param>
    /// <param name="withClause">Optional CTE definitions.</param>
    public SqlInsertStatement(SqlSegment target, SqlSegment? values, SqlStatement? sourceQuery, SqlSegment? returning, WithClause? withClause)
        : base(BuildSegments(target, values, returning), withClause)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        this.values = values;
        SourceQuery = sourceQuery;
        this.returning = returning;
    }

    /// <summary>
    /// Gets the target segment describing the destination table or expression.
    /// </summary>
    public SqlSegment Target { get; }

    /// <summary>
    /// Gets the VALUES segment when the statement inserts literal values.
    /// </summary>
    public SqlSegment? Values => values;

    /// <summary>
    /// Gets the source statement when the insert pulls data from a query.
    /// </summary>
    public SqlStatement? SourceQuery { get; }

    /// <summary>
    /// Gets the RETURNING segment when present.
    /// </summary>
    public SqlSegment? Returning => returning;

    /// <summary>
    /// Ensures a VALUES segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created VALUES segment.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the insert statement already uses a source query.</exception>
    public SqlSegment EnsureValuesSegment()
    {
        if (SourceQuery != null)
        {
            throw new InvalidOperationException("Cannot add VALUES to an INSERT statement that already specifies a source query.");
        }

        if (values == null)
        {
            values = SqlSegment.CreateEmpty("Values", SyntaxOptions);
            AttachSegment(values);
        }

        return values;
    }

    /// <summary>
    /// Ensures a RETURNING segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created RETURNING segment.</returns>
    public SqlSegment EnsureReturningSegment()
    {
        return EnsureOptionalSegment(ref returning, "Returning");
    }

    /// <inheritdoc />
    protected override IEnumerable<SqlStatement> GetChildStatements()
    {
        foreach (var child in base.GetChildStatements())
        {
            yield return child;
        }

        if (SourceQuery != null)
        {
            yield return SourceQuery;
        }
    }

    /// <inheritdoc />
    protected override string BuildSql()
    {
        var builder = new StringBuilder();
        if (WithClause != null)
        {
            builder.Append(WithClause.ToSql());
            builder.Append(' ');
        }

        builder.Append("INSERT INTO ");
        builder.Append(Target.ToSql());

        if (Values != null)
        {
            builder.Append(" VALUES ");
            builder.Append(Values.ToSql());
        }
        else if (SourceQuery != null)
        {
            builder.Append(' ');
            builder.Append(SourceQuery.ToSql());
        }

        if (Returning != null)
        {
            builder.Append(" RETURNING ");
            builder.Append(Returning.ToSql());
        }

        return builder.ToString();
    }

    private static IEnumerable<SqlSegment> BuildSegments(SqlSegment target, SqlSegment? values, SqlSegment? returning)
    {
        var segments = new List<SqlSegment>();
        if (target != null)
        {
            segments.Add(target);
        }

        if (values != null)
        {
            segments.Add(values);
        }

        if (returning != null)
        {
            segments.Add(returning);
        }

        return segments;
    }

    /// <summary>
    /// Ensures that an optional segment exists and registers it when necessary.
    /// </summary>
    /// <param name="segment">Reference to the optional segment.</param>
    /// <param name="name">Name of the segment to create.</param>
    /// <returns>The ensured segment.</returns>
    private SqlSegment EnsureOptionalSegment(ref SqlSegment? segment, string name)
    {
        if (segment == null)
        {
            segment = SqlSegment.CreateEmpty(name, SyntaxOptions);
            AttachSegment(segment);
        }

        return segment;
    }
}

/// <summary>
/// Represents a parsed UPDATE statement.
/// </summary>
public sealed class SqlUpdateStatement : SqlStatement
{
    private SqlSegment? from;
    private SqlSegment? where;
    private SqlSegment? returning;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlUpdateStatement"/> class.
    /// </summary>
    /// <param name="target">The target segment describing the updated entity.</param>
    /// <param name="set">The SET segment.</param>
    /// <param name="from">The FROM segment when joins are used.</param>
    /// <param name="where">The WHERE segment.</param>
    /// <param name="returning">The RETURNING segment.</param>
    /// <param name="withClause">Optional CTE definitions.</param>
    public SqlUpdateStatement(SqlSegment target, SqlSegment set, SqlSegment? from, SqlSegment? where, SqlSegment? returning, WithClause? withClause)
        : base(new[] { target, set, from, where, returning }.Where(s => s != null)!.Cast<SqlSegment>(), withClause)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Set = set ?? throw new ArgumentNullException(nameof(set));
        this.from = from;
        this.where = where;
        this.returning = returning;
    }

    /// <summary>
    /// Gets the target segment describing what is being updated.
    /// </summary>
    public SqlSegment Target { get; }

    /// <summary>
    /// Gets the SET segment describing the assignments.
    /// </summary>
    public SqlSegment Set { get; }

    /// <summary>
    /// Gets the FROM segment when present.
    /// </summary>
    public SqlSegment? From => from;

    /// <summary>
    /// Gets the WHERE segment when present.
    /// </summary>
    public SqlSegment? Where => where;

    /// <summary>
    /// Gets the RETURNING segment when present.
    /// </summary>
    public SqlSegment? Returning => returning;

    /// <summary>
    /// Ensures the FROM segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created FROM segment.</returns>
    public SqlSegment EnsureFromSegment()
    {
        return EnsureOptionalSegment(ref from, "From");
    }

    /// <summary>
    /// Ensures the WHERE segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created WHERE segment.</returns>
    public SqlSegment EnsureWhereSegment()
    {
        return EnsureOptionalSegment(ref where, "Where");
    }

    /// <summary>
    /// Ensures the RETURNING segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created RETURNING segment.</returns>
    public SqlSegment EnsureReturningSegment()
    {
        return EnsureOptionalSegment(ref returning, "Returning");
    }

    /// <inheritdoc />
    protected override string BuildSql()
    {
        var builder = new StringBuilder();
        if (WithClause != null)
        {
            builder.Append(WithClause.ToSql());
            builder.Append(' ');
        }

        builder.Append("UPDATE ");
        builder.Append(Target.ToSql());
        builder.Append(" SET ");
        builder.Append(Set.ToSql());

        if (From != null)
        {
            builder.Append(" FROM ");
            builder.Append(From.ToSql());
        }

        if (Where != null)
        {
            builder.Append(" WHERE ");
            builder.Append(Where.ToSql());
        }

        if (Returning != null)
        {
            builder.Append(" RETURNING ");
            builder.Append(Returning.ToSql());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ensures an optional segment exists for the update statement.
    /// </summary>
    /// <param name="segment">Reference to the optional segment.</param>
    /// <param name="name">Name of the segment to create.</param>
    /// <returns>The ensured segment.</returns>
    private SqlSegment EnsureOptionalSegment(ref SqlSegment? segment, string name)
    {
        if (segment == null)
        {
            segment = SqlSegment.CreateEmpty(name, SyntaxOptions);
            AttachSegment(segment);
        }

        return segment;
    }
}

/// <summary>
/// Represents a parsed DELETE statement.
/// </summary>
public sealed class SqlDeleteStatement : SqlStatement
{
    private SqlSegment? target;
    private SqlSegment? usingSegment;
    private SqlSegment? where;
    private SqlSegment? returning;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlDeleteStatement"/> class.
    /// </summary>
    /// <param name="target">The DELETE target segment when explicitly stated.</param>
    /// <param name="from">The FROM segment.</param>
    /// <param name="using">The USING segment when supported by the dialect.</param>
    /// <param name="where">The WHERE segment.</param>
    /// <param name="returning">The RETURNING segment.</param>
    /// <param name="withClause">Optional CTE definitions.</param>
    public SqlDeleteStatement(SqlSegment? target, SqlSegment from, SqlSegment? @using, SqlSegment? where, SqlSegment? returning, WithClause? withClause)
        : base(new[] { target, from, @using, where, returning }.Where(s => s != null)!.Cast<SqlSegment>(), withClause)
    {
        this.target = target;
        From = from ?? throw new ArgumentNullException(nameof(from));
        usingSegment = @using;
        this.where = where;
        this.returning = returning;
    }

    /// <summary>
    /// Gets the optional target segment explicitly referenced after DELETE.
    /// </summary>
    public SqlSegment? Target => target;

    /// <summary>
    /// Gets the FROM segment.
    /// </summary>
    public SqlSegment From { get; }

    /// <summary>
    /// Gets the USING segment when present.
    /// </summary>
    public SqlSegment? Using => usingSegment;

    /// <summary>
    /// Gets the WHERE segment when present.
    /// </summary>
    public SqlSegment? Where => where;

    /// <summary>
    /// Gets the RETURNING segment when present.
    /// </summary>
    public SqlSegment? Returning => returning;

    /// <summary>
    /// Ensures the DELETE target segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created target segment.</returns>
    public SqlSegment EnsureTargetSegment()
    {
        return EnsureOptionalSegment(ref target, "Target");
    }

    /// <summary>
    /// Ensures the USING segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created USING segment.</returns>
    public SqlSegment EnsureUsingSegment()
    {
        return EnsureOptionalSegment(ref usingSegment, "Using");
    }

    /// <summary>
    /// Ensures the WHERE segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created WHERE segment.</returns>
    public SqlSegment EnsureWhereSegment()
    {
        return EnsureOptionalSegment(ref where, "Where");
    }

    /// <summary>
    /// Ensures the RETURNING segment exists and returns it.
    /// </summary>
    /// <returns>The existing or newly created RETURNING segment.</returns>
    public SqlSegment EnsureReturningSegment()
    {
        return EnsureOptionalSegment(ref returning, "Returning");
    }

    /// <inheritdoc />
    protected override string BuildSql()
    {
        var builder = new StringBuilder();
        if (WithClause != null)
        {
            builder.Append(WithClause.ToSql());
            builder.Append(' ');
        }

        builder.Append("DELETE");
        if (Target != null)
        {
            builder.Append(' ');
            builder.Append(Target.ToSql());
        }

        builder.Append(" FROM ");
        builder.Append(From.ToSql());

        if (Using != null)
        {
            builder.Append(" USING ");
            builder.Append(Using.ToSql());
        }

        if (Where != null)
        {
            builder.Append(" WHERE ");
            builder.Append(Where.ToSql());
        }

        if (Returning != null)
        {
            builder.Append(" RETURNING ");
            builder.Append(Returning.ToSql());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ensures the optional segment exists and registers it when necessary.
    /// </summary>
    /// <param name="segment">Reference to the optional segment.</param>
    /// <param name="name">Name of the segment to create.</param>
    /// <returns>The ensured segment.</returns>
    private SqlSegment EnsureOptionalSegment(ref SqlSegment? segment, string name)
    {
        if (segment == null)
        {
            segment = SqlSegment.CreateEmpty(name, SyntaxOptions);
            AttachSegment(segment);
        }

        return segment;
    }
}

/// <summary>
/// Represents a section of a SQL statement, such as SELECT columns or a WHERE clause.
/// </summary>
public sealed class SqlSegment
{
    private readonly List<ISqlSegmentPart> parts;
    private readonly ReadOnlyCollection<ISqlSegmentPart> readOnlyParts;
    private readonly SqlSyntaxOptions syntaxOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSegment"/> class.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="parts">Parts composing the segment.</param>
    /// <param name="syntaxOptions">Syntax options controlling tokenization for future edits.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="parts"/> is null.</exception>
    internal SqlSegment(string name, IEnumerable<ISqlSegmentPart> parts, SqlSyntaxOptions syntaxOptions)
        : this(name, (parts ?? throw new ArgumentNullException(nameof(parts))).ToList(), syntaxOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSegment"/> class with the provided list of parts.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="parts">Parts composing the segment.</param>
    /// <param name="syntaxOptions">Syntax options controlling tokenization for future edits.</param>
    private SqlSegment(string name, List<ISqlSegmentPart> parts, SqlSyntaxOptions syntaxOptions)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
        readOnlyParts = new ReadOnlyCollection<ISqlSegmentPart>(this.parts);
    }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="SqlSegment"/> class.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="syntaxOptions">Syntax options controlling tokenization for future edits.</param>
    private SqlSegment(string name, SqlSyntaxOptions syntaxOptions)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
        parts = new List<ISqlSegmentPart>();
        readOnlyParts = new ReadOnlyCollection<ISqlSegmentPart>(parts);
    }

    /// <summary>
    /// Creates an empty segment with the specified name.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="syntaxOptions">Syntax options controlling tokenization for future edits.</param>
    /// <returns>The newly created segment.</returns>
    internal static SqlSegment CreateEmpty(string name, SqlSyntaxOptions syntaxOptions) => new SqlSegment(name, syntaxOptions);

    /// <summary>
    /// Gets the name of the segment.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the segment contains any parts.
    /// </summary>
    public bool IsEmpty => parts.Count == 0;

    /// <summary>
    /// Gets the parts composing the segment.
    /// </summary>
    internal IReadOnlyList<ISqlSegmentPart> Parts => readOnlyParts;

    /// <summary>
    /// Gets the syntax options associated with the segment.
    /// </summary>
    internal SqlSyntaxOptions SyntaxOptions => syntaxOptions;

    /// <summary>
    /// Gets the subqueries found in the segment.
    /// </summary>
    public IEnumerable<SqlStatement> Subqueries => parts.OfType<SqlSubqueryPart>().Select(p => p.Statement);

    /// <summary>
    /// Appends raw SQL tokens to the segment.
    /// </summary>
    /// <param name="sql">The SQL snippet to append.</param>
    public void AddRaw(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL text cannot be null or whitespace.", nameof(sql));
        }

        AppendParts(ParseParts(sql));
    }

    /// <summary>
    /// Appends a comma-separated element to the segment, inserting a comma when required.
    /// </summary>
    /// <param name="sql">The SQL snippet representing the new element.</param>
    public void AddCommaSeparatedElement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL text cannot be null or whitespace.", nameof(sql));
        }

        if (!IsEmpty)
        {
            parts.Add(new SqlTokenPart(","));
        }

        AppendParts(ParseParts(sql));
    }

    /// <summary>
    /// Appends a logical conjunction to the segment followed by an expression.
    /// </summary>
    /// <param name="conjunction">The conjunction keyword such as AND or OR.</param>
    /// <param name="expression">The expression to append.</param>
    public void AddConjunction(string conjunction, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression text cannot be null or whitespace.", nameof(expression));
        }

        if (!IsEmpty)
        {
            if (string.IsNullOrWhiteSpace(conjunction))
            {
                throw new ArgumentException("Conjunction cannot be null or whitespace when appending to a non-empty segment.", nameof(conjunction));
            }

            AppendParts(ParseParts(conjunction));
        }

        AppendParts(ParseParts(expression));
    }

    /// <summary>
    /// Builds the SQL text represented by the segment.
    /// </summary>
    /// <returns>The SQL string representing the segment.</returns>
    public string ToSql()
    {
        var tokens = new List<string>();
        foreach (var part in parts)
        {
            part.WriteTo(tokens);
        }

        return SqlStringFormatter.JoinTokens(tokens);
    }

    /// <summary>
    /// Appends the provided parts to the segment.
    /// </summary>
    /// <param name="newParts">Parts to append.</param>
    private void AppendParts(IEnumerable<ISqlSegmentPart> newParts)
    {
        foreach (var part in newParts)
        {
            parts.Add(part);
        }
    }

    /// <summary>
    /// Parses the provided SQL snippet into segment parts.
    /// </summary>
    /// <param name="sql">The SQL snippet to parse.</param>
    /// <returns>The parsed parts.</returns>
    private IReadOnlyList<ISqlSegmentPart> ParseParts(string sql)
    {
        var tokenizer = new SqlTokenizer(sql, syntaxOptions);
        var tokens = tokenizer.Tokenize();
        return SqlParser.BuildSegmentParts(tokens, syntaxOptions);
    }
}

/// <summary>
/// Represents a WITH clause containing CTE definitions.
/// </summary>
public sealed class WithClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithClause"/> class.
    /// </summary>
    /// <param name="isRecursive">Indicates whether the WITH clause is recursive.</param>
    /// <param name="definitions">The definitions included in the clause.</param>
    public WithClause(bool isRecursive, IEnumerable<CteDefinition> definitions)
    {
        IsRecursive = isRecursive;
        Definitions = new ReadOnlyCollection<CteDefinition>((definitions ?? throw new ArgumentNullException(nameof(definitions))).ToList());
    }

    /// <summary>
    /// Gets a value indicating whether the clause is recursive.
    /// </summary>
    public bool IsRecursive { get; }

    /// <summary>
    /// Gets the CTE definitions.
    /// </summary>
    public IReadOnlyList<CteDefinition> Definitions { get; }

    /// <summary>
    /// Builds the SQL text representing the WITH clause.
    /// </summary>
    /// <returns>The SQL string for the clause.</returns>
    public string ToSql()
    {
        var builder = new StringBuilder();
        builder.Append("WITH");
        if (IsRecursive)
        {
            builder.Append(" RECURSIVE");
        }

        builder.Append(' ');
        builder.Append(string.Join(", ", Definitions.Select(d => d.ToSql())));
        return builder.ToString();
    }
}

/// <summary>
/// Represents a CTE definition inside a WITH clause.
/// </summary>
public sealed class CteDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CteDefinition"/> class.
    /// </summary>
    /// <param name="name">Name of the CTE.</param>
    /// <param name="columns">Optional list of column names.</param>
    /// <param name="statement">The statement defining the CTE.</param>
    public CteDefinition(string name, IReadOnlyList<string>? columns, SqlStatement statement)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Columns = columns;
        Statement = statement ?? throw new ArgumentNullException(nameof(statement));
    }

    /// <summary>
    /// Gets the name of the CTE.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional list of column names.
    /// </summary>
    public IReadOnlyList<string>? Columns { get; }

    /// <summary>
    /// Gets the statement associated with the CTE.
    /// </summary>
    public SqlStatement Statement { get; }

    /// <summary>
    /// Builds the SQL text representing the CTE definition.
    /// </summary>
    /// <returns>The SQL string for the CTE.</returns>
    public string ToSql()
    {
        var builder = new StringBuilder();
        builder.Append(Name);
        if (Columns != null && Columns.Count > 0)
        {
            builder.Append('(');
            builder.Append(string.Join(", ", Columns));
            builder.Append(')');
        }

        builder.Append(" AS (");
        builder.Append(Statement.ToSql());
        builder.Append(')');
        return builder.ToString();
    }
}

/// <summary>
/// Represents errors that occur during SQL parsing.
/// </summary>
public sealed class SqlParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    public SqlParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SqlParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SqlParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal interface ISqlSegmentPart
{
    void WriteTo(ICollection<string> tokens);
}

internal sealed class SqlTokenPart : ISqlSegmentPart
{
    private readonly string token;

    public SqlTokenPart(string token)
    {
        this.token = token;
    }

    public void WriteTo(ICollection<string> tokens)
    {
        tokens.Add(token);
    }
}

internal sealed class SqlSubqueryPart : ISqlSegmentPart
{
    public SqlSubqueryPart(SqlStatement statement)
    {
        Statement = statement;
    }

    public SqlStatement Statement { get; }

    public void WriteTo(ICollection<string> tokens)
    {
        tokens.Add("(");
        tokens.Add(Statement.ToSql());
        tokens.Add(")");
    }
}

internal static class SqlStringFormatter
{
    private static readonly HashSet<string> SpaceBeforeParenthesisKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP",
        "HAVING",
        "ORDER",
        "LIMIT",
        "OFFSET",
        "VALUES",
        "IN",
        "EXISTS",
        "JOIN",
        "INNER",
        "LEFT",
        "RIGHT",
        "FULL",
        "OUTER",
        "ON",
        "USING",
        "RETURNING",
        "UPDATE",
        "INSERT",
        "DELETE",
        "SET",
        "AS",
        "DISTINCT",
        "WITH",
        "UNION",
        "INTERSECT",
        "EXCEPT",
        "CASE",
        "WHEN",
        "THEN",
        "ELSE",
    };

    public static string JoinTokens(IReadOnlyList<string> tokens)
    {
        var builder = new StringBuilder();
        string? previous = null;
        foreach (var token in tokens)
        {
            if (builder.Length > 0 && ShouldInsertSpace(previous, token))
            {
                builder.Append(' ');
            }

            builder.Append(token);
            previous = token;
        }

        return builder.ToString();
    }

    private static bool ShouldInsertSpace(string? previous, string current)
    {
        if (string.IsNullOrEmpty(current))
        {
            return false;
        }

        if (current is "," or ")" or "." or ";" or ":" or "]")
        {
            return false;
        }

        if (previous is null)
        {
            return false;
        }

        if (current == "(")
        {
            if (previous.Length == 0)
            {
                return false;
            }

            if (SpaceBeforeParenthesisKeywords.Contains(previous.ToUpperInvariant()))
            {
                return true;
            }

            char last = previous[^1];
            return last switch
            {
                '(' or '[' or '.' => false,
                _ when char.IsLetterOrDigit(last) => false,
                _ => true,
            };
        }

        char previousLast = previous[^1];
        if (previousLast is '(' or '[' or '.')
        {
            return false;
        }

        return true;
    }
}
