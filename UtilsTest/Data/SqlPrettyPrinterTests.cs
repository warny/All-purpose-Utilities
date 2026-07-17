using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="SqlPrettyPrinter"/> exercised via <see cref="SqlQueryAnalyzer"/> round-trips.
/// </summary>
[TestClass]
public sealed class SqlPrettyPrinterTests
{
    private static readonly SqlFormattingOptions Prefixed4 = new(SqlFormattingMode.Prefixed, 4);
    private static readonly SqlFormattingOptions Suffixed4 = new(SqlFormattingMode.Suffixed, 4);

    private static string Format(string sql, SqlFormattingOptions options)
        => SqlQueryAnalyzer.Parse(sql).ToSql(options).Replace("\r\n", "\n");

    // ---- Inline mode ----

    [TestMethod]
    public void Inline_DoesNotAddNewlines()
    {
        string result = SqlQueryAnalyzer.Parse("SELECT a, b FROM t WHERE x = 1").ToSql();
        Assert.IsFalse(result.Contains('\n'), "Inline mode must not introduce line breaks");
        Assert.IsFalse(result.Contains('\r'), "Inline mode must not introduce line breaks");
        Assert.AreEqual("SELECT a, b FROM t WHERE x = 1", result);
    }

    // ---- Prefixed mode: SELECT column list ----

    [TestMethod]
    public void Prefixed_TwoColumns_CommaAtLineStart()
    {
        string result = Format("SELECT a, b", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "   ,b";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_ThreeColumns_AllCommasAtLineStart()
    {
        string result = Format("SELECT a, b, c", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "   ,b\n" +
            "   ,c";
        Assert.AreEqual(expected, result);
    }

    // ---- Prefixed mode: SELECT with FROM and WHERE ----

    [TestMethod]
    public void Prefixed_WithFromAndWhere_ClausesOnOwnLines()
    {
        string result = Format("SELECT a, b FROM t WHERE x = 1", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "   ,b\n" +
            "FROM t\n" +
            "WHERE x = 1";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_GroupBy_ItemsIndented()
    {
        string result = Format("SELECT a FROM t GROUP BY a", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "FROM t\n" +
            "GROUP BY\n" +
            "    a";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_GroupByWithTwoItems_CommaAtLineStart()
    {
        string result = Format("SELECT a, b FROM t GROUP BY a, b", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "   ,b\n" +
            "FROM t\n" +
            "GROUP BY\n" +
            "    a\n" +
            "   ,b";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_OrderByWithTwoItems_CommaAtLineStart()
    {
        string result = Format("SELECT a FROM t ORDER BY a DESC, b ASC", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "FROM t\n" +
            "ORDER BY\n" +
            "    a DESC\n" +
            "   ,b ASC";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_Join_JoinOnOwnLine()
    {
        string result = Format("SELECT a FROM t JOIN u ON t.id = u.id", Prefixed4);
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "FROM t\n" +
            "JOIN u ON t.id = u.id";
        Assert.AreEqual(expected, result);
    }

    // ---- Prefixed mode: UPDATE statement ----

    [TestMethod]
    public void Prefixed_UpdateWithSet_SetItemsIndented()
    {
        // The grammar extracts SET but not WHERE for UPDATE; WHERE is verified separately.
        string result = Format("UPDATE t SET a = 1, b = 2", Prefixed4);
        const string expected =
            "UPDATE t\n" +
            "SET\n" +
            "    a = 1\n" +
            "   ,b = 2";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Prefixed_UpdateWithSetAndWhere_WhereOnOwnLine()
    {
        string result = Format("UPDATE t SET a = 1, b = 2 WHERE x = 3", Prefixed4);
        const string expected =
            "UPDATE t\n" +
            "SET\n" +
            "    a = 1\n" +
            "   ,b = 2\n" +
            "WHERE x = 3";
        Assert.AreEqual(expected, result);
    }

    // ---- Prefixed mode: IndentSize variation ----

    [TestMethod]
    public void Prefixed_IndentSize2_CommaAtCorrectDepth()
    {
        var options = new SqlFormattingOptions(SqlFormattingMode.Prefixed, 2);
        // IndentSize=2: items at clauseIndent+2=2, comma at clauseIndent+Max(2-1,0)=1
        string result = Format("SELECT a, b", options).Replace("\r\n", "\n");
        const string expected =
            "SELECT\n" +
            "  a\n" +
            " ,b";
        Assert.AreEqual(expected, result);
    }

    // ---- Suffixed mode: SELECT column list ----

    [TestMethod]
    public void Suffixed_TwoColumns_CommaAtLineEnd()
    {
        string result = Format("SELECT a, b", Suffixed4);
        const string expected =
            "SELECT\n" +
            "    a,\n" +
            "    b";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Suffixed_ThreeColumns_AllCommasAtLineEnd()
    {
        string result = Format("SELECT a, b, c", Suffixed4);
        const string expected =
            "SELECT\n" +
            "    a,\n" +
            "    b,\n" +
            "    c";
        Assert.AreEqual(expected, result);
    }

    // ---- Suffixed mode: SELECT with FROM and WHERE ----

    [TestMethod]
    public void Suffixed_WithFromAndWhere_FromTableIndented()
    {
        string result = Format("SELECT a, b FROM t WHERE x = 1", Suffixed4);
        const string expected =
            "SELECT\n" +
            "    a,\n" +
            "    b\n" +
            "FROM\n" +
            "    t\n" +
            "WHERE x = 1";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Suffixed_GroupByWithTwoItems_CommaAtLineEnd()
    {
        string result = Format("SELECT a, b FROM t GROUP BY a, b", Suffixed4);
        const string expected =
            "SELECT\n" +
            "    a,\n" +
            "    b\n" +
            "FROM\n" +
            "    t\n" +
            "GROUP BY\n" +
            "    a,\n" +
            "    b";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Suffixed_Join_RelationAndJoinOnSameLine()
    {
        string result = Format("SELECT a FROM t JOIN u ON t.id = u.id", Suffixed4);
        // Suffixed: FROM puts table on next line; JOIN is appended to the table line,
        // then the joined table starts on the next line.
        const string expected =
            "SELECT\n" +
            "    a\n" +
            "FROM\n" +
            "    t JOIN\n" +
            "    u ON t.id = u.id";
        Assert.AreEqual(expected, result);
    }
}
