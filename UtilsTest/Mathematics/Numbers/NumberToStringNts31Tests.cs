using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-31 (Dutch long scale): NL inherits the strict Conway-Guy-Wechsler tables of SCALE-LONG,
/// with the Dutch suffixes <c>-joen</c>/<c>-jard</c> joined by <c>l</c>; the scale nouns stay singular after an exact
/// numeral, the groups are separated by a space, and widening the cardinals leaves the ordinal domain unchanged.
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

    /// <summary>Widening the cardinals does not widen the ordinals: 999 999 stays the largest supported ordinal.</summary>
    [TestMethod]
    public void Ordinals_KeepTheirDomain()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("NL");

        Assert.AreEqual("negenhonderdnegenennegentigduizend negenhonderdnegenennegentigste", converter.ConvertOrdinal(999_999L));
        Assert.AreEqual("min negenhonderdnegenennegentigduizend negenhonderdnegenennegentigste", converter.ConvertOrdinal(-999_999L));
        foreach (long value in new[] { 1_000_000L, 1_000_001L, 2_000_000L, 1_000_000_000L, long.MaxValue })
            Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(value), $"{value}");
        // Beyond long the general ordinal contract of the engine rejects the argument, as for every language.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertOrdinal(BigInteger.Pow(10, 24)));
    }
}
