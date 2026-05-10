using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserSharedPrefixPlanValidatorTests
{
    [TestMethod]
    public void Validate_ReturnsValidWithoutIssues_ForNormalSharedPrefixPlan()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var result = validator.Validate(plan);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Issues.Count);
    }

    [TestMethod]
    public void Validate_ReturnsInvalid_WhenContinuationsAreMissing()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", 1, []);

        var result = validator.Validate(plan);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Message.Contains("no continuations", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_ReturnsValidWithInfoAndWarning_WhenBoundaryDiverges()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var result = validator.Validate(plan);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Severity == ParserSharedPrefixPlanValidationSeverity.Info));
        Assert.IsTrue(result.Issues.Any(static issue => issue.Message.Contains("non-fallback", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_ReturnsWarning_WhenAlternativeIndexesAreDuplicated()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(0, 1)]);

        var result = validator.Validate(plan);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Message.Contains("duplicated", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_ReturnsInvalid_WhenNegativePositionsArePresent()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", -1, [Continuation(0, -1), Continuation(1, 2)]);

        var result = validator.Validate(plan);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Count >= 2);
    }

    [TestMethod]
    public void Validate_ReturnsInvalid_WhenSharedTokenNameIsEmpty()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("   ", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var result = validator.Validate(plan);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(static issue => issue.Message.Contains("token name is empty", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_IsDeterministicAcrossInvocations()
    {
        var validator = new ParserSharedPrefixPlanValidator();
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var first = validator.Validate(plan);
        var second = validator.Validate(plan);

        Assert.AreEqual(first.IsValid, second.IsValid);
        CollectionAssert.AreEqual(first.Issues.ToArray(), second.Issues.ToArray());
    }

    private static ParserSharedPrefixPlan Plan(
        string tokenName,
        int boundaryPosition,
        IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var alternativeIndexes = continuations.Select(static c => c.Key.AlternativeIndex).ToArray();
        return new ParserSharedPrefixPlan(
            tokenName,
            alternativeIndexes,
            continuations,
            new ParserSharedPrefixSegment(tokenName, new ParserSharedPrefixBoundary(boundaryPosition, null)));
    }

    private static ParserContinuationDescriptor Continuation(int alternativeIndex, int sequencePosition)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, sequencePosition),
            ["ID"],
            true);
    }
}
