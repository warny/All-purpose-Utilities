using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.Collections;

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
    private readonly Dictionary<string, IDbDataParameter> _parameters = [];

    /// <summary>
    /// Command associated with this interpolator. Parameters will be created on this command.
    /// </summary>
    public IDbCommand DbCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlBuilderInterpolator"/> class.
    /// </summary>
    /// <param name="literalLength">Provided by the compiler to optimize the size of the string builder.</param>
    /// <param name="formattedCount">Number of formatted placeholders in the interpolation.</param>
    /// <param name="dbConnection">Connection used to create the underlying command.</param>
    public SqlBuilderInterpolator(int literalLength, int formattedCount, IDbConnection dbConnection)
    {
        DbCommand = dbConnection.CreateCommand();
    }

    /// <summary>
    /// Appends a literal piece of SQL to the internal buffer.
    /// </summary>
    /// <param name="s">The literal string to append.</param>
    public void AppendLiteral(string s)
    {
        _sqlQuery.Append(s);
    }

    private void AddParameter(IDbDataParameter param)
    {
        _sqlQuery.Append(' ');
        _sqlQuery.Append(param.ParameterName);
        _sqlQuery.Append(' ');
    }

    /// <summary>
    /// Adds a formatted value to the SQL query and ensures a parameter is created.
    /// </summary>
    /// <typeparam name="T">Type of the value to append.</typeparam>
    /// <param name="value">The value being interpolated.</param>
    /// <param name="name">Name of the argument being interpolated.</param>
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
            => AddParameter(_parameters.GetOrAdd(name, () => DbCommand.AddNewParameter($"@p{DbCommand.Parameters.Count}", DataUtils.GetDbType(typeof(T)), value)));

    /// <summary>
    /// Returns the built SQL query.
    /// </summary>
    /// <returns>The SQL string accumulated so far.</returns>
    public override string ToString() => _sqlQuery.ToString();
}
