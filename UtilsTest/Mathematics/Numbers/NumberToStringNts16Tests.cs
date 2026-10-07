using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-16/NTS-19 guards: the Wolof ordinal domain guard and mechanical (structural, not linguistic)
/// checks of the Wolof <c>-éel</c> suffix and thousands. The forms themselves are pinned by the
/// sourced examples of the Wolof feature; the Ewe ordinals restored by NTS-20 are covered by
/// <see cref="NumberToStringNts20Tests"/>.
/// </summary>
[TestClass]
public class NumberToStringNts16Tests
{
    /// <summary>An empty variant query.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoVariants = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// The Wolof guard rejects zero and the round thousands (last element junni, NTS-19), and lets
    /// every other value through to the declarative pipeline.
    /// </summary>
    [TestMethod]
    public void WolofGuard_RejectsZeroAndRoundThousands_LetsOtherValuesThrough()
    {
        var guard = new WolofOrdinalLanguageSpecifics();

        foreach (long rejected in new long[] { 0, 1000, 2000, 21000, 999000, long.MaxValue - long.MaxValue % 1000 })
            Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(rejected, NoVariants, out _), rejected.ToString());
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(1000, NoVariants, out _));
        foreach (long accepted in new long[] { 1, 999, 1001, 1100, 999999, long.MaxValue })
        {
            Assert.IsFalse(guard.TryConvertOrdinal(accepted, NoVariants, out string? result), accepted.ToString());
            Assert.IsNull(result);
        }
        Assert.IsFalse(guard.TryConvertOrdinal(1001, NoVariants, out _));
        Assert.AreEqual("text", guard.FinalizeWriting("WO", "text"));
    }

    /// <summary>
    /// Mechanical guard over the whole public domain (1-999999): every Wolof ordinal except 1 and the
    /// round thousands is its cardinal with <c>éel</c> appended to the last element, the cardinal
    /// itself being otherwise unchanged; the round thousands are rejected.
    /// </summary>
    [TestMethod]
    public void Wolof_Ordinals_AppendEelToTheCardinal_ExceptRoundThousands()
    {
        var converter = NumberToStringConverter.GetConverter("WO");
        var failures = new List<string>();
        for (long number = 2; number <= 999_999; number++)
        {
            if (number % 1000 == 0)
            {
                try
                {
                    converter.ConvertOrdinal(number);
                    failures.Add($"{number}: round thousand accepted");
                }
                catch (NotSupportedException)
                {
                }
                continue;
            }
            string cardinal = converter.Convert((BigInteger)number);
            string ordinal = converter.ConvertOrdinal(number);
            if (ordinal != cardinal + "éel")
                failures.Add($"{number}: cardinal '{cardinal}', ordinal '{ordinal}'");
            if (failures.Count > 20) break;
        }

        Assert.AreEqual("bu njëkk", converter.ConvertOrdinal(1));
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Mechanical guard of the Wolof thousands (NTS-19): "ak" joins the thousands to a non-empty lower
    /// group and only then, never doubled nor trailing; every multiplier of junni bears the attached
    /// connective -i except the bare junni of 1000-1999; no hyphen remains in any cardinal.
    /// </summary>
    [TestMethod]
    public void Wolof_Thousands_UseAkAndTheConnective()
    {
        var converter = NumberToStringConverter.GetConverter("WO");
        var failures = new List<string>();
        for (long number = 1; number <= 999_999; number++)
        {
            string cardinal = converter.Convert((BigInteger)number);
            bool hasLower = number % 1000 != 0;
            if (cardinal.Contains('-') || cardinal.Contains("ak ak", StringComparison.Ordinal)
                || cardinal.EndsWith(" ak", StringComparison.Ordinal) || cardinal.StartsWith("benn junni", StringComparison.Ordinal))
                failures.Add($"{number}: '{cardinal}'");
            else if (number >= 1000)
            {
                bool akAfterJunni = cardinal.Contains("junni ak ", StringComparison.Ordinal);
                bool multiplierMarked = number < 2000 ? cardinal.StartsWith("junni", StringComparison.Ordinal) : cardinal.Contains("i junni", StringComparison.Ordinal);
                if (akAfterJunni != hasLower || !multiplierMarked)
                    failures.Add($"{number}: '{cardinal}'");
            }
            if (failures.Count > 20) break;
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
