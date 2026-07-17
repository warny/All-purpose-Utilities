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

    /// <summary>
    /// Parses and formats the specified SQL text using the provided formatting options,
    /// normalizing line endings to <c>\n</c>.
    /// </summary>
    /// <param name="sql">The SQL text to parse and format.</param>
    /// <param name="options">The formatting options to apply.</param>
    /// <returns>The formatted SQL with normalized line endings.</returns>
    private static string Format(string sql, SqlFormattingOptions options)
        => SqlQueryAnalyzer.Parse(sql).ToSql(options).Replace("\r\n", "\n");

    // ---- Inline mode ----

    /// <summary>
    /// Verifies that inline mode produces a single-line string with no line breaks.
    /// </summary>
    [TestMethod]
    public void Inline_DoesNotAddNewlines()
    {
        string result = SqlQueryAnalyzer.Parse("SELECT a, b FROM t WHERE x = 1").ToSql();
        Assert.IsFalse(result.Contains('\n'), "Inline mode must not introduce line breaks");
        Assert.IsFalse(result.Contains('\r'), "Inline mode must not introduce line breaks");
        Assert.AreEqual("SELECT a, b FROM t WHERE x = 1", result);
    }

    // ---- Prefixed mode: SELECT column list ----

    /// <summary>
    /// Verifies that prefixed mode places the comma at the start of each subsequent line for a two-column SELECT.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode places the comma at the start of each subsequent line for a three-column SELECT.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode places FROM and WHERE each on its own line.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode indents GROUP BY items.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode places the comma at the start of each subsequent GROUP BY item.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode places the comma at the start of each subsequent ORDER BY item.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode places a JOIN clause on its own line.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode indents SET items in an UPDATE statement.
    /// </summary>
    [TestMethod]
    public void Prefixed_UpdateWithSet_SetItemsIndented()
    {
        string result = Format("UPDATE t SET a = 1, b = 2", Prefixed4);
        const string expected =
            "UPDATE t\n" +
            "SET\n" +
            "    a = 1\n" +
            "   ,b = 2";
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Verifies that prefixed mode places WHERE on its own line in an UPDATE statement.
    /// </summary>
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

    /// <summary>
    /// Verifies that prefixed mode applies the configured indent size to comma placement.
    /// </summary>
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

    /// <summary>
    /// Verifies that suffixed mode places the comma at the end of each line for a two-column SELECT.
    /// </summary>
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

    /// <summary>
    /// Verifies that suffixed mode places the comma at the end of each line for a three-column SELECT.
    /// </summary>
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

    /// <summary>
    /// Verifies that suffixed mode indents the FROM table and places WHERE on its own line.
    /// </summary>
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

    /// <summary>
    /// Verifies that suffixed mode places the comma at the end of each GROUP BY item.
    /// </summary>
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

    /// <summary>
    /// Verifies that suffixed mode appends JOIN to the table line and starts the joined table on the next line.
    /// </summary>
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
