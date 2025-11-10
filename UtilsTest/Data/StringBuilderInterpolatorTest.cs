using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using System.Linq;
using Utils.Data;
using Utils.Data.Sql;

namespace UtilsTests.Data;

[TestClass]
public class SqlInterpolatorTest
{
    private sealed class FakeParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; }
        public string SourceColumn { get; set; }
        public DataRowVersion SourceVersion { get; set; }
        public object Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    private sealed class FakeParameterCollection : System.Collections.Generic.List<IDataParameter>, IDataParameterCollection
    {
        public bool Contains(string parameterName) => this.Any(p => p.ParameterName == parameterName);
        public int IndexOf(string parameterName) => this.FindIndex(p => p.ParameterName == parameterName);
        public void RemoveAt(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index >= 0) RemoveAt(index);
        }
        object IDataParameterCollection.this[string parameterName]
        {
            get => this[IndexOf(parameterName)];
            set => this[IndexOf(parameterName)] = (IDataParameter)value;
        }
    }

    private sealed class FakeCommand : IDbCommand
    {
        private readonly FakeParameterCollection _parameters = new();
        public string CommandText { get; set; }
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection Connection { get; set; }
        public IDataParameterCollection Parameters => _parameters;
        public IDbTransaction Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }
        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new FakeParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotImplementedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();
        public object ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class FakeConnection : IDbConnection
    {
        public string ConnectionString { get; set; }
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotImplementedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => new FakeCommand();
        public void Dispose() { }
        public void Open() { }
    }

    [TestMethod]
    public void CreateCommandBindsParameters()
    {
        IDbConnection connection = new FakeConnection();
        string arg1 = "text";
        int arg2 = 42;

        IDbCommand command = connection.CreateCommand($"SELECT * FROM table WHERE fld1 = {arg1} AND fld2 = {arg2}");

        Assert.AreEqual("SELECT * FROM table WHERE fld1 =  @p0  AND fld2 =  @p1 ", command.CommandText);
        Assert.AreEqual(2, command.Parameters.Count);

        var p0 = (IDbDataParameter)command.Parameters[0];
        var p1 = (IDbDataParameter)command.Parameters[1];
        Assert.AreEqual("@p0", p0.ParameterName);
        Assert.AreEqual(arg1, p0.Value);
        Assert.AreEqual(DbType.String, p0.DbType);
        Assert.AreEqual("@p1", p1.ParameterName);
        Assert.AreEqual(arg2, p1.Value);
        Assert.AreEqual(DbType.Int32, p1.DbType);
    }

    [TestMethod]
    public void CreateCommandUsesCustomPrefix()
    {
        IDbConnection connection = new FakeConnection();
        var options = new SqlSyntaxOptions(new[] { ':', '@' }, ':');
        string value = "sample";

        IDbCommand command = connection.CreateCommand(options, $"SELECT * FROM data WHERE col = {value}");

        Assert.AreEqual("SELECT * FROM data WHERE col =  :p0 ", command.CommandText);
        Assert.AreEqual(1, command.Parameters.Count);

        var parameter = (IDbDataParameter)command.Parameters[0];
        Assert.AreEqual(":p0", parameter.ParameterName);
        Assert.AreEqual(value, parameter.Value);
    }
}