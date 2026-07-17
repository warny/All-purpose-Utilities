using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="SqlPrettyPrinter"/> exercised via <see cref="SqlQueryAnalyzer"/> round-trips.
/// Segments not parsed by the grammar (e.g. FROM/WHERE inside SELECT) are built programmatically
/// via EnsureXxxSegment() + AddRaw() so the formatter can process complete statements.
/// </summary>
[TestClass]
public sealed class SqlPrettyPrinterTests
{
    private static readonly SqlFormattingOptions Prefixed4 = new(SqlFormattingMode.Prefixed, 4);
    private static readonly SqlFormattingOptions Suffixed4 = new(SqlFormattingMode.Suffixed, 4);

    private static string Format(string sql, SqlFormattingOptions options)
        => SqlQueryAnalyzer.Parse(sql).ToSql(options).Replace("\r\n", "\n");

    private static SqlSelectStatement Select(string columns)
    {
        var q = SqlQueryAnalyzer.Parse($"SELECT {columns}");
        return (SqlSelectStatement)q.RootStatement;
    }

    // ---- Inline mode ----

    [TestMethod]
    public void Inline_DoesNotAddNewlines()
    {
        var select = Select("a, b");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureWhereSegment().AddRaw("x = 1");
        string result = select.ToSql();
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

    // ---- Prefixed mode: full SELECT with programmatic segments ----

    [TestMethod]
    public void Prefixed_WithFromAndWhere_ClausesOnOwnLines()
    {
        var select = Select("a, b");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureWhereSegment().AddRaw("x = 1");
        string result = select.ToSql(Prefixed4).Replace("\r\n", "\n");
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
        var select = Select("a");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureGroupBySegment().AddRaw("a");
        string result = select.ToSql(Prefixed4).Replace("\r\n", "\n");
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
        var select = Select("a, b");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureGroupBySegment().AddRaw("a");
        select.EnsureGroupBySegment().AddCommaSeparatedElement("b");
        string result = select.ToSql(Prefixed4).Replace("\r\n", "\n");
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
        var select = Select("a");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureOrderBySegment().AddRaw("a DESC");
        select.EnsureOrderBySegment().AddCommaSeparatedElement("b ASC");
        string result = select.ToSql(Prefixed4).Replace("\r\n", "\n");
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
        var select = Select("a");
        select.EnsureFromSegment().AddRaw("t JOIN u ON t.id = u.id");
        string result = select.ToSql(Prefixed4).Replace("\r\n", "\n");
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
        // Build WHERE programmatically since the grammar does not extract it for UPDATE.
        var query = SqlQueryAnalyzer.Parse("UPDATE t SET a = 1, b = 2");
        var update = (SqlUpdateStatement)query.RootStatement;
        update.EnsureWhereSegment().AddRaw("x = 3");
        string result = update.ToSql(Prefixed4).Replace("\r\n", "\n");
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

    // ---- Suffixed mode: full SELECT with programmatic segments ----

    [TestMethod]
    public void Suffixed_WithFromAndWhere_FromTableIndented()
    {
        var select = Select("a, b");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureWhereSegment().AddRaw("x = 1");
        string result = select.ToSql(Suffixed4).Replace("\r\n", "\n");
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
        var select = Select("a, b");
        select.EnsureFromSegment().AddRaw("t");
        select.EnsureGroupBySegment().AddRaw("a");
        select.EnsureGroupBySegment().AddCommaSeparatedElement("b");
        string result = select.ToSql(Suffixed4).Replace("\r\n", "\n");
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
        var select = Select("a");
        select.EnsureFromSegment().AddRaw("t JOIN u ON t.id = u.id");
        string result = select.ToSql(Suffixed4).Replace("\r\n", "\n");
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
