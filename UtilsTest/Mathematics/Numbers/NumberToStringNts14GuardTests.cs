using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-14 guards: the shared zero-ordinal guard plugin, and mechanical (structural, not linguistic)
/// sweeps over the Hebrew compound ordinals and the Italian scale nouns. The forms themselves are
/// pinned by the sourced examples of the language features.
/// </summary>
[TestClass]
public class NumberToStringNts14GuardTests
{
    /// <summary>An empty variant query.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoVariants = ImmutableDictionary<string, string>.Empty;

    /// <summary>The zero guard rejects zero and lets every other value through to the declarative pipeline.</summary>
    [TestMethod]
    public void ZeroGuard_RejectsZeroOnly()
    {
        var guard = new ZeroOrdinalUnsupportedLanguageSpecifics();

        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(0L, NoVariants, out _));
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(0, NoVariants, out _));
        Assert.IsFalse(guard.TryConvertOrdinal(1L, NoVariants, out string? result));
        Assert.IsNull(result);
        Assert.IsFalse(guard.TryConvertOrdinal(long.MaxValue, NoVariants, out _));
        Assert.AreEqual("text", guard.FinalizeWriting("XX", "text"));
    }

    /// <summary>The guarded languages keep their non-zero ordinals.</summary>
    /// <param name="culture">The guarded culture.</param>
    [TestMethod]
    [DataRow("CA")]
    [DataRow("CA-valencia")]
    [DataRow("WO")]
    [DataRow("EE")]
    public void ZeroGuard_GuardedLanguages_StillFormNonZeroOrdinals(string culture)
    {
        var converter = NumberToStringConverter.GetConverter(culture);

        Assert.Throws<NotSupportedException>(() => converter.ConvertOrdinal(0));
        Assert.IsFalse(string.IsNullOrEmpty(converter.ConvertOrdinal(2)));
        Assert.IsFalse(string.IsNullOrEmpty(converter.ConvertOrdinal(21)));
    }

    /// <summary>
    /// Mechanical guard: every default Hebrew ordinal whose last two digits are 11-19 equals the
    /// zachar (masculine) ordinal, while the standalone cardinal keeps its counting teen.
    /// </summary>
    [TestMethod]
    public void Hebrew_DefaultOrdinalsEndingInATeen_EqualTheMasculineVariant()
    {
        var converter = NumberToStringConverter.GetConverter("HE");
        var failures = new List<string>();
        for (long number = 111; number <= 99_999; number++)
        {
            if (number % 100 is < 11 or > 19) continue;
            string defaultOrdinal = converter.ConvertOrdinal(number);
            string masculine = converter.ConvertOrdinal(number, "gender=zachar");
            string cardinal = converter.Convert((BigInteger)number);
            if (defaultOrdinal != masculine || !cardinal.EndsWith("עשרה", StringComparison.Ordinal))
                failures.Add($"{number}: ordinal '{defaultOrdinal}', zachar '{masculine}', cardinal '{cardinal}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Mechanical guard: from a million the Italian scale nouns are lower-case separate words, a
    /// multiplier of one is the article "un", and the lower groups are joined by "e".
    /// </summary>
    [TestMethod]
    public void Italian_ScaleNouns_AreLowerCaseSeparateWordsJoinedByE()
    {
        var converter = NumberToStringConverter.GetConverter("IT");
        var failures = new List<string>();
        BigInteger million = BigInteger.Pow(10, 6);
        for (int scale = 2; scale <= 7; scale++)
        {
            BigInteger unit = BigInteger.Pow(1000, scale);
            foreach (BigInteger value in new[] { unit, 2 * unit, unit + 1, 21 * unit + 1000 })
            {
                string text = converter.Convert(value);
                if (text != text.ToLowerInvariant() || text.StartsWith("uno ", StringComparison.Ordinal)
                    || text.Contains("lli") || (value % unit != 0 && !text.Contains(" e ")) || !text.Contains(' '))
                    failures.Add($"{value}: '{text}'");
            }
        }
        if (converter.Convert(million - 1).Contains(' '))
            failures.Add("999999 is no longer soldered");

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
