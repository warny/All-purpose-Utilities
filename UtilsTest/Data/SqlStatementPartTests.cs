using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for typed SQL statement parts exposed by parsed statements.
/// </summary>
[TestClass]
public sealed class SqlStatementPartTests
{
    /// <summary>
    /// Ensures SELECT statements expose typed parts aligned with their segments.
    /// </summary>
    [TestMethod]
    public void SelectStatementExposesTypedParts()
    {
        var parser = SqlParser.Create("SELECT id, name FROM accounts WHERE active = 1");

        var statement = (SqlSelectStatement)parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();

        Assert.AreSame(statement.Select, statement.SelectPart.Segment);
        Assert.IsNotNull(statement.FromPart);
        Assert.AreSame(statement.From, statement.FromPart!.Segment);
        Assert.IsNotNull(statement.WherePart);
        Assert.AreEqual(statement.Where?.ToSql(), statement.WherePart!.ToSql());
    }

    /// <summary>
    /// Ensures INSERT statements expose typed parts for the target and values clauses.
    /// </summary>
    [TestMethod]
    public void InsertStatementExposesTypedParts()
    {
        var parser = SqlParser.Create("INSERT INTO accounts(id) VALUES (1)");

        var statement = (SqlInsertStatement)parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();

        Assert.AreSame(statement.Target, statement.InsertPart.Segment);
        Assert.AreSame(statement.Target, statement.IntoPart.Segment);
        Assert.IsNotNull(statement.ValuesPart);
        Assert.AreSame(statement.Values, statement.ValuesPart!.Segment);
    }

    /// <summary>
    /// Ensures UPDATE statements create typed parts when optional clauses are added.
    /// </summary>
    [TestMethod]
    public void UpdateStatementCreatesTypedPartsWhenEnsured()
    {
        var parser = SqlParser.Create("UPDATE accounts SET name = 'x'");

        var statement = (SqlUpdateStatement)parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();

        Assert.AreSame(statement.Target, statement.UpdatePart.Segment);
        Assert.IsNull(statement.FromPart);

        var fromSegment = statement.EnsureFromSegment();

        Assert.IsNotNull(statement.FromPart);
        Assert.AreSame(fromSegment, statement.FromPart!.Segment);
    }

    /// <summary>
    /// Ensures DELETE statements expose typed parts and create them when optional sections are added.
    /// </summary>
    [TestMethod]
    public void DeleteStatementCreatesTypedParts()
    {
        var parser = SqlParser.Create("DELETE FROM accounts");

        var statement = (SqlDeleteStatement)parser.ParseStatementWithOptionalCte();
        parser.ConsumeOptionalTerminator();
        parser.EnsureEndOfInput();

        Assert.IsNull(statement.DeletePart);
        Assert.IsNotNull(statement.FromPart);
        Assert.AreSame(statement.From, statement.FromPart.Segment);

        var targetSegment = statement.EnsureTargetSegment();

        Assert.IsNotNull(statement.DeletePart);
        Assert.AreSame(targetSegment, statement.DeletePart!.Segment);
    }
}
