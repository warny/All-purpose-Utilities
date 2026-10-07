using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-20 mechanical guards over the whole Ewe domain (structural, not linguistic: the forms themselves
/// are pinned by the sourced examples of <c>Ewe.feature</c>). They prove that every cardinal decomposes
/// into the configured rules (scale noun first, multiplier identical to the standalone cardinal, the
/// <c>kple</c>/<c>vɔ</c> connectors) and that every ordinal is its cardinal with <c>-lia</c> on the last
/// element, "first" excepted.
/// </summary>
[TestClass]
public class NumberToStringNts20Tests
{
    /// <summary>Tokens of the former, unsourced configuration that must never reappear.</summary>
    private static readonly string[] LegacyTokens = ["deka", "eto", "atɔ ", "adrɛ", "asea", "blavo", "kpeɖe", "zero", "etsõ"];

    /// <summary>Gets the Ewe converter.</summary>
    private static NumberToStringConverter Ewe => NumberToStringConverter.GetConverter("EE");

    /// <summary>Converts a cardinal.</summary>
    private static string C(long value) => Ewe.Convert((BigInteger)value);

    /// <summary>The Ewe configuration uses the scale-first order only; its multipliers are standalone cardinals.</summary>
    [TestMethod]
    public void Ewe_UsesScaleFirstWithoutScopedGroups()
    {
        Assert.AreEqual(ScaleMultiplierPosition.AfterScale, Ewe.ScaleMultiplierPosition);
        Assert.AreEqual(0, Ewe.ScaleScopedGroups.Count);
        Assert.IsTrue(Ewe.SupportsOrdinals);
    }

    /// <summary>
    /// Every cardinal from 0 to 999 999 is the recomposition of its parts: akpe + the standalone cardinal
    /// of the multiplier, then "kple" before a lower part below 100 or a plain space before one with
    /// hundreds; alafa + unit, then "kple" before 1-10; tens + "vɔ" + unit, one being ɖekɛ there.
    /// </summary>
    [TestMethod]
    public void Ewe_CardinalSweep_DecomposesIntoTheConfiguredRules()
    {
        var converter = Ewe;
        var cardinals = new string[1_000_000];
        var failures = new List<string>();
        for (long number = 0; number < cardinals.Length; number++)
        {
            string text = converter.Convert((BigInteger)number);
            cardinals[number] = text;
            if (text.Length == 0 || text.Contains("  ", StringComparison.Ordinal) || text != text.Trim())
                failures.Add($"{number}: blank or badly spaced '{text}'");
            foreach (string legacy in LegacyTokens)
                if (text.Contains(legacy, StringComparison.Ordinal))
                    failures.Add($"{number}: legacy token '{legacy}' in '{text}'");
            foreach (string doubled in new[] { "kple kple", "vɔ vɔ", "kple vɔ", "vɔ kple", "akpe akpe" })
                if (text.Contains(doubled, StringComparison.Ordinal))
                    failures.Add($"{number}: doubled connector in '{text}'");
            if (text.EndsWith(" kple", StringComparison.Ordinal) || text.EndsWith(" vɔ", StringComparison.Ordinal))
                failures.Add($"{number}: trailing connector in '{text}'");

            // The teens are the lexicalized wui- forms, never the former "ewo kple" + unit.
            if (number is >= 11 and <= 19 && !text.StartsWith("wui", StringComparison.Ordinal))
                failures.Add($"{number}: teen '{text}'");
            string expected = Expected(number, cardinals);
            if (expected is not null && text != expected)
                failures.Add($"{number}: expected '{expected}', got '{text}'");
            if (failures.Count > 20) break;
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));

        // Recomposition from already verified smaller values (null for the lexical base cases).
        static string Expected(long number, string[] cardinals)
        {
            if (number >= 1000)
            {
                long lower = number % 1000;
                string head = "akpe " + cardinals[number / 1000];
                return lower == 0 ? head : head + (lower < 100 ? " kple " : " ") + cardinals[lower];
            }
            if (number >= 100)
            {
                long rest = number % 100;
                string head = "alafa " + cardinals[number / 100];
                return rest == 0 ? head : head + (rest <= 10 ? " kple " : " ") + cardinals[rest];
            }
            if (number > 20 && number % 10 != 0)
                return cardinals[number - number % 10] + " vɔ " + (number % 10 == 1 ? "ɖekɛ" : cardinals[number % 10]);
            return null!;
        }
    }

    /// <summary>
    /// Every ordinal from 1 to 999 999 is gbãtɔ for one and otherwise the cardinal with "lia" appended to
    /// its last element (the multiplier of akpe, the scale noun never being suffixed); zero is rejected.
    /// </summary>
    [TestMethod]
    public void Ewe_OrdinalSweep_AppendsLiaToTheCardinal()
    {
        var converter = Ewe;
        var failures = new List<string>();
        for (long number = 2; number <= 999_999; number++)
        {
            string cardinal = converter.Convert((BigInteger)number);
            string ordinal = converter.ConvertOrdinal(number);
            if (ordinal != cardinal + "lia" || ordinal.Contains("akpelia", StringComparison.Ordinal) || ordinal.Contains("etsõ", StringComparison.Ordinal))
                failures.Add($"{number}: cardinal '{cardinal}', ordinal '{ordinal}'");
            if (failures.Count > 20) break;
        }

        Assert.AreEqual("gbãtɔ", converter.ConvertOrdinal(1));
        Assert.Throws<NotSupportedException>(() => converter.ConvertOrdinal(0));
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
