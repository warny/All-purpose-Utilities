using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

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

    /// <summary>
    /// Verifies the shared SCALE-SHORT Conway-Wechsler tables against a reference table of short-scale names
    /// (index = Conway n + 1). The expected values are literal, not recomputed by the test.
    /// </summary>
    [TestMethod]
    public void ShortScale_ConwayWechsler_MatchesReferenceNames()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;
        (int Index, string Name)[] reference =
        [
            (1, "thousand"), (2, "million"), (3, "billion"), (4, "trillion"), (5, "quadrillion"),
            (6, "quintillion"), (7, "sextillion"), (8, "septillion"), (9, "octillion"), (10, "nonillion"),
            (11, "decillion"), (13, "duodecillion"), (14, "tredecillion"), (15, "quattuordecillion"),
            (18, "septendecillion"), (19, "octodecillion"), (101, "centillion"),
        ];

        Assert.IsTrue(scale.IsUnbounded);
        foreach (var (index, name) in reference)
            Assert.AreEqual(name, scale.GetScaleName(index), $"scale {index}");
    }

    /// <summary>
    /// Pins the SCALE-SHORT names that differ from the usual dictionary short-scale names (NTS-24): 10^36
    /// "undecillion", 10^48 "quindecillion", 10^51 "sexdecillion", 10^60 "novemdecillion" and 10^63
    /// "vigintillion". The tables are shared by EN, ID, SW, TR and EE, so a correction is a separate,
    /// multi-language decision; update this test together with it.
    /// </summary>
    [TestMethod]
    public void ShortScale_ConwayWechsler_KnownDivergencesFromDictionaryNames()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;

        Assert.AreEqual("unidecillion", scale.GetScaleName(12));
        Assert.AreEqual("quinquadecillion", scale.GetScaleName(16));
        Assert.AreEqual("sedecillion", scale.GetScaleName(17));
        Assert.AreEqual("novendecillion", scale.GetScaleName(20));
        Assert.AreEqual("vingtillion", scale.GetScaleName(21));
    }
}
