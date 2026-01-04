using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for SQL statement part readers such as <see cref="SelectPartReader"/> and <see cref="FromPartReader"/>.
/// </summary>
[TestClass]
public sealed class SqlStatementPartReaderTests
{
    /// <summary>
    /// Ensures the select part reader preserves aliases and stops before the FROM clause.
    /// </summary>
    [TestMethod]
    public void SelectPartReaderStopsBeforeFromClause()
    {
        var parser = SqlParser.Create("amount AS total, tax t FROM sales");
        var reader = new SelectPartReader(parser);
        var fromReader = new FromPartReader(parser);

        var selectSegment = reader.ReadSelectPart(fromReader.ClauseKeyword);

        Assert.AreEqual("amount AS total, tax t", selectSegment.ToSql());
        Assert.AreEqual("FROM", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures the from part reader gathers comma-separated tables and respects where boundaries.
    /// </summary>
    [TestMethod]
    public void FromPartReaderReadsTables()
    {
        var parser = SqlParser.Create("FROM accounts a, orders o WHERE o.account_id = a.id");
        var reader = new FromPartReader(parser);

        var whereReader = new WherePartReader(parser);
        var fromSegment = reader.TryReadFromPart(whereReader.ClauseKeyword);

        Assert.IsNotNull(fromSegment);
        Assert.AreEqual("accounts a, orders o", fromSegment!.ToSql());
        Assert.AreEqual("WHERE", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures the where part reader reads predicates up to set operators or statement boundaries.
    /// </summary>
    [TestMethod]
    public void WherePartReaderStopsAtClause()
    {
        var parser = SqlParser.Create("WHERE price > 100 UNION SELECT 1");
        var reader = new WherePartReader(parser);

        var whereSegment = reader.TryReadWherePart();

        Assert.IsNotNull(whereSegment);
        Assert.AreEqual("price > 100", whereSegment!.ToSql());
        Assert.AreEqual("UNION", parser.Peek().Normalized);
    }

    /// <summary>
    /// Ensures clause keywords are exposed for coordinating terminators between part readers.
    /// </summary>
    [TestMethod]
    public void ClauseKeywordsAreExposed()
    {
        var parser = SqlParser.Create("LIMIT 5");
        var limitReader = new LimitPartReader(parser);
        var offsetReader = new OffsetPartReader(parser);

        Assert.AreEqual(ClauseStart.Limit, limitReader.ClauseKeyword);
        Assert.AreEqual(ClauseStart.Offset, offsetReader.ClauseKeyword);
    }

    /// <summary>
    /// Ensures clause keyword metadata is available through the registry for clause start detection.
    /// </summary>
    [TestMethod]
    public void ClauseKeywordRegistryExposesPartReaderKeywords()
    {
        var orderByKeywords = ClauseStartKeywordRegistry.KnownClauseKeywords[ClauseStart.OrderBy];

        Assert.AreEqual(1, orderByKeywords.Count);
        CollectionAssert.AreEqual(new[] { "ORDER", "BY" }, orderByKeywords.Single().ToArray());
    }

    /// <summary>
    /// Ensures part references are created using the metadata exposed by part readers instead of hardcoded switches.
    /// </summary>
    [TestMethod]
    public void PartReferencesUseReaderMetadata()
    {
        var select = new SqlSelectStatement(
            SqlSegment.CreateEmpty("Select", SqlSyntaxOptions.Default),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false);

        var whereSegment = select.EnsureWhereSegment();

        Assert.IsNotNull(whereSegment);
        Assert.IsNotNull(select.WherePart);
        Assert.AreSame(whereSegment, select.WherePart!.Segment);
        Assert.AreEqual(WherePartReader.PartName, select.WherePart.Name);
    }
}
