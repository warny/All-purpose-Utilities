using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Data;
public static class DbCommandExtensions
{
	public static IDbDataParameter CreateParameter(this IDbCommand dbCommand, string name, DbType dbType, object value)
	{
		var parameter = dbCommand.CreateParameter();
		parameter.ParameterName = name;
		parameter.DbType = dbType;
		parameter.Value = value;
		return parameter;
	}

	public static IDbDataParameter AddNewParameter(this IDbCommand dbCommand, string name, DbType dbType, object value)
	{
		var parameter = CreateParameter(dbCommand, name, dbType, value.ToDbValue());
		dbCommand.Parameters.Add(parameter);
		return parameter;
	}

	public static IDbDataParameter AddNewParameter(this IDbCommand dbCommand, string name, object value)
	=> dbCommand.AddNewParameter(name, DataUtils.GetDbType(value), value);


}
