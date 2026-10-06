using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Sweeps the validated Italian ordinal domain (NTS-13) to detect regressions that the sourced
/// examples of <c>Italian.feature</c> would miss: every value from 11 to 1999 and every round
/// thousand up to 999000 must be produced from the cardinal through the stem rules, in both genders,
/// and every value outside that domain must keep failing closed.
/// </summary>
[TestClass]
public class NumberToStringItalianOrdinalSweepTests
{
    /// <summary>The Italian converter.</summary>
    private static NumberToStringConverter Italian => NumberToStringConverter.GetConverter("IT");

    /// <summary>Endings that a mechanical suffix on an unstemmed final vowel would leave behind.</summary>
    private static readonly string[] ForbiddenInfixes = ["oesim", "aesim", "éesim", "milaesim", "mileesim"];

    /// <summary>Enumerates the productive domain: 11-1999 and the round thousands 2000-999000.</summary>
    /// <returns>The values whose ordinal must be formed.</returns>
    private static IEnumerable<long> ProductiveDomain()
    {
        for (long n = 11; n <= 1999; n++) yield return n;
        for (long n = 2000; n <= 999_000; n += 1000) yield return n;
    }

    /// <summary>Asserts that every productive value forms a well-shaped ordinal for the given gender.</summary>
    /// <param name="variant">The variant argument, or an empty string for the default (masculine) form.</param>
    /// <param name="suffix">The expected gender suffix.</param>
    [TestMethod]
    [DataRow("", "esimo")]
    [DataRow("gender=maschile", "esimo")]
    [DataRow("gender=femminile", "esima")]
    public void ProductiveDomain_FormsSuffixedOrdinalFromCardinal(string variant, string suffix)
    {
        string[] variants = variant.Length == 0 ? [] : [variant];
        var failures = new List<string>();
        foreach (long number in ProductiveDomain())
        {
            string ordinal;
            try
            {
                ordinal = Italian.ConvertOrdinal(number, variants);
            }
            catch (Exception exception)
            {
                failures.Add($"{number}: {exception.GetType().Name}");
                continue;
            }
            string cardinal = Italian.Convert((BigInteger)number);
            if (ordinal == cardinal || !ordinal.EndsWith(suffix, StringComparison.Ordinal) || ordinal.Contains(' '))
                failures.Add($"{number}: '{ordinal}'");
            foreach (string infix in ForbiddenInfixes)
                if (ordinal.Contains(infix, StringComparison.Ordinal))
                    failures.Add($"{number}: '{ordinal}' contains '{infix}'");
            // "-iesim" is only legitimate after the preserved -i of sei ("ventiseiesimo").
            if (ordinal.Contains("iesim", StringComparison.Ordinal) && !ordinal.Contains("seiesim", StringComparison.Ordinal))
                failures.Add($"{number}: '{ordinal}' keeps a final -i");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The stem is derived from the cardinal: the ordinal without its suffix is a prefix-preserving rewrite of the cardinal ending.</summary>
    [TestMethod]
    public void ProductiveDomain_KeepsTheCardinalBeforeItsRewrittenEnding()
    {
        var failures = new List<string>();
        foreach (long number in ProductiveDomain())
        {
            string cardinal = Italian.Convert((BigInteger)number);
            string stem = Italian.ConvertOrdinal(number)[..^"esimo".Length];
            // At most the last four characters of the cardinal ("mila" → "mill", "ouno" → "un") are rewritten.
            int keep = Math.Max(0, cardinal.Length - 4);
            if (!stem.StartsWith(cardinal[..keep], StringComparison.Ordinal))
                failures.Add($"{number}: cardinal '{cardinal}', stem '{stem}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Values outside the validated domain fail closed in both genders.</summary>
    /// <param name="number">The rejected value.</param>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(2001L)]
    [DataRow(2999L)]
    [DataRow(21_001L)]
    [DataRow(100_001L)]
    [DataRow(999_999L)]
    [DataRow(1_000_000L)]
    [DataRow(1_001_000L)]
    [DataRow(long.MaxValue)]
    public void OutsideDomain_IsRejected(long number)
    {
        Assert.Throws<NotSupportedException>(() => Italian.ConvertOrdinal(number));
        Assert.Throws<NotSupportedException>(() => Italian.ConvertOrdinal(number, "gender=femminile"));
    }

    /// <summary>The rejection of non-round thousands holds across a whole sweep of 2001-9999.</summary>
    [TestMethod]
    public void NonRoundThousandsAboveNineteenNinetyNine_AreRejected()
    {
        for (long number = 2001; number <= 9999; number++)
        {
            if (number % 1000 == 0) continue;
            Assert.Throws<NotSupportedException>(() => Italian.ConvertOrdinal(number), $"number {number}");
        }
    }

    /// <summary>Zero does not become a mechanical "zeresimo" now that the vowel stem rule exists.</summary>
    [TestMethod]
    public void Zero_IsNotDerivedMechanically()
        => Assert.Throws<NotSupportedException>(() => Italian.ConvertOrdinal(0L));
}
