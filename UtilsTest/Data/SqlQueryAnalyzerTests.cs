using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="SqlQueryAnalyzer"/> using the Utils.Parser-backed SQL parser.
/// </summary>
[TestClass]
public sealed class SqlQueryAnalyzerTests
{
    [TestMethod]
    public void ParseSimpleSelectWithoutClauses()
    {
        const string sql = "SELECT id, name";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlSelectStatement));
        var select = (SqlSelectStatement)query.RootStatement;
        Assert.AreEqual("id, name", select.Select.ToSql());
        Assert.AreEqual(sql, query.ToSql());
    }

    [TestMethod]
    public void ParseSimpleUpdateStatement()
    {
        const string sql = "UPDATE accounts SET name = 'x'";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlUpdateStatement));
        var update = (SqlUpdateStatement)query.RootStatement;
        Assert.AreEqual("accounts", update.Target.ToSql());
        Assert.AreEqual("name = 'x'", update.Set.ToSql());
        Assert.AreEqual(sql, query.ToSql());
    }

    [TestMethod]
    public void ParseSimpleDeleteStatement()
    {
        const string sql = "DELETE FROM accounts";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlDeleteStatement));
        var delete = (SqlDeleteStatement)query.RootStatement;
        Assert.AreEqual("accounts", delete.From.ToSql());
        Assert.AreEqual(sql, query.ToSql());
    }

    [TestMethod]
    public void CanAppendElementsToSelectSegments()
    {
        SqlQuery query = SqlQueryAnalyzer.Parse("SELECT table1.champ1");
        var select = (SqlSelectStatement)query.RootStatement;

        select.Select.AddCommaSeparatedElement("table1.champ2");
        SqlSegment whereSegment = select.EnsureWhereSegment();
        whereSegment.AddConjunction("AND", "table1.champ1 IS NOT NULL");
        SqlSegment orderBySegment = select.EnsureOrderBySegment();
        orderBySegment.AddCommaSeparatedElement("table1.champ1");

        Assert.AreEqual(
            "SELECT table1.champ1, table1.champ2 WHERE table1.champ1 IS NOT NULL ORDER BY table1.champ1",
            query.ToSql());
    }
}
