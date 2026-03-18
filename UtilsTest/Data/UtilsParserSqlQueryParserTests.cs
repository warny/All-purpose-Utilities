using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests the SQL parser integration that prefers the generated <c>Utils.Parser</c> grammar.
/// </summary>
[TestClass]
public sealed class UtilsParserSqlQueryParserTests
{
    [TestMethod]
    public void ParseSimpleSelectStatement()
    {
        const string sql = "SELECT id, name";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlSelectStatement));
        var statement = (SqlSelectStatement)query.RootStatement;
        Assert.AreEqual("id, name", statement.Select.ToSql());
        Assert.AreEqual(sql, query.ToSql());
    }
}
