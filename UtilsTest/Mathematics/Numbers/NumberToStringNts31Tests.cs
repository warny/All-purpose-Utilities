using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-31 (Dutch long scale): NL inherits the strict Conway-Guy-Wechsler tables of SCALE-LONG,
/// with the Dutch suffixes <c>-joen</c>/<c>-jard</c> joined by <c>l</c>; the scale nouns stay singular after an exact
/// numeral and the groups are separated by a space. The ordinals cover -<see cref="long.MaxValue"/> to
/// <see cref="long.MaxValue"/> (<see cref="long.MinValue"/> is rejected by the engine): a round scale value
/// ordinalizes its scale noun (<c>miljoenste</c>, <c>twee miljoenste</c>), a compound value only its final element.
/// The linguistic forms are pinned by <c>Dutch.feature</c>.
/// </summary>
[TestClass]
public class NumberToStringNts31Tests
{
    /// <summary>Gets the Dutch scale.</summary>
    private static NumberScale DutchScale => NumberToStringConverter.GetConverter("NL").Scale;

    /// <summary>The Dutch scale is unbounded and its attested base names come from the inherited tables.</summary>
    [TestMethod]
    [DataRow(1, "duizend")]
    [DataRow(2, "miljoen")]
    [DataRow(3, "miljard")]
    [DataRow(4, "biljoen")]
    [DataRow(5, "biljard")]
    [DataRow(6, "triljoen")]
    [DataRow(7, "triljard")]
    [DataRow(8, "quadriljoen")]
    [DataRow(9, "quadriljard")]
    [DataRow(10, "quintiljoen")]
    [DataRow(12, "sextiljoen")]
    [DataRow(14, "septiljoen")]
    [DataRow(16, "octiljoen")]
    [DataRow(18, "noniljoen")]
    [DataRow(20, "deciljoen")]
    [DataRow(21, "deciljard")]
    [DataRow(40, "vigintiljoen")]
    [DataRow(41, "vigintiljard")]
    [DataRow(200, "centiljoen")]
    [DataRow(201, "centiljard")]
    public void DutchScale_BaseNames(int index, string expected)
    {
        Assert.IsTrue(DutchScale.IsUnbounded);
        Assert.AreEqual(expected, DutchScale.GetScaleName(index));
    }

    /// <summary>
    /// Representative generated names beyond the attested base: the decillion family (units before deci with the
    /// Conway linking consonant), the a → i ending before -illi-, and grouped indices from 1000 joined by the inherited
    /// -illi- (Conway n is half the long-scale index).
    /// </summary>
    [TestMethod]
    [DataRow(22, "undeciljoen")]
    [DataRow(26, "tredeciljoen")]
    [DataRow(32, "sedeciljoen")]
    [DataRow(34, "septendeciljoen")]
    [DataRow(35, "septendeciljard")]
    [DataRow(60, "trigintiljoen")]
    [DataRow(260, "trigintacentiljoen")]
    [DataRow(1998, "novenonagintanongentiljoen")]
    [DataRow(2000, "milliniljoen")]
    [DataRow(2001, "milliniljard")]
    [DataRow(2002, "millimiljoen")]
    [DataRow(60000, "trigintilliniljoen")]
    public void DutchScale_GeneratedConwayNames(int index, string expected)
        => Assert.AreEqual(expected, DutchScale.GetScaleName(index));

    /// <summary>
    /// Every Dutch scale name from the million to index 4001 is the SCALE-LONG name with its final <c>-lion</c> /
    /// <c>-liard</c> replaced by <c>-joen</c> / <c>-jard</c>: the Conway tables are inherited unchanged.
    /// </summary>
    [TestMethod]
    public void DutchScale_IsTheLongScaleWithDutchEndings()
    {
        NumberScale longScale = NumberToStringConverter.GetConverter("SCALE-LONG").Scale;
        for (int index = 2; index <= 4001; index++)
        {
            string reference = longScale.GetScaleName(index);
            string expected = index % 2 == 0
                ? reference[..^"lion".Length] + "joen"
                : reference[..^"liard".Length] + "jard";
            Assert.AreEqual(expected, DutchScale.GetScaleName(index), $"index {index} ({reference})");
        }
    }

    /// <summary>Dutch keeps the Latin Conway spellings, not the Swedish adaptations.</summary>
    [TestMethod]
    public void DutchScale_DoesNotUseSwedishSpellings()
    {
        Assert.AreEqual("quadriljoen", DutchScale.GetScaleName(8));
        Assert.AreEqual("quintiljoen", DutchScale.GetScaleName(10));
        Assert.AreEqual("sextiljoen", DutchScale.GetScaleName(12));
        Assert.AreEqual("octiljoen", DutchScale.GetScaleName(16));
        for (int index = 2; index <= 4001; index++)
        {
            string name = DutchScale.GetScaleName(index);
            foreach (string forbidden in new[] { "kvadri", "kvinti", "sekst", "okti", "jon(", "jonen", "jarden" })
                Assert.IsFalse(name.Contains(forbidden), $"index {index}: {name}");
        }
    }

    /// <summary><c>duizend</c> drops <c>een</c>; miljoen and the higher scale nouns keep it.</summary>
    [TestMethod]
    [DataRow("1000", "duizend")]
    [DataRow("1000000", "een miljoen")]
    [DataRow("1000000000", "een miljard")]
    [DataRow("1000000000000", "een biljoen")]
    [DataRow("1000000000000000000000000", "een quadriljoen")]
    public void Een_IsDroppedOnlyBeforeDuizend(string number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("NL").Convert(BigInteger.Parse(number)));

    /// <summary>
    /// Exact cardinals never pluralize the scale noun, never contain a comma (the inherited SCALE-LONG
    /// <c>groupSeparator</c> is overridden) and never insert <c>en</c> between groups.
    /// </summary>
    [TestMethod]
    public void ExactCardinals_HaveSingularScaleNounsAndSpaces()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");
        foreach (long value in new[] { 2_000_000L, 4_000_000_000L, 21_000_000L, 999_999_999_999L, 3_400_000L, 1_001_001L, 2_002_002_002_002L })
        {
            string text = converter.Convert(value);
            foreach (string forbidden in new[] { "miljoenen", "miljarden", "biljoenen", ",", " en " })
                Assert.IsFalse(text.Contains(forbidden), $"{value}: {text}");
        }
        Assert.AreEqual("twee miljoen", converter.Convert(2_000_000L));
        Assert.AreEqual("vier miljard", converter.Convert(4_000_000_000L));
        Assert.AreEqual("een miljoen duizend een", converter.Convert(1_001_001L));
        Assert.AreEqual(
            "negenhonderdnegenennegentig miljard negenhonderdnegenennegentig miljoen negenhonderdnegenennegentigduizend negenhonderdnegenennegentig",
            converter.Convert(999_999_999_999L));
    }

    /// <summary>The cardinals are unbounded: the largest <see cref="long"/> and a value with a grouped index convert.</summary>
    [TestMethod]
    public void Cardinals_AreUnbounded()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        StringAssert.StartsWith(converter.Convert(long.MaxValue), "negen triljoen ");
        StringAssert.EndsWith(converter.Convert(BigInteger.Pow(10, 6000)), "milliniljoen");
        Assert.AreEqual("een milliniljoen", converter.Convert(BigInteger.Pow(10, 6000)));
    }

    /// <summary>
    /// Round scale ordinals (e-ANS: <c>miljoenste</c>, <c>miljardste</c>): the scale noun takes <c>-ste</c> on every scale
    /// reachable by <see cref="long"/>, from <c>miljoen</c> (index 2) to <c>triljoen</c> (index 6).
    /// </summary>
    [TestMethod]
    [DataRow(1_000_000L, "miljoenste")]
    [DataRow(2_000_000L, "twee miljoenste")]
    [DataRow(100_000_000L, "honderd miljoenste")]
    [DataRow(1_000_000_000L, "miljardste")]
    [DataRow(2_000_000_000L, "twee miljardste")]
    [DataRow(1_000_000_000_000L, "biljoenste")]
    [DataRow(1_000_000_000_000_000L, "biljardste")]
    [DataRow(1_000_000_000_000_000_000L, "triljoenste")]
    [DataRow(9_000_000_000_000_000_000L, "negen triljoenste")]
    public void Ordinals_RoundScaleValues(long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("NL").ConvertOrdinal(number));

    /// <summary>A round value with the multiplier one is the scale noun alone: <c>een</c> disappears.</summary>
    [TestMethod]
    [DataRow(1_000_000L, "een miljoenste")]
    [DataRow(1_000_000_000L, "een miljardste")]
    [DataRow(1_000_000_000_000L, "een biljoenste")]
    public void Ordinals_MultiplierOne_DropsEen(long number, string rejected)
    {
        string ordinal = NumberToStringConverter.GetConverter("NL").ConvertOrdinal(number);
        Assert.AreNotEqual(rejected, ordinal);
        Assert.IsFalse(ordinal.StartsWith("een ", StringComparison.Ordinal), ordinal);
    }

    /// <summary>
    /// A multiplier above one stays its own cardinal, separated from the ordinal scale noun by a space (Besluit
    /// bekendmaking spellingvoorschriften 2005, rule 6.N: <c>de twee miljoenste</c>), never soldered.
    /// </summary>
    [TestMethod]
    public void Ordinals_LargerMultipliers_StaySpaced()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        Assert.AreEqual("twee miljoenste", converter.ConvertOrdinal(2_000_000L));
        Assert.AreNotEqual("tweemiljoenste", converter.ConvertOrdinal(2_000_000L));
        Assert.AreEqual("eenentwintig miljoenste", converter.ConvertOrdinal(21_000_000L));
        Assert.AreEqual("negenhonderdnegenennegentig miljardste", converter.ConvertOrdinal(999_000_000_000L));
    }

    /// <summary>
    /// Compound values keep every earlier group cardinal and ordinalize only the final element (Besluit bekendmaking
    /// spellingvoorschriften 2005, rule 6.N), without <c>en</c> between the groups.
    /// </summary>
    [TestMethod]
    [DataRow(1_001_000L, "een miljoen duizendste")]
    [DataRow(1_000_001L, "een miljoen eerste")]
    [DataRow(2_200_000L, "twee miljoen tweehonderdduizendste")]
    [DataRow(2_200_022L, "twee miljoen tweehonderdduizend tweeëntwintigste")]
    [DataRow(2_200_222L, "twee miljoen tweehonderdduizend tweehonderdtweeëntwintigste")]
    [DataRow(2_001_000_000L, "twee miljard een miljoenste")]
    public void Ordinals_CompoundValues(long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("NL").ConvertOrdinal(number));

    /// <summary>
    /// Over a broad sample of the <see cref="long"/> range from a million, every word but the last is the cardinal
    /// unchanged; only a round value with the multiplier one also drops its leading <c>een</c>.
    /// </summary>
    [TestMethod]
    public void Ordinals_OnlyTheFinalElementIsOrdinal()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");
        var samples = new List<long> { long.MaxValue };
        for (long value = 1_000_000; value < long.MaxValue / 7; value = value * 7 + 13)
            samples.Add(value);
        long power = 1_000_000;
        for (int exponent = 6; exponent <= 18; exponent++, power *= 10)
            samples.AddRange([power, power + 1, power * 3 + 1000, power * 7 + 222]);

        foreach (long value in samples)
        {
            string cardinal = converter.Convert(value);
            string ordinal = converter.ConvertOrdinal(value);
            string[] cardinalWords = cardinal.Split(' ');
            string[] ordinalWords = ordinal.Split(' ');
            bool droppedEen = cardinalWords.Length == 2 && ordinalWords.Length == 1 && cardinalWords[0] == "een";
            string[] cardinalHead = droppedEen ? [] : cardinalWords[..^1];
            Assert.AreEqual(cardinalWords.Length - (droppedEen ? 1 : 0), ordinalWords.Length, $"{value}: {cardinal} / {ordinal}");
            CollectionAssert.AreEqual(cardinalHead, ordinalWords[..^1], $"{value}: {cardinal} / {ordinal}");
            Assert.IsTrue(ordinal.EndsWith("ste", StringComparison.Ordinal) || ordinal.EndsWith("de", StringComparison.Ordinal), $"{value}: {ordinal}");
        }
    }

    /// <summary>The largest <see cref="long"/> converts: the cardinal groups stay, the final <c>zeven</c> becomes <c>zevende</c>.</summary>
    [TestMethod]
    public void Ordinals_ReachLongMaxValue()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        Assert.AreEqual(
            "negen triljoen tweehonderddrieëntwintig biljard driehonderdtweeënzeventig biljoen zesendertig miljard achthonderdvierenvijftig miljoen zevenhonderdvijfenzeventigduizend achthonderdzevende",
            converter.ConvertOrdinal(long.MaxValue));
        Assert.AreEqual(converter.Convert(long.MaxValue) + "de", converter.ConvertOrdinal(long.MaxValue));
    }

    /// <summary>Negative ordinals from a million take the generic <c>min</c> prefix of the Dutch configuration.</summary>
    [TestMethod]
    public void Ordinals_Negative()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        Assert.AreEqual("min miljoenste", converter.ConvertOrdinal(-1_000_000L));
        Assert.AreEqual("min twee miljoen tweehonderdduizend tweehonderdtweeëntwintigste", converter.ConvertOrdinal(-2_200_222L));
        Assert.AreEqual("min negenhonderdnegenennegentigduizend negenhonderdnegenennegentigste", converter.ConvertOrdinal(-999_999L));
    }

    /// <summary>The ordinals below a million are unchanged by the scale ordinals.</summary>
    [TestMethod]
    [DataRow(0L, "nulde")]
    [DataRow(1L, "eerste")]
    [DataRow(2L, "tweede")]
    [DataRow(8L, "achtste")]
    [DataRow(11L, "elfde")]
    [DataRow(20L, "twintigste")]
    [DataRow(21L, "eenentwintigste")]
    [DataRow(101L, "honderdeerste")]
    [DataRow(1000L, "duizendste")]
    [DataRow(2000L, "tweeduizendste")]
    [DataRow(999_999L, "negenhonderdnegenennegentigduizend negenhonderdnegenennegentigste")]
    public void Ordinals_BelowAMillion_AreUnchanged(long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("NL").ConvertOrdinal(number));

    /// <summary>
    /// Beyond <see cref="long"/>, and at <see cref="long.MinValue"/> (whose absolute value is not representable), the
    /// general ordinal contract of the engine rejects the argument, as for every language; <c>-long.MaxValue</c> is the
    /// lowest supported ordinal.
    /// </summary>
    [TestMethod]
    public void Ordinals_BeyondLong_AreRejectedByTheEngine()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertOrdinal(BigInteger.Pow(10, 24)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertOrdinal(long.MinValue));
        Assert.AreEqual("min " + converter.ConvertOrdinal(long.MaxValue), converter.ConvertOrdinal(-long.MaxValue));
    }
}
