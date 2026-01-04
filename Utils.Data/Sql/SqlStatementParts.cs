using System;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Represents a typed part of a SQL statement built from a parsed segment.
/// </summary>
public abstract class SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlStatementPart"/> class.
    /// </summary>
    /// <param name="name">Name of the part for identification.</param>
    /// <param name="segment">The underlying segment represented by the part.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="segment"/> is null.</exception>
    protected SqlStatementPart(string name, SqlSegment segment)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Segment = segment ?? throw new ArgumentNullException(nameof(segment));
    }

    /// <summary>
    /// Gets the display name of the part.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the <see cref="SqlSegment"/> that stores the parsed tokens.
    /// </summary>
    public SqlSegment Segment { get; }

    /// <summary>
    /// Builds the SQL text represented by the part.
    /// </summary>
    /// <returns>The SQL string rendered from the underlying segment.</returns>
    public string ToSql()
    {
        return Segment.ToSql();
    }
}

/// <summary>
/// Represents the SELECT clause of a SQL statement.
/// </summary>
public sealed class SelectPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the SELECT clause.</param>
    public SelectPart(SqlSegment segment)
        : base("Select", segment)
    {
    }
}

/// <summary>
/// Represents the FROM clause of a SQL statement.
/// </summary>
public sealed class FromPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FromPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the FROM clause.</param>
    public FromPart(SqlSegment segment)
        : base("From", segment)
    {
    }
}

/// <summary>
/// Represents the INTO clause of a SQL statement.
/// </summary>
public sealed class IntoPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntoPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the INTO clause.</param>
    public IntoPart(SqlSegment segment)
        : base("Into", segment)
    {
    }
}

/// <summary>
/// Represents the WHERE clause of a SQL statement.
/// </summary>
public sealed class WherePart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WherePart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the WHERE clause.</param>
    public WherePart(SqlSegment segment)
        : base("Where", segment)
    {
    }
}

/// <summary>
/// Represents the GROUP BY clause of a SQL statement.
/// </summary>
public sealed class GroupByPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupByPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the GROUP BY clause.</param>
    public GroupByPart(SqlSegment segment)
        : base("GroupBy", segment)
    {
    }
}

/// <summary>
/// Represents the HAVING clause of a SQL statement.
/// </summary>
public sealed class HavingPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HavingPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the HAVING clause.</param>
    public HavingPart(SqlSegment segment)
        : base("Having", segment)
    {
    }
}

/// <summary>
/// Represents the ORDER BY clause of a SQL statement.
/// </summary>
public sealed class OrderByPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the ORDER BY clause.</param>
    public OrderByPart(SqlSegment segment)
        : base("OrderBy", segment)
    {
    }
}

/// <summary>
/// Represents the LIMIT clause of a SQL statement.
/// </summary>
public sealed class LimitPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LimitPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the LIMIT clause.</param>
    public LimitPart(SqlSegment segment)
        : base("Limit", segment)
    {
    }
}

/// <summary>
/// Represents the OFFSET clause of a SQL statement.
/// </summary>
public sealed class OffsetPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the OFFSET clause.</param>
    public OffsetPart(SqlSegment segment)
        : base("Offset", segment)
    {
    }
}

/// <summary>
/// Represents trailing set operator content such as UNION clauses.
/// </summary>
public sealed class TailPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TailPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the trailing set operator clause.</param>
    public TailPart(SqlSegment segment)
        : base("Tail", segment)
    {
    }
}

/// <summary>
/// Represents the VALUES clause of a SQL statement.
/// </summary>
public sealed class ValuesPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValuesPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the VALUES clause.</param>
    public ValuesPart(SqlSegment segment)
        : base("Values", segment)
    {
    }
}

/// <summary>
/// Represents a DELETE clause referencing the target to remove rows from.
/// </summary>
public sealed class DeletePart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the DELETE clause.</param>
    public DeletePart(SqlSegment segment)
        : base("Delete", segment)
    {
    }
}

/// <summary>
/// Represents an UPDATE clause referencing the target to modify rows in.
/// </summary>
public sealed class UpdatePart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the UPDATE clause.</param>
    public UpdatePart(SqlSegment segment)
        : base("Update", segment)
    {
    }
}

/// <summary>
/// Represents an INSERT clause identifying the target of the operation.
/// </summary>
public sealed class InsertPart : SqlStatementPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsertPart"/> class.
    /// </summary>
    /// <param name="segment">The segment describing the INSERT clause.</param>
    public InsertPart(SqlSegment segment)
        : base("Insert", segment)
    {
    }
}
