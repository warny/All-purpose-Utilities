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
    /// <returns>A <see cref="SqlQuery"/> representing the parsed statement.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
    /// <exception cref="SqlParseException">Thrown when the SQL text cannot be parsed.</exception>
    public static SqlQuery Parse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL text cannot be null or whitespace.", nameof(sql));
        }

        var parser = SqlParser.Create(sql);
        SqlStatement statement = parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();
        return new SqlQuery(statement);
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootStatement"/> is null.</exception>
    public SqlQuery(SqlStatement rootStatement)
    {
        RootStatement = rootStatement ?? throw new ArgumentNullException(nameof(rootStatement));
    }

    /// <summary>
    /// Gets the root statement of the query.
    /// </summary>
    public SqlStatement RootStatement { get; }

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
/// Determines how SQL text is formatted when rebuilt from the analyser output.
/// </summary>
public enum SqlFormattingMode
{
    /// <summary>
    /// The SQL text is emitted on a single line.
    /// </summary>
    Inline,

    /// <summary>
    /// Commas are placed at the beginning of lines for list-oriented clauses.
    /// </summary>
    Prefixed,

    /// <summary>
    /// Commas terminate lines for list-oriented clauses.
    /// </summary>
    Suffixed,
}

/// <summary>
/// Provides formatting configuration for SQL reconstruction.
/// </summary>
public sealed class SqlFormattingOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlFormattingOptions"/> class.
    /// </summary>
    /// <param name="mode">The formatting mode to apply when rebuilding SQL.</param>
    /// <param name="indentSize">The number of spaces used per indentation level.</param>
    public SqlFormattingOptions(SqlFormattingMode mode = SqlFormattingMode.Inline, int indentSize = 4)
    {
        if (indentSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize), indentSize, "Indent size must be greater than or equal to zero.");
        }

        Mode = mode;
        IndentSize = indentSize;
    }

    /// <summary>
    /// Gets the default formatting options (inline mode with an indent size of four spaces).
    /// </summary>
    public static SqlFormattingOptions Default { get; } = new SqlFormattingOptions();

    /// <summary>
    /// Gets the formatting mode used for reconstruction.
    /// </summary>
    public SqlFormattingMode Mode { get; }

    /// <summary>
    /// Gets the number of spaces used per indentation level.
    /// </summary>
    public int IndentSize { get; }
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
    protected SqlStatement(IEnumerable<SqlSegment> segments, WithClause? withClause)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        this.segments = new List<SqlSegment>();
        readOnlySegments = new ReadOnlyCollection<SqlSegment>(this.segments);

        foreach (var segment in segments)
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
        return SqlPrettyPrinter.Format(inline, options);
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
            segment = SqlSegment.CreateEmpty(name);
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
            values = SqlSegment.CreateEmpty("Values");
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
            segment = SqlSegment.CreateEmpty(name);
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
            segment = SqlSegment.CreateEmpty(name);
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
            segment = SqlSegment.CreateEmpty(name);
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSegment"/> class.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="parts">Parts composing the segment.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="parts"/> is null.</exception>
    internal SqlSegment(string name, IEnumerable<ISqlSegmentPart> parts)
        : this(name, (parts ?? throw new ArgumentNullException(nameof(parts))).ToList())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSegment"/> class with the provided list of parts.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <param name="parts">Parts composing the segment.</param>
    private SqlSegment(string name, List<ISqlSegmentPart> parts)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        readOnlyParts = new ReadOnlyCollection<ISqlSegmentPart>(this.parts);
    }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="SqlSegment"/> class.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    private SqlSegment(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        parts = new List<ISqlSegmentPart>();
        readOnlyParts = new ReadOnlyCollection<ISqlSegmentPart>(parts);
    }

    /// <summary>
    /// Creates an empty segment with the specified name.
    /// </summary>
    /// <param name="name">Name of the segment.</param>
    /// <returns>The newly created segment.</returns>
    internal static SqlSegment CreateEmpty(string name) => new SqlSegment(name);

    /// <summary>
    /// Gets the name of the segment.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the segment contains any parts.
    /// </summary>
    public bool IsEmpty => parts.Count == 0;

    /// <summary>
    /// <summary>
    /// Gets the parts composing the segment.
    /// </summary>
    internal IReadOnlyList<ISqlSegmentPart> Parts => readOnlyParts;

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
    private static IReadOnlyList<ISqlSegmentPart> ParseParts(string sql)
    {
        var tokenizer = new SqlTokenizer(sql);
        var tokens = tokenizer.Tokenize();
        return SqlParser.BuildSegmentParts(tokens);
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

/// <summary>
/// Formats SQL token streams according to the selected <see cref="SqlFormattingMode"/>.
/// </summary>
internal static class SqlPrettyPrinter
{
    /// <summary>
    /// Represents the clause currently being formatted.
    /// </summary>
    private enum ClauseContext
    {
        /// <summary>
        /// No special clause formatting is active.
        /// </summary>
        None,

        /// <summary>
        /// The clause contains SELECT list items.
        /// </summary>
        SelectList,

        /// <summary>
        /// The clause contains GROUP BY expressions.
        /// </summary>
        GroupByList,

        /// <summary>
        /// The clause contains ORDER BY expressions.
        /// </summary>
        OrderByList,

        /// <summary>
        /// The clause contains VALUES rows.
        /// </summary>
        ValuesList,

        /// <summary>
        /// The clause contains SET assignments.
        /// </summary>
        SetList,

        /// <summary>
        /// The clause contains RETURNING expressions.
        /// </summary>
        ReturningList,
    }

    /// <summary>
    /// Represents a formatted line made of SQL tokens.
    /// </summary>
    private sealed class FormattedLine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedLine"/> class.
        /// </summary>
        /// <param name="indentSpaces">The indentation applied to the line expressed in spaces.</param>
        public FormattedLine(int indentSpaces)
        {
            IndentSpaces = indentSpaces;
            Tokens = new List<string>();
        }

        /// <summary>
        /// Gets the number of leading spaces applied to the line.
        /// </summary>
        public int IndentSpaces { get; }

        /// <summary>
        /// Gets the SQL tokens forming the line.
        /// </summary>
        public List<string> Tokens { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the first comma should be rendered without a trailing space.
        /// </summary>
        public bool SuppressSpaceAfterLeadingComma { get; set; }
    }

    private static readonly HashSet<string> ClauseKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
        "RETURNING",
        "SET",
        "INSERT",
        "UPDATE",
        "DELETE",
        "UNION",
        "INTERSECT",
        "EXCEPT",
    };

    private static readonly HashSet<string> JoinLeadingModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "INNER",
        "LEFT",
        "RIGHT",
        "FULL",
        "CROSS",
    };

    /// <summary>
    /// Formats the provided SQL text according to the specified options.
    /// </summary>
    /// <param name="sql">The inline SQL string.</param>
    /// <param name="options">Formatting options controlling the output.</param>
    /// <returns>The formatted SQL text.</returns>
    public static string Format(string sql, SqlFormattingOptions options)
    {
        if (options.Mode == SqlFormattingMode.Inline)
        {
            return sql;
        }

        var tokenizer = new SqlTokenizer(sql);
        IReadOnlyList<SqlToken> tokens = tokenizer.Tokenize();
        return options.Mode switch
        {
            SqlFormattingMode.Prefixed => FormatList(tokens, options, true),
            SqlFormattingMode.Suffixed => FormatList(tokens, options, false),
            _ => sql,
        };
    }

    /// <summary>
    /// Formats the supplied SQL tokens into multi-line text.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="options">Formatting options driving indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <returns>The formatted SQL text.</returns>
    private static string FormatList(IReadOnlyList<SqlToken> tokens, SqlFormattingOptions options, bool commaAtLineStart)
    {
        var lines = new List<FormattedLine>();
        FormattedLine? currentLine = null;
        var parenthesisStack = new Stack<bool>();
        int indentLevel = 0;
        ClauseContext clause = ClauseContext.None;
        bool firstItem = false;
        bool pendingComma = false;
        int clauseIndent = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            SqlToken token = tokens[i];
            string text = token.Text;
            string upper = token.Normalized;

            if (TryHandleClauseStart(tokens, options, commaAtLineStart, ref i, ref currentLine, lines, ref indentLevel, ref clause, ref firstItem, ref pendingComma, ref clauseIndent, upper, text))
            {
                continue;
            }

            if (clause != ClauseContext.None && text != ",")
            {
                PrepareClauseLine(options, commaAtLineStart, ref currentLine, lines, ref clause, ref firstItem, ref pendingComma, clauseIndent);
            }

            if (text == "," && clause != ClauseContext.None)
            {
                if (commaAtLineStart)
                {
                    pendingComma = true;
                }
                else
                {
                    AppendToken(ref currentLine, lines, clauseIndent + options.IndentSize, text);
                    CommitLine(ref currentLine, lines);
                    firstItem = true;
                }

                continue;
            }

            int baseIndent = indentLevel * options.IndentSize;
            int effectiveIndent;
            if (clause != ClauseContext.None)
            {
                effectiveIndent = currentLine?.IndentSpaces ?? clauseIndent + options.IndentSize;
            }
            else
            {
                effectiveIndent = baseIndent;
            }

            if (text == "(")
            {
                if (clause != ClauseContext.None && firstItem)
                {
                    PrepareClauseLine(options, commaAtLineStart, ref currentLine, lines, ref clause, ref firstItem, ref pendingComma, clauseIndent);
                }

                AppendToken(ref currentLine, lines, effectiveIndent, text);
                bool multiline = ShouldExpandParenthesis(tokens, i + 1);
                parenthesisStack.Push(multiline);
                indentLevel++;
                if (multiline)
                {
                    CommitLine(ref currentLine, lines);
                }

                continue;
            }

            if (text == ")")
            {
                bool multiline = parenthesisStack.Count > 0 && parenthesisStack.Pop();
                indentLevel = Math.Max(0, indentLevel - 1);
                if (multiline)
                {
                    CommitLine(ref currentLine, lines);
                }

                baseIndent = indentLevel * options.IndentSize;
                if (clause != ClauseContext.None)
                {
                    effectiveIndent = currentLine?.IndentSpaces ?? clauseIndent + options.IndentSize;
                }
                else
                {
                    effectiveIndent = baseIndent;
                }
                AppendToken(ref currentLine, lines, effectiveIndent, text);
                continue;
            }

            AppendToken(ref currentLine, lines, effectiveIndent, text);
        }

        CommitLine(ref currentLine, lines);
        return BuildFormattedText(lines);
    }

    /// <summary>
    /// Detects and formats clause keywords encountered while walking the token list.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="options">Formatting options driving indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <param name="index">Index of the current token. The value may be advanced when multiple tokens are consumed.</param>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentLevel">The current indentation level represented by parenthesis depth.</param>
    /// <param name="clause">The active clause context.</param>
    /// <param name="firstItem">Indicates whether the next clause item is the first one.</param>
    /// <param name="pendingComma">Indicates whether a comma should be emitted at the beginning of the next line.</param>
    /// <param name="clauseIndent">Stores the base indentation of the active clause.</param>
    /// <param name="upper">Uppercase representation of the current token.</param>
    /// <param name="text">Original token text.</param>
    /// <returns><c>true</c> when the token has been fully handled; otherwise <c>false</c>.</returns>
    private static bool TryHandleClauseStart(
        IReadOnlyList<SqlToken> tokens,
        SqlFormattingOptions options,
        bool commaAtLineStart,
        ref int index,
        ref FormattedLine? currentLine,
        List<FormattedLine> lines,
        ref int indentLevel,
        ref ClauseContext clause,
        ref bool firstItem,
        ref bool pendingComma,
        ref int clauseIndent,
        string upper,
        string text)
    {
        int baseIndent = indentLevel * options.IndentSize;

        switch (upper)
        {
            case "WITH":
            case "UNION":
            case "INTERSECT":
            case "EXCEPT":
                clause = ClauseContext.None;
                firstItem = false;
                pendingComma = false;
                StartNewLine(ref currentLine, lines, baseIndent);
                AppendToken(ref currentLine, lines, baseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "SELECT":
                clause = ClauseContext.SelectList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "GROUP":
                if (index + 1 < tokens.Count && tokens[index + 1].Normalized.Equals("BY", StringComparison.OrdinalIgnoreCase))
                {
                    clause = ClauseContext.GroupByList;
                    firstItem = true;
                    pendingComma = false;
                    clauseIndent = baseIndent;
                    StartNewLine(ref currentLine, lines, clauseIndent);
                    AppendToken(ref currentLine, lines, clauseIndent, text);
                    index++;
                    AppendToken(ref currentLine, lines, clauseIndent, tokens[index].Text);
                    CommitLine(ref currentLine, lines);
                    return true;
                }

                break;

            case "ORDER":
                if (index + 1 < tokens.Count && tokens[index + 1].Normalized.Equals("BY", StringComparison.OrdinalIgnoreCase))
                {
                    clause = ClauseContext.OrderByList;
                    firstItem = true;
                    pendingComma = false;
                    clauseIndent = baseIndent;
                    StartNewLine(ref currentLine, lines, clauseIndent);
                    AppendToken(ref currentLine, lines, clauseIndent, text);
                    index++;
                    AppendToken(ref currentLine, lines, clauseIndent, tokens[index].Text);
                    CommitLine(ref currentLine, lines);
                    return true;
                }

                break;

            case "VALUES":
                clause = ClauseContext.ValuesList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "RETURNING":
                clause = ClauseContext.ReturningList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "SET":
                clause = ClauseContext.SetList;
                firstItem = true;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                CommitLine(ref currentLine, lines);
                return true;

            case "FROM":
            case "WHERE":
            case "HAVING":
            case "LIMIT":
            case "OFFSET":
            case "USING":
            case "INSERT":
            case "UPDATE":
            case "DELETE":
                clause = ClauseContext.None;
                firstItem = false;
                pendingComma = false;
                clauseIndent = baseIndent;
                StartNewLine(ref currentLine, lines, clauseIndent);
                AppendToken(ref currentLine, lines, clauseIndent, text);
                return true;
        }

        if (JoinLeadingModifiers.Contains(upper))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            StartNewLine(ref currentLine, lines, clauseIndent);
            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        if (upper.Equals("OUTER", StringComparison.OrdinalIgnoreCase))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            EnsureLine(ref currentLine, lines, clauseIndent);
            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        if (upper.Equals("JOIN", StringComparison.OrdinalIgnoreCase))
        {
            clause = ClauseContext.None;
            firstItem = false;
            pendingComma = false;
            clauseIndent = baseIndent;
            if (currentLine == null || currentLine.Tokens.Count == 0)
            {
                StartNewLine(ref currentLine, lines, clauseIndent);
            }
            else
            {
                string lastToken = currentLine.Tokens[^1].ToUpperInvariant();
                if (!JoinLeadingModifiers.Contains(lastToken) && lastToken != "OUTER")
                {
                    StartNewLine(ref currentLine, lines, clauseIndent);
                }
            }

            AppendToken(ref currentLine, lines, clauseIndent, text);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures a line is available for clause content and emits comma prefixes when required.
    /// </summary>
    /// <param name="options">Formatting options controlling indentation.</param>
    /// <param name="commaAtLineStart">Indicates whether commas should start new lines.</param>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="clause">The active clause context.</param>
    /// <param name="firstItem">Indicates whether the next clause item is the first one.</param>
    /// <param name="pendingComma">Indicates whether a comma should be emitted before the next token.</param>
    /// <param name="clauseIndent">Stores the base indentation of the active clause.</param>
    private static void PrepareClauseLine(
        SqlFormattingOptions options,
        bool commaAtLineStart,
        ref FormattedLine? currentLine,
        List<FormattedLine> lines,
        ref ClauseContext clause,
        ref bool firstItem,
        ref bool pendingComma,
        int clauseIndent)
    {
        if (clause == ClauseContext.None)
        {
            return;
        }

        if (firstItem)
        {
            StartNewLine(ref currentLine, lines, clauseIndent + options.IndentSize);
            firstItem = false;
            return;
        }

        if (commaAtLineStart && pendingComma)
        {
            int indent = clauseIndent + Math.Max(options.IndentSize - 1, 0);
            StartNewLine(ref currentLine, lines, indent);
            AppendToken(ref currentLine, lines, indent, ",");
            if (currentLine != null)
            {
                currentLine.SuppressSpaceAfterLeadingComma = true;
            }
            pendingComma = false;
            return;
        }

        if (currentLine == null)
        {
            StartNewLine(ref currentLine, lines, clauseIndent + options.IndentSize);
        }
    }

    /// <summary>
    /// Determines whether the contents enclosed by a parenthesis warrant multi-line formatting.
    /// </summary>
    /// <param name="tokens">Tokens composing the SQL statement.</param>
    /// <param name="startIndex">Index immediately after the opening parenthesis.</param>
    /// <returns><c>true</c> when the content includes clause keywords; otherwise <c>false</c>.</returns>
    private static bool ShouldExpandParenthesis(IReadOnlyList<SqlToken> tokens, int startIndex)
    {
        int depth = 1;
        for (int i = startIndex; i < tokens.Count; i++)
        {
            string text = tokens[i].Text;
            if (text == "(")
            {
                depth++;
                continue;
            }

            if (text == ")")
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if (depth == 1 && ClauseKeywords.Contains(tokens[i].Normalized))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends a token to the current line, creating a new one when necessary.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    /// <param name="token">Token to append.</param>
    private static void AppendToken(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces, string token)
    {
        EnsureLine(ref currentLine, lines, indentSpaces);
        currentLine!.Tokens.Add(token);
    }

    /// <summary>
    /// Starts a new line with the specified indentation.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    private static void StartNewLine(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces)
    {
        CommitLine(ref currentLine, lines);
        currentLine = new FormattedLine(indentSpaces);
    }

    /// <summary>
    /// Ensures that a line exists with the specified indentation before appending tokens.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    /// <param name="indentSpaces">Indentation expressed in spaces.</param>
    private static void EnsureLine(ref FormattedLine? currentLine, List<FormattedLine> lines, int indentSpaces)
    {
        if (currentLine == null)
        {
            currentLine = new FormattedLine(indentSpaces);
            return;
        }

        if (currentLine.Tokens.Count == 0 && currentLine.IndentSpaces != indentSpaces)
        {
            currentLine = new FormattedLine(indentSpaces);
            return;
        }

        if (currentLine.IndentSpaces != indentSpaces)
        {
            StartNewLine(ref currentLine, lines, indentSpaces);
        }
    }

    /// <summary>
    /// Commits the current line to the output collection when it contains tokens.
    /// </summary>
    /// <param name="currentLine">Reference to the line currently being built.</param>
    /// <param name="lines">Collection of completed lines.</param>
    private static void CommitLine(ref FormattedLine? currentLine, List<FormattedLine> lines)
    {
        if (currentLine != null && currentLine.Tokens.Count > 0)
        {
            lines.Add(currentLine);
        }

        currentLine = null;
    }

    /// <summary>
    /// Builds the formatted SQL text from the prepared lines.
    /// </summary>
    /// <param name="lines">Lines composing the final output.</param>
    /// <returns>The formatted SQL text.</returns>
    private static string BuildFormattedText(List<FormattedLine> lines)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            FormattedLine line = lines[i];
            builder.Append(' ', line.IndentSpaces);
            string text = SqlStringFormatter.JoinTokens(line.Tokens);
            if (line.SuppressSpaceAfterLeadingComma && text.StartsWith(", ", StringComparison.Ordinal))
            {
                text = "," + text.Substring(2);
            }

            builder.Append(text);
            if (i < lines.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
