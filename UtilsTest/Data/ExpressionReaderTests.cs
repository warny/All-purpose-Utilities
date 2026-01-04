using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for <see cref="ExpressionReader"/> and <see cref="ExpressionListReader"/>.
/// </summary>
[TestClass]
public sealed class ExpressionReaderTests
{
    [TestMethod]
    public void ExpressionReaderReadsExpressionWithExplicitAlias()
    {
        var parser = SqlParser.Create("amount * 1.2 AS gross, discount");
        var reader = new ExpressionReader(parser);

        var result = reader.ReadExpression("SelectExpr", true, ClauseStart.StatementEnd);

        Assert.AreEqual("amount * 1.2", result.Expression.ToSql());
        Assert.AreEqual("gross", result.Alias);
        Assert.AreEqual(",", parser.Peek().Text);
    }

    [TestMethod]
    public void ExpressionListReaderStopsAtClauseAndHandlesImplicitAlias()
    {
        var parser = SqlParser.Create("orders.total, SUM(quantity) qty FROM sales");
        var listReader = new ExpressionListReader(parser);

        var expressions = listReader.ReadExpressions("SelectExpr", true, ClauseStart.From);

        Assert.AreEqual(2, expressions.Count);
        Assert.AreEqual("orders.total", expressions[0].Expression.ToSql());
        Assert.IsNull(expressions[0].Alias);
        Assert.AreEqual("SUM(quantity)", expressions[1].Expression.ToSql());
        Assert.AreEqual("qty", expressions[1].Alias);
        Assert.AreEqual("FROM", parser.Peek().Normalized);
    }

    [TestMethod]
    public void ExpressionReaderHandlesIfFunctionWithAlias()
    {
        var parser = SqlParser.Create("IF(total > 0, total, 0) AS total_value FROM orders");
        var reader = new ExpressionReader(parser);

        var result = reader.ReadExpression("SelectExpr", true, ClauseStart.From);

        Assert.AreEqual("IF(total > 0, total, 0)", result.Expression.ToSql());
        Assert.AreEqual("total_value", result.Alias);
        Assert.AreEqual("FROM", parser.Peek().Normalized);
    }

    [TestMethod]
    public void ExpressionReaderHandlesSearchedCaseExpression()
    {
        var parser = SqlParser.Create("CASE WHEN qty > 0 THEN price * qty WHEN qty = 0 THEN 0 ELSE NULL END revenue, tax");
        var reader = new ExpressionReader(parser);

        var result = reader.ReadExpression("SelectExpr", true, ClauseStart.StatementEnd);

        Assert.AreEqual("CASE WHEN qty > 0 THEN price * qty WHEN qty = 0 THEN 0 ELSE NULL END", result.Expression.ToSql());
        Assert.AreEqual("revenue", result.Alias);
        Assert.AreEqual(",", parser.Peek().Text);
    }

    [TestMethod]
    public void ExpressionReaderHandlesSimpleCaseExpression()
    {
        var parser = SqlParser.Create("CASE status WHEN 'NEW' THEN 1 WHEN 'OLD' THEN 2 ELSE 0 END AS status_code FROM orders");
        var reader = new ExpressionReader(parser);

        var result = reader.ReadExpression("SelectExpr", true, ClauseStart.From);

        Assert.AreEqual("CASE status WHEN 'NEW' THEN 1 WHEN 'OLD' THEN 2 ELSE 0 END", result.Expression.ToSql());
        Assert.AreEqual("status_code", result.Alias);
        Assert.AreEqual("FROM", parser.Peek().Normalized);
    }
}
