using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-16 guards: the Wolof ordinal domain guard and mechanical (structural, not linguistic) checks
/// of the Wolof <c>-eel</c> suffix and of the withdrawn Ewe ordinals. The forms themselves are pinned
/// by the sourced examples of the Wolof and Ewe features.
/// </summary>
[TestClass]
public class NumberToStringNts16Tests
{
    /// <summary>An empty variant query.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoVariants = ImmutableDictionary<string, string>.Empty;

    /// <summary>The Wolof guard rejects zero and every value from a thousand, and lets 1-999 through.</summary>
    [TestMethod]
    public void WolofGuard_RejectsZeroAndThousands_LetsLowerValuesThrough()
    {
        var guard = new WolofOrdinalLanguageSpecifics();

        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(0L, NoVariants, out _));
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(0, NoVariants, out _));
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(1000L, NoVariants, out _));
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(1000, NoVariants, out _));
        Assert.Throws<NotSupportedException>(() => guard.TryConvertOrdinal(long.MaxValue, NoVariants, out _));
        Assert.IsFalse(guard.TryConvertOrdinal(1L, NoVariants, out string? result));
        Assert.IsNull(result);
        Assert.IsFalse(guard.TryConvertOrdinal(999, NoVariants, out _));
        Assert.AreEqual("text", guard.FinalizeWriting("WO", "text"));
    }

    /// <summary>
    /// Mechanical guard: every Wolof ordinal from 2 to 999 is its cardinal with <c>-eel</c> appended
    /// to the last element, the cardinal itself being otherwise unchanged; 1 is the suppletive form.
    /// </summary>
    [TestMethod]
    public void Wolof_OrdinalsBelowAThousand_AppendEelToTheCardinal()
    {
        var converter = NumberToStringConverter.GetConverter("WO");
        var failures = new List<string>();
        for (long number = 2; number <= 999; number++)
        {
            string cardinal = converter.Convert((BigInteger)number);
            string ordinal = converter.ConvertOrdinal(number);
            if (ordinal != cardinal + "eel")
                failures.Add($"{number}: cardinal '{cardinal}', ordinal '{ordinal}'");
        }

        Assert.AreEqual("bu njëkk", converter.ConvertOrdinal(1));
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Ewe ordinals are withdrawn until the cardinal rebuild (NTS-20): the converter no longer
    /// advertises them and rejects every value, while its cardinals keep working.
    /// </summary>
    [TestMethod]
    public void Ewe_OrdinalsAreUnsupported_CardinalsUnaffected()
    {
        var converter = NumberToStringConverter.GetConverter("EE");

        Assert.IsFalse(converter.SupportsOrdinals);
        foreach (long number in new long[] { 0, 1, 2, 21, 1000, -2 })
            Assert.Throws<NotSupportedException>(() => converter.ConvertOrdinal(number));
        Assert.AreEqual("eve", converter.Convert((BigInteger)2));
    }
}
