using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using Utils.Data;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Validates the behavior of <see cref="SqlCommandFactory"/>.
/// </summary>
[TestClass]
public class SqlCommandFactoryTests
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
        public bool Contains(string parameterName) => this.Exists(p => p.ParameterName == parameterName);

        public int IndexOf(string parameterName) => this.FindIndex(p => p.ParameterName == parameterName);

        public void RemoveAt(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }

        object IDataParameterCollection.this[string parameterName]
        {
            get => this[IndexOf(parameterName)];
            set => this[IndexOf(parameterName)] = (IDataParameter)value;
        }
    }

    private sealed class FakeCommand : IDbCommand
    {
        private readonly FakeParameterCollection parameters = new();

        public string CommandText { get; set; }

        public int CommandTimeout { get; set; }

        public CommandType CommandType { get; set; }

        public IDbConnection Connection { get; set; }

        public IDataParameterCollection Parameters => parameters;

        public IDbTransaction Transaction { get; set; }

        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel()
        {
        }

        public IDbDataParameter CreateParameter() => new FakeParameter();

        public void Dispose()
        {
        }

        public int ExecuteNonQuery() => 0;

        public IDataReader ExecuteReader() => throw new NotImplementedException();

        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();

        public object ExecuteScalar() => null;

        public void Prepare()
        {
        }
    }

    private sealed class FakeConnection : IDbConnection
    {
        public string ConnectionString { get; set; }

        public int ConnectionTimeout => 0;

        public string Database => string.Empty;

        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotImplementedException();

        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();

        public void ChangeDatabase(string databaseName)
        {
        }

        public void Close()
        {
        }

        public IDbCommand CreateCommand() => new FakeCommand();

        public void Dispose()
        {
        }

        public void Open()
        {
        }
    }

    /// <summary>
    /// Ensures commands built through the factory honor the configured syntax options without additional parameters.
    /// </summary>
    [TestMethod]
    public void CreateCommandUsesFactorySyntaxOptions()
    {
        var factory = new SqlCommandFactory(new SqlSyntaxOptions(new[] { ':' }, ':'));
        IDbConnection connection = new FakeConnection();
        string name = "factory";

        IDbCommand command = factory.CreateCommand(connection, $"SELECT * FROM data WHERE name = {name}");

        Assert.AreEqual("SELECT * FROM data WHERE name =  :p0 ", command.CommandText);
        Assert.AreEqual(1, command.Parameters.Count);

        var parameter = (IDbDataParameter)command.Parameters[0];
        Assert.AreEqual(":p0", parameter.ParameterName);
        Assert.AreEqual(DbType.String, parameter.DbType);
        Assert.AreEqual(name, parameter.Value);
    }

    /// <summary>
    /// Ensures compiled queries use the syntax options provided to the factory.
    /// </summary>
    [TestMethod]
    public void CompileQueryUsesFactorySyntaxOptions()
    {
        var syntaxOptions = new SqlSyntaxOptions(new[] { ':' }, ':');
        var factory = new SqlCommandFactory(syntaxOptions);

        SqlQuery query = factory.CompileQuery("SELECT * FROM items WHERE id = :id");

        Assert.AreEqual(syntaxOptions, query.SyntaxOptions);
    }
}
