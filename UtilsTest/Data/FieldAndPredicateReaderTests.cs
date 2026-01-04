using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="FieldListReader"/> and <see cref="PredicateReader"/>.
/// </summary>
[TestClass]
public sealed class FieldAndPredicateReaderTests
{
    /// <summary>
    /// Ensures the field list reader collects identifiers until a clause boundary is reached.
    /// </summary>
    [TestMethod]
    public void FieldListReaderStopsAtClauseBoundary()
    {
        var parser = SqlParser.Create("id, name, created_at FROM accounts");
        var reader = new FieldListReader(parser);

        var fields = reader.ReadFields("Field", ClauseStart.From);

        Assert.AreEqual(3, fields.Count);
        Assert.AreEqual("id", fields[0].ToSql());
        Assert.AreEqual("name", fields[1].ToSql());
        Assert.AreEqual("created_at", fields[2].ToSql());
        Assert.AreEqual("FROM", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures predicate reading stops before the next clause and preserves logical operators.
    /// </summary>
    [TestMethod]
    public void PredicateReaderReadsComplexPredicate()
    {
        var parser = SqlParser.Create("price > 100 AND (status = 'A' OR status = 'B') GROUP BY region");
        var reader = new PredicateReader(parser);

        var predicate = reader.ReadPredicate("Where", ClauseStart.GroupBy);

        Assert.AreEqual("price > 100 AND(status = 'A' OR status = 'B')", predicate.ToSql());
        Assert.AreEqual("GROUP", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures predicates support IN value lists without consuming the following clause.
    /// </summary>
    [TestMethod]
    public void PredicateReaderHandlesInValueList()
    {
        var parser = SqlParser.Create("country IN ('FR', 'US', 'DE') ORDER BY country");
        var reader = new PredicateReader(parser);

        var predicate = reader.ReadPredicate("Where", ClauseStart.OrderBy);

        Assert.AreEqual("country IN ('FR', 'US', 'DE')", predicate.ToSql());
        Assert.AreEqual("ORDER", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures predicates support Oracle-style row value lists and IN subqueries.
    /// </summary>
    [TestMethod]
    public void PredicateReaderHandlesRowValueListAndSubquery()
    {
        var parser = SqlParser.Create("(col1, col2) IN ((1, 2), (3, 4)) OR (col1, col2) IN (SELECT a, b FROM dual)");
        var reader = new PredicateReader(parser);

        var predicate = reader.ReadPredicate("Where", ClauseStart.StatementEnd);

        Assert.AreEqual("(col1, col2) IN ((1, 2), (3, 4)) OR(col1, col2) IN (SELECT a, b FROM dual)", predicate.ToSql());
        Assert.IsTrue(parser.IsAtEnd);
    }
}
