using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="TableListReader"/>.
/// </summary>
[TestClass]
public sealed class TableListReaderTests
{
    /// <summary>
    /// Ensures comma-separated tables stop at the next clause boundary.
    /// </summary>
    [TestMethod]
    public void TableListReaderReadsTablesUntilClause()
    {
        var parser = SqlParser.Create("users u, orders o WHERE status = 'A'");
        var reader = new TableListReader(parser);

        var tables = reader.ReadTables("Table", ClauseStart.Where);

        Assert.AreEqual(2, tables.Count);
        Assert.AreEqual("users u", tables[0].ToSql());
        Assert.AreEqual("orders o", tables[1].ToSql());
        Assert.AreEqual("WHERE", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures join chains remain intact until each JOIN has an ON clause.
    /// </summary>
    [TestMethod]
    public void TableListReaderKeepsJoinedTablesTogether()
    {
        var parser = SqlParser.Create("customers c INNER JOIN orders o ON c.id = o.customer_id LEFT OUTER JOIN items i ON o.item_id = i.id GROUP BY c.id");
        var reader = new TableListReader(parser);

        var tables = reader.ReadTables("Table", ClauseStart.GroupBy);

        Assert.AreEqual(1, tables.Count);
        Assert.AreEqual("customers c INNER JOIN orders o ON c.id = o.customer_id LEFT OUTER JOIN items i ON o.item_id = i.id", tables[0].ToSql());
        Assert.AreEqual("GROUP", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures cross-apply subqueries are included in the table source without requiring ON clauses.
    /// </summary>
    [TestMethod]
    public void TableListReaderHandlesCrossApplyWithSubquery()
    {
        var parser = SqlParser.Create("accounts a CROSS APPLY (SELECT * FROM transactions t WHERE t.account_id = a.id) tx WHERE a.active = 1");
        var reader = new TableListReader(parser);

        var tables = reader.ReadTables("Table", ClauseStart.Where);

        Assert.AreEqual(1, tables.Count);
        Assert.AreEqual("accounts a CROSS APPLY(SELECT * FROM transactions t WHERE t.account_id = a.id) tx", tables[0].ToSql());
        Assert.AreEqual("WHERE", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures missing ON clauses for JOIN operations trigger a parsing exception.
    /// </summary>
    [TestMethod]
    public void TableListReaderThrowsWhenJoinIsIncomplete()
    {
        var parser = SqlParser.Create("users u INNER JOIN orders o WHERE 1 = 1");
        var reader = new TableListReader(parser);

        Assert.ThrowsException<SqlParseException>(() => reader.ReadTables("Table", ClauseStart.Where));
    }
}
