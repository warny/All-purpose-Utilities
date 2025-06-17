using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Utils.Collections;
using Utils.Objects;

namespace Utils.Data;

[InterpolatedStringHandler]
public class SqlBuilderInterpolator
{
	readonly StringBuilder _sqlQuery = new();
	readonly Dictionary<string, IDbDataParameter> _parameters = [];

	public IDbCommand DbCommand { get; }

	public SqlBuilderInterpolator(int literalLength, int formattedCount, IDbConnection dbConnection)
	{
		DbCommand = dbConnection.CreateCommand();
	}

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

	public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
		=> AddParameter(_parameters.GetOrAdd(name, () => DbCommand.AddNewParameter($"@p{DbCommand.Parameters.Count}", DataUtils.GetDbType(typeof(T)), value)));

	public override string ToString() => _sqlQuery.ToString();

}

