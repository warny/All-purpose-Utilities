using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data.Sql;

namespace UtilsTests.Data;

/// <summary>
/// Tests for typed SQL statement parts exposed by parsed statements.
/// </summary>
[TestClass]
public sealed class SqlStatementPartTests
{
    [TestMethod]
    public void SelectStatementExposesSelectPart()
    {
        SqlQuery query = SqlQueryAnalyzer.Parse("SELECT id, name");
        var statement = (SqlSelectStatement)query.RootStatement;

        Assert.AreSame(statement.Select, statement.SelectPart.Segment);
        Assert.IsNull(statement.FromPart);
        Assert.IsNull(statement.WherePart);
    }

    [TestMethod]
    public void UpdateStatementCreatesTypedPartsWhenEnsured()
    {
        SqlQuery query = SqlQueryAnalyzer.Parse("UPDATE accounts SET name = 'x'");
        var statement = (SqlUpdateStatement)query.RootStatement;

        Assert.AreSame(statement.Target, statement.UpdatePart.Segment);
        Assert.IsNull(statement.FromPart);

        SqlSegment fromSegment = statement.EnsureFromSegment();

        Assert.IsNotNull(statement.FromPart);
        Assert.AreSame(fromSegment, statement.FromPart!.Segment);
    }

    [TestMethod]
    public void DeleteStatementCreatesTypedParts()
    {
        SqlQuery query = SqlQueryAnalyzer.Parse("DELETE FROM accounts");
        var statement = (SqlDeleteStatement)query.RootStatement;

        Assert.IsNull(statement.DeletePart);
        Assert.AreSame(statement.From, statement.FromPart.Segment);

        SqlSegment targetSegment = statement.EnsureTargetSegment();

        Assert.IsNotNull(statement.DeletePart);
        Assert.AreSame(targetSegment, statement.DeletePart!.Segment);
    }
}
