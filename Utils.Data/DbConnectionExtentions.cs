using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Data;
public static class DbConnectionExtensions
{
	public static IDbCommand CreateCommand(this IDbConnection dbConnection, [InterpolatedStringHandlerArgument(nameof(dbConnection))]SqlBuilderInterpolator interpolator)
	{
		interpolator.DbCommand.CommandText = interpolator.ToString();
		return interpolator.DbCommand;
	}
}
