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

    /// <summary>Strict Conway-Guy-Wechsler names keyed by Conway index n (the n-th -illion is 10^(3n+3) in the short scale).</summary>
    /// <remarks>
    /// Literal reference values, never recomputed by the test. quinquadecillion, sedecillion and novendecillion are the
    /// systematic Conway forms, intentionally preferred to the dictionary quindecillion, sexdecillion and novemdecillion.
    /// The linking-consonant cases follow Conway's markers: tre takes an s before an s- or x-marked component (tres),
    /// se takes s or x (ses, sex), septe and nove take m or n; ducenti is marked n only (treducenti, seducenti).
    /// </remarks>
    private static readonly (long N, string Name)[] ConwayReference =
    [
        (1, "million"), (2, "billion"), (3, "trillion"), (4, "quadrillion"), (5, "quintillion"),
        (6, "sextillion"), (7, "septillion"), (8, "octillion"), (9, "nonillion"), (10, "decillion"),
        (11, "undecillion"), (12, "duodecillion"), (13, "tredecillion"), (14, "quattuordecillion"),
        (15, "quinquadecillion"), (16, "sedecillion"), (17, "septendecillion"), (18, "octodecillion"),
        (19, "novendecillion"), (20, "vigintillion"),
        (21, "unvigintillion"), (23, "tresvigintillion"), (26, "sesvigintillion"), (27, "septemvigintillion"),
        (29, "novemvigintillion"),
        (30, "trigintillion"), (33, "trestrigintillion"), (36, "sestrigintillion"), (37, "septentrigintillion"),
        (40, "quadragintillion"), (50, "quinquagintillion"), (60, "sexagintillion"), (63, "tresexagintillion"),
        (66, "sesexagintillion"), (70, "septuagintillion"), (80, "octogintillion"), (83, "tresoctogintillion"),
        (86, "sexoctogintillion"), (87, "septemoctogintillion"), (90, "nonagintillion"), (99, "novenonagintillion"),
        (100, "centillion"), (101, "uncentillion"), (103, "trescentillion"), (106, "sexcentillion"),
        (107, "septencentillion"), (109, "novencentillion"), (110, "decicentillion"), (111, "undecicentillion"),
        (130, "trigintacentillion"), (186, "sexoctogintacentillion"), (199, "novenonagintacentillion"),
        (200, "ducentillion"), (203, "treducentillion"), (206, "seducentillion"), (207, "septenducentillion"),
        (209, "novenducentillion"), (306, "sestrecentillion"), (803, "tresoctingentillion"),
        (806, "sexoctingentillion"), (807, "septemoctingentillion"), (809, "novemoctingentillion"),
        (999, "novenonagintanongentillion"),
        // Grouped indices: every three-digit group is a new Conway index joined by -illi-, and 000 is "ni".
        (1000, "millinillion"), (1001, "millimillion"), (1009, "millinonillion"), (1010, "millidecillion"),
        (1030, "millitrigintillion"), (1100, "millicentillion"), (1999, "millinovenonagintanongentillion"),
        (2000, "billinillion"), (6560, "sextillisexagintaquingentillion"), (30000, "trigintillinillion"),
        (1000003, "millinillitrillion"),
    ];

    /// <summary>
    /// Verifies the shared SCALE-SHORT tables against strict Conway-Guy-Wechsler names. In SCALE-SHORT the scale
    /// index is Conway n + 1 (index 1 is thousand, index 2 is million = n 1).
    /// </summary>
    [TestMethod]
    public void ShortScale_ConwayWechsler_MatchesReferenceNames()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;

        Assert.IsTrue(scale.IsUnbounded);
        Assert.AreEqual("", scale.GetScaleName(0));
        Assert.AreEqual("thousand", scale.GetScaleName(1));
        foreach (var (n, name) in ConwayReference)
            Assert.AreEqual(name, scale.GetScaleName(checked((int)n + 1)), $"Conway n {n}");
    }

    /// <summary>
    /// Verifies SCALE-LONG: each Conway index n is used twice, scale index 2n with the "on" suffix and 2n + 1 with
    /// the "ard" suffix (index 2 is million, index 3 milliard).
    /// </summary>
    [TestMethod]
    public void LongScale_ConwayWechsler_MatchesReferenceNames()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-LONG").Scale;

        Assert.IsTrue(scale.IsUnbounded);
        foreach (var (n, name) in ConwayReference)
        {
            string stem = name[..^"on".Length];
            Assert.AreEqual(stem + "on", scale.GetScaleName(checked((int)(2 * n))), $"Conway n {n}, on");
            Assert.AreEqual(stem + "ard", scale.GetScaleName(checked((int)(2 * n + 1))), $"Conway n {n}, ard");
        }
    }

    /// <summary>
    /// Verifies the published multi-group Conway-Wechsler example 10^33,002,010,111 (n = 11,000,670,036), which mixes
    /// a compound triplet, a zero triplet, a hundreds triplet and a terminal tens triplet. The index exceeds the
    /// <see cref="int"/> range of <see cref="NumberScale.GetScaleName"/>, so the internal builder is used directly.
    /// </summary>
    [TestMethod]
    public void ShortScale_ConwayWechsler_PublishedMultiGroupExample()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;

        Assert.AreEqual(
            "undecillinilliseptuagintasescentillisestrigintillion",
            scale.BuildDynamicName(11_000_670_036, "on"));
    }

    /// <summary>Verifies that scale indices far above the 999th -illion stay nameable.</summary>
    [TestMethod]
    public void ShortScale_LargeIndices_AreNameable()
    {
        var scale = NumberToStringConverter.GetConverter("SCALE-SHORT").Scale;

        foreach (int index in new[] { 1001, 1002, 2001, 1_000_004, int.MaxValue })
            Assert.IsTrue(scale.CanNameGroup(index), $"scale {index}");
    }

    /// <summary>Synthetic tables: every 1..9 group, including 001 in a higher group, uses Scale0Prefixes, not UnitsPrefixes.</summary>
    [TestMethod]
    public void GroupedIndex_SingleDigitGroups_UseScale0Prefixes()
    {
        var scale = new NumberScale(
            [], ["Z"], voidGroup: "V", groupSeparator: "-",
            scale0Prefixes: ["", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9"],
            unitsPrefixes: ["", "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8", "u9"],
            tensPrefixes: ["", "t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8", "t9"],
            hundredsPrefixes: ["", "h1", "h2", "h3", "h4", "h5", "h6", "h7", "h8", "h9"]);

        // Without static names and with StartIndex 0, scale index = prefix value - 1.
        Assert.AreEqual("s1-Z", scale.GetScaleName(0));
        Assert.AreEqual("u2t1-Z", scale.GetScaleName(11));
        Assert.AreEqual("s1-V-Z", scale.GetScaleName(999));
        Assert.AreEqual("s1-s1-Z", scale.GetScaleName(1000));
        Assert.AreEqual("s2-u1t1h1-Z", scale.GetScaleName(2110));
        Assert.AreEqual("s1-V-s3-Z", scale.GetScaleName(1_000_002));
    }

    /// <summary>Synthetic tables: the [default|-illi=>form] ending is chosen by the position of the component in its group.</summary>
    [TestMethod]
    public void PrefixEnding_IlliContext_AppliesOnlyToTheLastComponentOfAGroup()
    {
        var scale = new NumberScale(
            [], ["on"], groupSeparator: "lli",
            scale0Prefixes: ["", "mi", "bi", "tri", "quadri", "quinti", "sexti", "septi", "octi", "noni"],
            unitsPrefixes: ["", "un", "", "", "", "", "", "", "", ""],
            tensPrefixes: ["", "", "", "trigint[a|-illi=>i]", "", "", "", "", "", ""],
            hundredsPrefixes: ["", "centi", "", "", "", "", "", "", "", ""]);

        Assert.AreEqual("trigintillion", scale.BuildDynamicName(30, "on"));
        Assert.AreEqual("untrigintillion", scale.BuildDynamicName(31, "on"));
        Assert.AreEqual("trigintacentillion", scale.BuildDynamicName(130, "on"));
        Assert.AreEqual("trigintillinillion", scale.BuildDynamicName(30_000, "on"));
    }

    /// <summary>An ending context other than -illi, or a malformed ending, is rejected when the scale is built.</summary>
    [TestMethod]
    public void PrefixEnding_UnsupportedContext_IsRejected()
    {
        string[] digits = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        string[] unknownContext = ["", "x[a|-on=>i]", "", "", "", "", "", "", "", ""];
        string[] missingArrow = ["", "x[a|i]", "", "", "", "", "", "", "", ""];

        Assert.ThrowsExactly<ArgumentException>(() => new NumberScale(
            [], ["on"], scale0Prefixes: digits, unitsPrefixes: digits, tensPrefixes: unknownContext, hundredsPrefixes: digits));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberScale(
            [], ["on"], scale0Prefixes: digits, unitsPrefixes: digits, tensPrefixes: missingArrow, hundredsPrefixes: digits));
    }

    /// <summary>Synthetic tables: a single linking consonant is inserted, chosen in the order of the end markers.</summary>
    [TestMethod]
    public void LinkingConsonant_AtMostOneIsInserted_InEndMarkerOrder()
    {
        var scale = new NumberScale(
            [], ["on"], groupSeparator: "lli",
            scale0Prefixes: ["", "mi", "bi", "tri", "quadri", "quinti", "sexti", "septi", "octi", "noni"],
            unitsPrefixes: ["", "", "", "", "", "", "se(xs)", "", "", ""],
            tensPrefixes: ["", "", "(ms)viginti", "", "", "", "", "", "(mxs)octoginti", ""],
            hundredsPrefixes: ["", "", "", "", "", "", "", "", "", ""]);

        Assert.AreEqual("sesvigintillion", scale.BuildDynamicName(26, "on"));
        Assert.AreEqual("sexoctogintillion", scale.BuildDynamicName(86, "on"));
    }

    /// <summary>
    /// Every 1..9 Scale0Prefixes entry is reachable from a higher -illi- group, so an empty entry makes the scale bounded
    /// and the affected groups unnameable instead of producing a degenerate name.
    /// </summary>
    [TestMethod]
    public void EmptyScale0Prefix_IsNotNameable()
    {
        string[] digits = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        string[] scale0 = ["", "", "b", "c", "d", "e", "f", "g", "h", "i"];
        var scale = new NumberScale(
            [], ["on"], scale0Prefixes: scale0, unitsPrefixes: digits, tensPrefixes: digits, hundredsPrefixes: digits);

        Assert.IsFalse(scale.IsUnbounded);
        Assert.IsFalse(scale.CanNameGroup(0));
        Assert.IsTrue(scale.CanNameGroup(1));
        Assert.IsFalse(scale.CanNameGroup(999));
    }

    /// <summary>
    /// Every 1..9 entry of the units, tens and hundreds tables is needed by some 10..999 group, so an empty one makes
    /// the scale bounded and the groups that need it unnameable, instead of silently dropping the component.
    /// </summary>
    [TestMethod]
    public void EmptyCompositionPrefix_IsNotNameable()
    {
        string[] full = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        string[] missingOne = ["", "", "b", "c", "d", "e", "f", "g", "h", "i"];

        // Index = prefix value - 1 (no static names): 011 needs units[1] and tens[1], 100 needs hundreds[1].
        var noUnit = new NumberScale([], ["on"], scale0Prefixes: full, unitsPrefixes: missingOne, tensPrefixes: full, hundredsPrefixes: full);
        Assert.IsFalse(noUnit.IsUnbounded);
        Assert.IsFalse(noUnit.CanNameGroup(10));
        Assert.IsTrue(noUnit.CanNameGroup(11));

        var noTen = new NumberScale([], ["on"], scale0Prefixes: full, unitsPrefixes: full, tensPrefixes: missingOne, hundredsPrefixes: full);
        Assert.IsFalse(noTen.IsUnbounded);
        Assert.IsFalse(noTen.CanNameGroup(9));
        Assert.IsFalse(noTen.CanNameGroup(10));
        Assert.IsTrue(noTen.CanNameGroup(20));

        var noHundred = new NumberScale([], ["on"], scale0Prefixes: full, unitsPrefixes: full, tensPrefixes: full, hundredsPrefixes: missingOne);
        Assert.IsFalse(noHundred.IsUnbounded);
        Assert.IsFalse(noHundred.CanNameGroup(99));
        Assert.IsFalse(noHundred.CanNameGroup(1_000_100));
        Assert.IsTrue(noHundred.CanNameGroup(199));
    }

    /// <summary>A composition entry must match the prefix grammar completely; text before or after it is rejected.</summary>
    [TestMethod]
    public void PrefixEntry_TrailingOrLeadingText_IsRejected()
    {
        string[] digits = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        foreach (string entry in new[] { "trigint[a|-illi=>i]garbage", "trigint!", "-trigint", "x(ns)trigint", "trigint a" })
        {
            string[] tens = ["", "deci", "", entry, "", "", "", "", "", ""];
            Assert.ThrowsExactly<ArgumentException>(() => new NumberScale(
                [], ["on"], scale0Prefixes: digits, unitsPrefixes: digits, tensPrefixes: tens, hundredsPrefixes: digits),
                entry);
        }
    }

    /// <summary>FirstLetterUppercase capitalizes the complete name once, not each -illi- group.</summary>
    [TestMethod]
    public void FirstLetterUppercase_CapitalizesOnlyTheWholeName()
    {
        var scale = NumberToStringConverter.GetConverter("DE").Scale;

        Assert.AreEqual("Million(en)", scale.GetScaleName(2));
        Assert.AreEqual("Undezillion(en)", scale.GetScaleName(22));
        Assert.AreEqual("Vigintilliarde(n)", scale.GetScaleName(41));
        Assert.AreEqual("Trigintillion(en)", scale.GetScaleName(60));
        Assert.AreEqual("Millinillion(en)", scale.GetScaleName(2000));
    }
}
