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
    /// Verifies the shared SCALE-SHORT tables against a reference table of strict Conway-Guy-Wechsler names
    /// (index = Conway n + 1). quinquadecillion, sedecillion and novendecillion are the systematic Conway
    /// forms, intentionally preferred to the dictionary quindecillion, sexdecillion and novemdecillion.
    /// The expected values are literal, not recomputed by the test.
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
            (16, "quinquadecillion"), (17, "sedecillion"), (18, "septendecillion"), (19, "octodecillion"),
            (20, "novendecillion"), (101, "centillion"),
        ];

        Assert.IsTrue(scale.IsUnbounded);
        foreach (var (index, name) in reference)
            Assert.AreEqual(name, scale.GetScaleName(index), $"scale {index}");
    }

    /// <summary>
    /// Pins the two known SCALE-SHORT table errors against strict Conway-Wechsler (NTS-24): the units prefix
    /// "uni" instead of "un" (10^36 "unidecillion", strict "undecillion") and the tens prefix "vingti" instead
    /// of "viginti" (10^63 "vingtillion", strict "vigintillion"). The tables are shared by EN, ID, SW, TR and
    /// EE, so the correction is a separate, deliberate multi-language change; replace these assertions with
    /// the strict forms when it lands.
    /// </summary>
    [TestMethod]
    public void ShortScale_ConwayWechsler_KnownTableErrors()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;

        Assert.AreEqual("unidecillion", scale.GetScaleName(12));
        Assert.AreEqual("vingtillion", scale.GetScaleName(21));
    }
}
