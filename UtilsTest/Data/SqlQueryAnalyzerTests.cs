using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="SqlQueryAnalyzer"/>.
/// </summary>
[TestClass]
public sealed class SqlQueryAnalyzerTests
{
    [TestMethod]
    public void ParseSelectWithCteAndSubqueries()
    {
        const string sql = @"WITH recent_orders AS (
SELECT o.id, o.customer_id
FROM orders o
WHERE o.created_at > CURRENT_DATE - INTERVAL '7 day'
)
SELECT c.id,
       (SELECT COUNT(*) FROM order_items oi WHERE oi.order_id = ro.id) AS item_count
FROM customers c
JOIN recent_orders ro ON ro.customer_id = c.id
WHERE EXISTS (
    SELECT 1
    FROM invoices i
    WHERE i.customer_id = c.id
)
ORDER BY c.id;";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);
        Assert.IsNotNull(query);
        Assert.AreEqual(4, query.AllStatements.Count);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlSelectStatement));
        var select = (SqlSelectStatement)query.RootStatement;
        Assert.IsNotNull(select.WithClause);
        Assert.AreEqual(1, select.WithClause!.Definitions.Count);
        Assert.AreEqual("recent_orders", select.WithClause!.Definitions[0].Name);
        Assert.AreEqual("c.id, (SELECT COUNT(*) FROM order_items oi WHERE oi.order_id = ro.id) AS item_count", select.Select.ToSql());
        Assert.IsNotNull(select.From);
        Assert.IsTrue(select.From!.ToSql().Contains("JOIN recent_orders"));
        Assert.IsNotNull(select.Where);
        Assert.AreEqual(1, select.Select.Subqueries.Count());
        Assert.AreEqual(1, select.Where!.Subqueries.Count());

        string rebuilt = query.ToSql();
        const string expected = "WITH recent_orders AS (SELECT o.id, o.customer_id FROM orders o WHERE o.created_at > CURRENT_DATE - INTERVAL '7 day') SELECT c.id, (SELECT COUNT(*) FROM order_items oi WHERE oi.order_id = ro.id) AS item_count FROM customers c JOIN recent_orders ro ON ro.customer_id = c.id WHERE EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = c.id) ORDER BY c.id";
        Assert.AreEqual(expected, rebuilt);
    }

    [TestMethod]
    public void ParseInsertWithValuesAndReturning()
    {
        const string sql = "INSERT INTO products (name, price) VALUES ('Widget', 9.99) RETURNING id;";
        SqlQuery query = SqlQueryAnalyzer.Parse(sql);
        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlInsertStatement));
        var insert = (SqlInsertStatement)query.RootStatement;
        Assert.AreEqual("products(name, price)", insert.Target.ToSql());
        Assert.IsNotNull(insert.Values);
        Assert.AreEqual("('Widget', 9.99)", insert.Values!.ToSql());
        Assert.IsNotNull(insert.Returning);
        Assert.AreEqual("id", insert.Returning!.ToSql());
        Assert.AreEqual("INSERT INTO products(name, price) VALUES ('Widget', 9.99) RETURNING id", query.ToSql());
    }

    [TestMethod]
    public void ParseUpdateWithSubquery()
    {
        const string sql = @"UPDATE accounts a
SET balance = balance + (SELECT SUM(amount) FROM payments p WHERE p.account_id = a.id)
FROM adjustments adj
WHERE adj.account_id = a.id
RETURNING a.id;";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);
        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlUpdateStatement));
        var update = (SqlUpdateStatement)query.RootStatement;
        Assert.AreEqual("accounts a", update.Target.ToSql());
        Assert.AreEqual("balance = balance + (SELECT SUM(amount) FROM payments p WHERE p.account_id = a.id)", update.Set.ToSql());
        Assert.IsNotNull(update.From);
        Assert.AreEqual("adjustments adj", update.From!.ToSql());
        Assert.IsNotNull(update.Where);
        Assert.AreEqual("adj.account_id = a.id", update.Where!.ToSql());
        Assert.IsNotNull(update.Returning);
        Assert.AreEqual("a.id", update.Returning!.ToSql());
        Assert.AreEqual(1, update.Set.Subqueries.Count());
    }

    [TestMethod]
    public void ParseDeleteWithUsing()
    {
        const string sql = @"DELETE FROM sessions s
USING users u
WHERE u.id = s.user_id
RETURNING s.id;";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);
        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlDeleteStatement));
        var delete = (SqlDeleteStatement)query.RootStatement;
        Assert.IsNull(delete.Target);
        Assert.AreEqual("sessions s", delete.From.ToSql());
        Assert.IsNotNull(delete.Using);
        Assert.AreEqual("users u", delete.Using!.ToSql());
        Assert.IsNotNull(delete.Where);
        Assert.AreEqual("u.id = s.user_id", delete.Where!.ToSql());
        Assert.IsNotNull(delete.Returning);
        Assert.AreEqual("s.id", delete.Returning!.ToSql());
        Assert.AreEqual("DELETE FROM sessions s USING users u WHERE u.id = s.user_id RETURNING s.id", query.ToSql());
    }

    [TestMethod]
    public void ParsePreservesParameterAndTempPrefixes()
    {
        const string sql = "SELECT * FROM #temp WHERE Id = @id";

        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        Assert.IsInstanceOfType(query.RootStatement, typeof(SqlSelectStatement));
        Assert.AreEqual(sql, query.ToSql());
    }

    [TestMethod]
    public void ParseSupportsCustomParameterPrefixes()
    {
        const string sql = "SELECT * FROM accounts WHERE id = :account_id";
        var syntaxOptions = new SqlSyntaxOptions(new[] { ':', '@' }, ':');

        SqlQuery query = SqlQueryAnalyzer.Parse(sql, syntaxOptions);

        Assert.AreSame(syntaxOptions, query.SyntaxOptions);
        Assert.AreEqual(sql, query.ToSql());
    }

    [TestMethod]
    public void ToSqlSupportsFormattingModes()
    {
        const string sql = "SELECT table1.champ1, table2.champ2, table2.champ3 FROM table1 INNER JOIN table2 ON table1.champ1 = table2.champ1";
        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        string prefixed = query.ToSql(new SqlFormattingOptions(SqlFormattingMode.Prefixed));
        const string expectedPrefixed = "SELECT\n    table1.champ1\n   ,table2.champ2\n   ,table2.champ3\nFROM table1\nINNER JOIN table2 ON table1.champ1 = table2.champ1";
        Assert.AreEqual(expectedPrefixed.Replace("\n", Environment.NewLine), prefixed);

        string suffixed = query.ToSql(new SqlFormattingOptions(SqlFormattingMode.Suffixed));
        const string expectedSuffixed = "SELECT\n    table1.champ1,\n    table2.champ2,\n    table2.champ3\nFROM table1\nINNER JOIN table2 ON table1.champ1 = table2.champ1";
        Assert.AreEqual(expectedSuffixed.Replace("\n", Environment.NewLine), suffixed);

        string prefixedWithIndent = query.ToSql(new SqlFormattingOptions(SqlFormattingMode.Prefixed, 2));
        const string expectedPrefixedIndent = "SELECT\n  table1.champ1\n ,table2.champ2\n ,table2.champ3\nFROM table1\nINNER JOIN table2 ON table1.champ1 = table2.champ1";
        Assert.AreEqual(expectedPrefixedIndent.Replace("\n", Environment.NewLine), prefixedWithIndent);
    }

    [TestMethod]
    public void CanAppendElementsToSegments()
    {
        const string sql = "SELECT table1.champ1 FROM table1";
        SqlQuery query = SqlQueryAnalyzer.Parse(sql);

        var select = (SqlSelectStatement)query.RootStatement;
        select.Select.AddCommaSeparatedElement("table1.champ2");

        SqlSegment whereSegment = select.EnsureWhereSegment();
        whereSegment.AddConjunction("AND", "table1.champ1 IS NOT NULL");

        SqlSegment orderBySegment = select.EnsureOrderBySegment();
        orderBySegment.AddCommaSeparatedElement("table1.champ1");

        const string expected = "SELECT table1.champ1, table1.champ2 FROM table1 WHERE table1.champ1 IS NOT NULL ORDER BY table1.champ1";
        Assert.AreEqual(expected, query.ToSql());
    }
}
