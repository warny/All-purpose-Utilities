using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Tests the inspectable number-scale support contract.</summary>
[TestClass]
public class NumberScaleContractTests
{
    /// <summary>Verifies that a static-only scale reports its exact finite range.</summary>
    [TestMethod]
    public void StaticScale_ReportsFiniteRange()
    {
        var scale = new NumberScale(["", "thousand"], []);

        Assert.IsFalse(scale.IsUnbounded);
        Assert.IsTrue(scale.CanNameGroup(1));
        Assert.IsFalse(scale.CanNameGroup(2));
    }

    /// <summary>Verifies that complete dynamic prefix tables report an unbounded scale.</summary>
    [TestMethod]
    public void CompleteDynamicScale_IsUnbounded()
    {
        string[] prefixes = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        var scale = new NumberScale(
            [""], ["illion"], scale0Prefixes: prefixes,
            unitsPrefixes: prefixes, tensPrefixes: prefixes, hundredsPrefixes: prefixes);

        Assert.IsTrue(scale.IsUnbounded);
        Assert.IsTrue(scale.CanNameGroup(500));
    }

    /// <summary>Verifies that an empty static scale name is valid only for group zero.</summary>
    [TestMethod]
    public void EmptyStaticName_IsRejectedAboveGroupZero()
    {
        var scale = new NumberScale(["", ""], []);

        Assert.IsTrue(scale.CanNameGroup(0));
        Assert.IsFalse(scale.CanNameGroup(1));
    }

    /// <summary>Verifies that null and empty void-group placeholders use the default while an explicit value is preserved.</summary>
    [TestMethod]
    public void VoidGroup_NullOrEmptyUsesDefault_ExplicitValueIsPreserved()
    {
        string[] prefixes = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        var nullPlaceholder = new NumberScale([], ["suffix"], voidGroup: null,
            scale0Prefixes: prefixes, unitsPrefixes: prefixes, tensPrefixes: prefixes, hundredsPrefixes: prefixes);
        var emptyPlaceholder = new NumberScale([], ["suffix"], voidGroup: "",
            scale0Prefixes: prefixes, unitsPrefixes: prefixes, tensPrefixes: prefixes, hundredsPrefixes: prefixes);
        var explicitPlaceholder = new NumberScale([], ["suffix"], voidGroup: "zero",
            scale0Prefixes: prefixes, unitsPrefixes: prefixes, tensPrefixes: prefixes, hundredsPrefixes: prefixes);

        StringAssert.Contains(nullPlaceholder.GetScaleName(999), "ni");
        Assert.AreEqual(nullPlaceholder.GetScaleName(999), emptyPlaceholder.GetScaleName(999));
        StringAssert.Contains(explicitPlaceholder.GetScaleName(999), "zero");
    }
}
