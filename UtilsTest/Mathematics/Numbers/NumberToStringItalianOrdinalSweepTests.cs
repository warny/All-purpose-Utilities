using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Mechanical regression guard over the Italian ordinal domain declared productive by NTS-13.
/// </summary>
/// <remarks>
/// These sweeps check structural invariants only — no exception, a soldered single word, the gender
/// suffix, no unstemmed final vowel, a stem that keeps the cardinal. They do <b>not</b> validate the
/// linguistic correctness of the forms: a morphologically plausible string can satisfy every
/// invariant and still be wrong (the mechanical <c>centodiecesimo</c> for 110 did). The sourced
/// forms of each morphological family are pinned by exact examples in <c>Italian.feature</c>; any
/// new family discovered must be added there first.
/// </remarks>
[TestClass]
public class NumberToStringItalianOrdinalSweepTests
{
    /// <summary>The Italian converter.</summary>
    private static NumberToStringConverter Italian => NumberToStringConverter.GetConverter("IT");

    /// <summary>
    /// Fragments that a mechanical suffix would leave behind: an unstemmed final vowel, an accented
    /// tre, an unconverted -mila, or the mechanical ordinal of dieci (the lexical decimo is used).
    /// </summary>
    private static readonly string[] ForbiddenInfixes = ["oesim", "aesim", "éesim", "milaesim", "mileesim", "diecesim"];

    /// <summary>Determines whether <paramref name="number"/> is a hundred followed by ten (110 … 910), whose ordinal ends in the lexical decimo.</summary>
    /// <param name="number">The value to classify.</param>
    /// <returns><see langword="true"/> for 110, 210 … 910.</returns>
    private static bool IsHundredAndTen(long number) => number is > 100 and < 1000 && number % 100 == 10;

    /// <summary>Determines whether <paramref name="number"/> is a thousand ending in ten below 2000 (1010 … 1910), left fail-closed (NTS-15).</summary>
    /// <param name="number">The value to classify.</param>
    /// <returns><see langword="true"/> for 1010, 1110 … 1910.</returns>
    private static bool IsThousandEndingInTen(long number) => number is > 1000 and < 2000 && number % 100 == 10;

    /// <summary>Enumerates the suffixed productive domain: 11-1999 and the round thousands, minus the x10 families.</summary>
    /// <returns>The values whose ordinal is formed by the stem rules and the suffix.</returns>
    private static IEnumerable<long> SuffixedDomain()
    {
        for (long n = 11; n <= 1999; n++)
            if (!IsHundredAndTen(n) && !IsThousandEndingInTen(n))
                yield return n;
        for (long n = 2000; n <= 999_000; n += 1000) yield return n;
    }

    /// <summary>Every suffixed value forms a well-shaped ordinal for the given gender (structural guard only).</summary>
    /// <param name="variant">The variant argument, or an empty string for the default (masculine) form.</param>
    /// <param name="suffix">The expected gender suffix.</param>
    [TestMethod]
    [DataRow("", "esimo")]
    [DataRow("gender=maschile", "esimo")]
    [DataRow("gender=femminile", "esima")]
    public void SuffixedDomain_IsStructurallyWellFormed(string variant, string suffix)
    {
        string[] variants = variant.Length == 0 ? [] : [variant];
        var failures = new List<string>();
        foreach (long number in SuffixedDomain())
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

    /// <summary>The suffixed stem keeps the cardinal: at most its last four characters are rewritten (structural guard only).</summary>
    [TestMethod]
    public void SuffixedDomain_KeepsTheCardinalBeforeItsRewrittenEnding()
    {
        var failures = new List<string>();
        foreach (long number in SuffixedDomain())
        {
            string cardinal = Italian.Convert((BigInteger)number);
            string stem = Italian.ConvertOrdinal(number)[..^"esimo".Length];
            // At most "mila" → "mill" or "ouno" → "un" is rewritten.
            int keep = Math.Max(0, cardinal.Length - 4);
            if (!stem.StartsWith(cardinal[..keep], StringComparison.Ordinal))
                failures.Add($"{number}: cardinal '{cardinal}', stem '{stem}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Every hundred followed by ten ends in the lexical decimo/decima after the unchanged hundreds.</summary>
    [TestMethod]
    public void HundredsAndTen_EndInLexicalDecimo()
    {
        for (long number = 110; number <= 910; number += 100)
        {
            string hundreds = Italian.Convert((BigInteger)(number - 10));
            Assert.AreEqual(hundreds + "decimo", Italian.ConvertOrdinal(number), $"number {number}");
            Assert.AreEqual(hundreds + "decima", Italian.ConvertOrdinal(number, "gender=femminile"), $"number {number}");
        }
    }

    /// <summary>Values outside the validated domain fail closed in both genders.</summary>
    /// <param name="number">The rejected value.</param>
    [TestMethod]
    [DataRow(0L)]
    [DataRow(1010L)]
    [DataRow(1110L)]
    [DataRow(1910L)]
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

    /// <summary>Every thousand ending in ten below 2000 fails closed.</summary>
    [TestMethod]
    public void ThousandsEndingInTen_AreRejected()
    {
        for (long number = 1010; number <= 1910; number += 100)
            Assert.Throws<NotSupportedException>(() => Italian.ConvertOrdinal(number), $"number {number}");
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
