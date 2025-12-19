using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

/// <summary>
/// Provides configuration options controlling how SQL identifiers and parameters are interpreted.
/// </summary>
public sealed class SqlSyntaxOptions
{
    private static readonly char[] SqlServerIdentifierPrefixes = { '@', '#', '$' };
    private static readonly char[] OracleIdentifierPrefixes = { ':' };
    private static readonly char[] MySqlIdentifierPrefixes = { '@' };
    private static readonly char[] SqliteIdentifierPrefixes = { '@', ':', '$', '?' };
    private static readonly char[] PostgreSqlIdentifierPrefixes = { '$' };

    private readonly HashSet<char> identifierPrefixes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlSyntaxOptions"/> class.
    /// </summary>
    /// <param name="identifierPrefixes">Characters that can prefix identifiers such as parameters or temporary tables.</param>
    /// <param name="autoParameterPrefix">The prefix used when automatically generating parameter names.</param>
    public SqlSyntaxOptions(IEnumerable<char>? identifierPrefixes = null, char autoParameterPrefix = '@')
    {
        var resolvedPrefixes = identifierPrefixes == null
            ? new HashSet<char>(SqlServerIdentifierPrefixes)
            : new HashSet<char>(identifierPrefixes);

        if (resolvedPrefixes.Count == 0)
        {
            throw new ArgumentException("At least one identifier prefix must be specified.", nameof(identifierPrefixes));
        }

        resolvedPrefixes.Add(autoParameterPrefix);

        this.identifierPrefixes = resolvedPrefixes;
        AutoParameterPrefix = autoParameterPrefix;
    }

    /// <summary>
    /// Gets syntax options configured for Microsoft SQL Server.
    /// </summary>
    public static SqlSyntaxOptions SqlServer { get; } = new SqlSyntaxOptions(SqlServerIdentifierPrefixes, '@');

    /// <summary>
    /// Gets syntax options configured for Oracle databases.
    /// </summary>
    public static SqlSyntaxOptions Oracle { get; } = new SqlSyntaxOptions(OracleIdentifierPrefixes, ':');

    /// <summary>
    /// Gets syntax options configured for MySQL databases.
    /// </summary>
    public static SqlSyntaxOptions MySql { get; } = new SqlSyntaxOptions(MySqlIdentifierPrefixes, '@');

    /// <summary>
    /// Gets syntax options configured for SQLite databases.
    /// </summary>
    public static SqlSyntaxOptions Sqlite { get; } = new SqlSyntaxOptions(SqliteIdentifierPrefixes, '@');

    /// <summary>
    /// Gets syntax options configured for PostgreSQL databases.
    /// </summary>
    public static SqlSyntaxOptions PostgreSql { get; } = new SqlSyntaxOptions(PostgreSqlIdentifierPrefixes, '$');

    /// <summary>
    /// Gets the default syntax options supporting common SQL Server style prefixes.
    /// </summary>
    public static SqlSyntaxOptions Default { get; } = SqlServer;

    /// <summary>
    /// Gets the characters that can prefix identifiers.
    /// </summary>
    public IReadOnlyCollection<char> IdentifierPrefixes => identifierPrefixes;

    /// <summary>
    /// Gets the prefix appended to automatically generated parameter names.
    /// </summary>
    public char AutoParameterPrefix { get; }

    /// <summary>
    /// Determines whether the provided character is configured as an identifier prefix.
    /// </summary>
    /// <param name="value">The character to check.</param>
    /// <returns><c>true</c> when the character is a known prefix; otherwise, <c>false</c>.</returns>
    public bool IsIdentifierPrefix(char value) => identifierPrefixes.Contains(value);
}
