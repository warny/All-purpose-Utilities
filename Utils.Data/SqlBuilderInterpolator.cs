using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Collections;
using Utils.Data.Sql;

namespace Utils.Data;

/// <summary>
/// Interpolated string handler used to build parameterized SQL statements.
/// </summary>
[InterpolatedStringHandler]
public class SqlBuilderInterpolator
{
    /// <summary>
    /// Buffer used to build the final SQL command text.
    /// </summary>
    private readonly StringBuilder _sqlQuery = new();

    /// <summary>
    /// Cache of created parameters indexed by argument name.
    /// </summary>
    private readonly Dictionary<string, IDbDataParameter> _parameters = new();

    /// <summary>
    /// Stores syntax options controlling parameter prefixes.
    /// </summary>
    private readonly SqlSyntaxOptions _syntaxOptions;

    /// <summary>
    /// Command associated with this interpolator. Parameters will be created on this command.
    /// </summary>
    public IDbCommand DbCommand { get; }

    private int _parameterIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlBuilderInterpolator"/> class.
    /// </summary>
    /// <param name="literalLength">Provided by the compiler to optimize the size of the string builder.</param>
    /// <param name="formattedCount">Number of formatted placeholders in the interpolation.</param>
    /// <param name="dbConnection">Connection used to create the underlying command.</param>
    /// <param name="syntaxOptions">Syntax options controlling parameter prefixes.</param>
    public SqlBuilderInterpolator(int literalLength, int formattedCount, DbConnection dbConnection, SqlSyntaxOptions? syntaxOptions = null)
        : this(literalLength, formattedCount, (IDbConnection)(dbConnection ?? throw new ArgumentNullException(nameof(dbConnection))), syntaxOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlBuilderInterpolator"/> class.
    /// </summary>
    /// <param name="literalLength">Provided by the compiler to optimize the size of the string builder.</param>
    /// <param name="formattedCount">Number of formatted placeholders in the interpolation.</param>
    /// <param name="dbConnection">Connection used to create the underlying command.</param>
    /// <param name="syntaxOptions">Syntax options controlling parameter prefixes.</param>
    public SqlBuilderInterpolator(int literalLength, int formattedCount, IDbConnection dbConnection, SqlSyntaxOptions? syntaxOptions = null)
        : this(literalLength, formattedCount, (dbConnection ?? throw new ArgumentNullException(nameof(dbConnection))).CreateCommand(), syntaxOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlBuilderInterpolator"/> class.
    /// </summary>
    /// <param name="literalLength">Provided by the compiler to optimize the size of the string builder.</param>
    /// <param name="formattedCount">Number of formatted placeholders in the interpolation.</param>
    /// <param name="dbCommand">Command to modify.</param>
    /// <param name="syntaxOptions">Syntax options controlling parameter prefixes.</param>
    public SqlBuilderInterpolator(int literalLength, int formattedCount, IDbCommand dbCommand, SqlSyntaxOptions? syntaxOptions = null)
    {
        DbCommand = dbCommand ?? throw new ArgumentNullException(nameof(dbCommand));
        DbCommand.Parameters.Clear();
        _syntaxOptions = syntaxOptions ?? SqlSyntaxOptions.Default;
        _parameterIndex = 0;
    }

    /// <summary>
    /// Gets the syntax options controlling parameter names for the interpolator.
    /// </summary>
    public SqlSyntaxOptions SyntaxOptions => _syntaxOptions;

    /// <summary>
    /// Appends a literal piece of SQL to the internal buffer.
    /// </summary>
    /// <param name="s">The literal string to append.</param>
    public void AppendLiteral(string s)
    {
        _sqlQuery.Append(s);
    }

    private void AddParameter(IDbDataParameter parameter)
    {
        _sqlQuery.Append(' ');
        _sqlQuery.Append(parameter.ParameterName);
        _sqlQuery.Append(' ');
    }

    /// <summary>
    /// Adds a formatted value to the SQL query and ensures a parameter is created.
    /// </summary>
    /// <typeparam name="T">Type of the value to append.</typeparam>
    /// <param name="value">The value being interpolated.</param>
    /// <param name="name">Name of the argument being interpolated.</param>
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        string parameterName;
        do
        {
            parameterName = $"{_syntaxOptions.AutoParameterPrefix}p{_parameterIndex++}";
        }
        while (DbCommand.Parameters.Contains(parameterName));

        AddParameter(_parameters.GetOrAdd(name, () => DbCommand.AddNewParameter(parameterName, DataUtils.GetDbType(typeof(T)), value)));
    }

    /// <summary>
    /// Returns the built SQL query.
    /// </summary>
    /// <returns>The SQL string accumulated so far.</returns>
    public override string ToString() => _sqlQuery.ToString();
}
