using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-22 guards for the Ewe millions: the border with the billions (opened by NTS-23), the corpus
/// attestations are reproduced exactly, and combinatorial sweeps (every multiplier 1–999 with
/// representative lower parts, never the whole domain) prove that miliɔn takes the standalone cardinal
/// after the noun and that ordinals suffix the last element only. Structural checks, not linguistic ones.
/// </summary>
[TestClass]
public class NumberToStringNts22Tests
{
    /// <summary>One million.</summary>
    private const long Million = 1_000_000;

    /// <summary>Representative lower parts below a million (units, teens, tens, hundreds, thousands).</summary>
    private static readonly long[] LowerParts =
        [0, 1, 2, 9, 10, 11, 19, 20, 21, 99, 100, 101, 110, 122, 999, 1000, 1001, 1100, 21000, 100000, 100001, 999999];

    /// <summary>Gets the Ewe converter.</summary>
    private static NumberToStringConverter Ewe => NumberToStringConverter.GetConverter("EE");

    /// <summary>Converts a cardinal.</summary>
    private static string C(long value) => Ewe.Convert((BigInteger)value);

    /// <summary>
    /// The millions border the billions: 999 999 999 is the last value spelled with miliɔn as its highest scale,
    /// and since NTS-23 the next value opens biliɔn instead of being rejected (see <see cref="NumberToStringNts23Tests"/>).
    /// </summary>
    [TestMethod]
    public void Ewe_MillionsBorderTheBillions()
    {
        var converter = Ewe;
        Assert.IsTrue(converter.Scale.CanNameGroup(2));
        Assert.AreEqual("miliɔn", converter.Scale.GetScaleName(2));

        Assert.AreEqual("miliɔn alafa asieke blaasieke vɔ asieke akpe alafa asieke blaasieke vɔ asieke alafa asieke blaasieke vɔ asieke", C(999_999_999));
        Assert.AreEqual("biliɔn ɖeka", C(1_000_000_000));
        Assert.AreEqual("minus biliɔn ɖeka", C(-1_000_000_000));
        Assert.AreEqual("miliɔn alafa asieke blaasieke vɔ asieke akpe alafa asieke blaasieke vɔ asieke alafa asieke blaasieke vɔ asiekelia", converter.ConvertOrdinal(999_999_999));
        Assert.AreEqual("biliɔn ɖekalia", converter.ConvertOrdinal(1_000_000_000));
    }

    /// <summary>The Biblica values written out with their digits are reproduced exactly.</summary>
    [TestMethod]
    public void Ewe_ReproducesTheCorpusMillions()
    {
        // 1CH 21:5 and REV 9:16 (Biblica Open Ewe Contemporary Scriptures).
        Assert.AreEqual("miliɔn ɖeka akpe alafa ɖeka", C(1_100_000));
        Assert.AreEqual("miliɔn alafa eve", C(200_000_000));
        Assert.IsFalse(C(1_000_000).Contains("akpe akpe", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every multiplier 1–999 of miliɔn, with representative lower parts: miliɔn + the standalone cardinal
    /// of the multiplier, then "kple" before a lower top group below 100 or a plain space otherwise.
    /// </summary>
    [TestMethod]
    public void Ewe_MillionSweep_ComposesTheStandaloneMultiplier()
    {
        var failures = new List<string>();
        for (long multiplier = 1; multiplier <= 999; multiplier++)
        {
            string head = "miliɔn " + C(multiplier);
            foreach (long lower in LowerParts)
            {
                long number = multiplier * Million + lower;
                long lowerTopGroup = lower >= 1000 ? lower / 1000 : lower;
                string expected = lower == 0 ? head : head + (lowerTopGroup < 100 ? " kple " : " ") + C(lower);
                string actual = C(number);
                if (actual != expected) failures.Add($"{number}: expected '{expected}', got '{actual}'");
            }
            if (failures.Count > 20) break;
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every multiplier 1–999 of miliɔn, round and with representative lower parts: the ordinal is the
    /// cardinal with "lia" on its last element, never on the scale noun.
    /// </summary>
    [TestMethod]
    public void Ewe_MillionOrdinalSweep_SuffixesTheLastElement()
    {
        var converter = Ewe;
        var failures = new List<string>();
        for (long multiplier = 1; multiplier <= 999; multiplier++)
        {
            foreach (long lower in LowerParts)
            {
                long number = multiplier * Million + lower;
                string ordinal = converter.ConvertOrdinal(number);
                if (ordinal != C(number) + "lia" || ordinal.Contains("miliɔnlia", StringComparison.Ordinal) || ordinal.Contains("akpelia", StringComparison.Ordinal))
                    failures.Add($"{number}: '{ordinal}'");
            }
            if (failures.Count > 20) break;
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
