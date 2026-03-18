using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;
using Utils.Parser.Runtime;

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

    [TestMethod]
    public void SqlQueryGrammar_EnablesCaseInsensitiveOption()
    {
        var grammar = SqlQueryGrammar.Build();

        Assert.IsNotNull(grammar.Options);
        Assert.AreEqual("true", grammar.Options!.Values["caseInsensitive"]);
    }

    [TestMethod]
    public void SqlQueryGrammar_LexesLowercaseKeywords()
    {
        var grammar = new CompiledGrammar(SqlQueryGrammar.Build());
        var tokens = grammar.Tokenize("select id, name from accounts").ToList();

        CollectionAssert.AreEqual(
            new[] { "SELECT", "IDENTIFIER", "COMMA", "IDENTIFIER", "FROM", "IDENTIFIER" },
            tokens.Select(token => token.RuleName).ToList());
    }

    [TestMethod]
    public void SqlQueryGrammar_LexesMixedCaseWithRecursiveKeywords()
    {
        var grammar = new CompiledGrammar(SqlQueryGrammar.Build());
        var tokens = grammar.Tokenize("wItH rEcUrSiVe cte AS (sElEcT id FrOm accounts)").ToList();

        CollectionAssert.AreEqual(
            new[] { "WITH", "RECURSIVE", "IDENTIFIER", "AS", "LPAREN", "SELECT", "IDENTIFIER", "FROM", "IDENTIFIER", "RPAREN" },
            tokens.Select(token => token.RuleName).ToList());
    }
}
