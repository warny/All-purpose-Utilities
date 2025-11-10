using System;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Utils.Data.Sql;

namespace Utils.Data;

/// <summary>
/// Provides helper methods to ease parameter creation for <see cref="IDbCommand"/> instances.
/// </summary>
public static class DbCommandExtensions
{
    /// <summary>
    /// Creates a new parameter and configures it with the provided metadata.
    /// </summary>
    /// <param name="dbCommand">The command for which the parameter is created.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="dbType">The <see cref="DbType"/> of the parameter.</param>
    /// <param name="value">Value of the parameter.</param>
    /// <returns>The configured parameter.</returns>
    public static IDbDataParameter CreateParameter(this IDbCommand dbCommand, string name, DbType dbType, object value)
    {
        var parameter = dbCommand.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        return parameter;
    }

    /// <summary>
    /// Creates a parameter, adds it to the command and returns it.
    /// </summary>
    /// <param name="dbCommand">The command to modify.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="dbType">Type of the parameter.</param>
    /// <param name="value">Value assigned to the parameter.</param>
    /// <returns>The parameter that was added to the command.</returns>
    public static IDbDataParameter AddNewParameter(this IDbCommand dbCommand, string name, DbType dbType, object value)
    {
        var parameter = CreateParameter(dbCommand, name, dbType, value.ToDbValue());
        dbCommand.Parameters.Add(parameter);
        return parameter;
    }

    /// <summary>
    /// Adds a parameter to the command, inferring its type from the provided value.
    /// </summary>
    /// <param name="dbCommand">The command to modify.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="value">Value assigned to the parameter.</param>
    /// <returns>The parameter that was added to the command.</returns>
    public static IDbDataParameter AddNewParameter(this IDbCommand dbCommand, string name, object value)
        => dbCommand.AddNewParameter(name, DataUtils.GetDbType(value), value);

    /// <summary>
    /// Sets the CommandText using an interpolated SQL builder.
    /// </summary>
    /// <param name="dbCommand">The command on which to set the CommandText.</param>
    /// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
    /// <returns>The same command instance for chaining.</returns>
    public static IDbCommand SetCommandText(this IDbCommand dbCommand, [InterpolatedStringHandlerArgument(nameof(dbCommand))] SqlBuilderInterpolator interpolator)
    {
        dbCommand.CommandText = interpolator.ToString();
        return dbCommand;
    }

    /// <summary>
    /// Sets the CommandText using an interpolated SQL builder and custom syntax options.
    /// </summary>
    /// <param name="dbCommand">The command on which to set the CommandText.</param>
    /// <param name="syntaxOptions">Syntax options controlling parameter prefixes.</param>
    /// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
    /// <returns>The same command instance for chaining.</returns>
    public static IDbCommand SetCommandText(this IDbCommand dbCommand, SqlSyntaxOptions syntaxOptions, [InterpolatedStringHandlerArgument(nameof(dbCommand), nameof(syntaxOptions))] SqlBuilderInterpolator interpolator)
    {
        _ = syntaxOptions;
        dbCommand.CommandText = interpolator.ToString();
        return dbCommand;
    }

    /// <summary>
    /// Sets the CommandText using an interpolated SQL builder.
    /// </summary>
    /// <param name="dbCommand">The command on which to set the CommandText.</param>
    /// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
    /// <returns>The same command instance for chaining.</returns>
    public static DbCommand SetCommandText(this DbCommand dbCommand, [InterpolatedStringHandlerArgument(nameof(dbCommand))] SqlBuilderInterpolator interpolator)
    {
        dbCommand.CommandText = interpolator.ToString();
        return dbCommand;
    }

    /// <summary>
    /// Sets the CommandText using an interpolated SQL builder and custom syntax options.
    /// </summary>
    /// <param name="dbCommand">The command on which to set the CommandText.</param>
    /// <param name="syntaxOptions">Syntax options controlling parameter prefixes.</param>
    /// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
    /// <returns>The same command instance for chaining.</returns>
    public static DbCommand SetCommandText(this DbCommand dbCommand, SqlSyntaxOptions syntaxOptions, [InterpolatedStringHandlerArgument(nameof(dbCommand), nameof(syntaxOptions))] SqlBuilderInterpolator interpolator)
    {
        _ = syntaxOptions;
        dbCommand.CommandText = interpolator.ToString();
        return dbCommand;
    }
}
