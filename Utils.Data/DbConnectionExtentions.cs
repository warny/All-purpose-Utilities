using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Utils.Data;

/// <summary>
/// Extension methods for <see cref="IDbConnection"/> and <see cref="DbConnection"/>.
/// </summary>
public static class DbConnectionExtensions
{

	/// <summary>
	/// Creates a command using an interpolated SQL builder.
	/// </summary>
	/// <param name="dbConnection">The connection used to create the command.</param>
	/// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
	/// <returns>A command initialized with text and parameters provided by <paramref name="interpolator"/>.</returns>
	public static IDbCommand CreateCommand(this IDbConnection dbConnection, [InterpolatedStringHandlerArgument(nameof(dbConnection))] SqlBuilderInterpolator interpolator)
	{
		interpolator.DbCommand.CommandText = interpolator.ToString();
		return interpolator.DbCommand;
	}

	/// <summary>
	/// Creates a command using an interpolated SQL builder.
	/// </summary>
	/// <param name="dbConnection">The connection used to create the command.</param>
	/// <param name="interpolator">Interpolator that builds the SQL and parameters.</param>
	/// <returns>A command initialized with text and parameters provided by <paramref name="interpolator"/>.</returns>
	public static DbCommand CreateCommand(this DbConnection dbConnection, [InterpolatedStringHandlerArgument(nameof(dbConnection))] SqlBuilderInterpolator interpolator)
	{
		interpolator.DbCommand.CommandText = interpolator.ToString();
		return (DbCommand)interpolator.DbCommand;
	}
}
