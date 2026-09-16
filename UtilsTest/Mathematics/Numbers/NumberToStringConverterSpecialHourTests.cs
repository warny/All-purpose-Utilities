using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Tests for <see cref="SpecialHourRule"/> — a generic, per-hour word replacement for time-of-day
/// rendering (e.g. "midnight" for hour 0, "noon" for hour 12), configurable for any hour and any
/// language rather than hardcoded to noon/midnight. Applies only to Convert(TimeOnly) and the time
/// portion of Convert(DateTime); Convert(TimeSpan) — a duration, not a time of day — is unaffected.
/// </summary>
[TestClass]
public class NumberToStringConverterSpecialHourTests
{
    /// <summary>Builds a synthetic converter, independent of any real language's wording, with the given special-hour rules.</summary>
    private static NumberToStringConverter WithSpecialHours(params SpecialHourRule[] rules)
    {
        var source = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(source)
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("HOUR", "HOURS", null),
                ["minute"] = ("MINUTE", "MINUTES", null),
                ["second"] = ("SECOND", "SECONDS", null),
            },
            SpecialHours = rules,
        };
        return new NumberToStringConverter(options);
    }

    // ─── Construction validation ─────────────────────────────────────────────

    [TestMethod]
    public void Constructor_HourBelowZero_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => WithSpecialHours(new SpecialHourRule(-1, "MIDNIGHT")));
    }

    [TestMethod]
    public void Constructor_HourAbove23_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => WithSpecialHours(new SpecialHourRule(24, "MIDNIGHT")));
    }

    [TestMethod]
    public void Constructor_EmptyValue_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WithSpecialHours(new SpecialHourRule(0, "")));
    }

    [TestMethod]
    public void Constructor_WhitespaceOnlyValue_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WithSpecialHours(new SpecialHourRule(0, "   ")));
    }

    [TestMethod]
    public void Constructor_DuplicateHour_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WithSpecialHours(new SpecialHourRule(12, "NOON"), new SpecialHourRule(12, "MIDDAY")));
    }

    [TestMethod]
    public void Constructor_NoSpecialHours_DefaultsToEmpty()
    {
        // A synthetic converter with no SpecialHours configured — independent of any real
        // language's data, so this "no rules configured" case never breaks if a real language
        // (e.g. DE's "Mitternacht"/"Mittag") later gains SpecialHours of its own.
        var converter = WithSpecialHours();
        Assert.AreEqual(0, converter.SpecialHours.Count);
    }

    // ─── wholeHour="true" — replaces the hour for the entire hour-long slice ──

    [TestMethod]
    public void Convert_TimeOnly_WholeHourTrue_ExactHour_UsesSpecialWord()
    {
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT", WholeHour: true), new SpecialHourRule(12, "NOON", WholeHour: true));

        Assert.AreEqual("MIDNIGHT", converter.Convert(new TimeOnly(0, 0, 0)));
        Assert.AreEqual("NOON", converter.Convert(new TimeOnly(12, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_WholeHourTrue_NonZeroMinute_AppendsMinuteAfterSpecialWord()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        Assert.AreEqual($"NOON one{converter.Separator}MINUTE", converter.Convert(new TimeOnly(12, 1, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_WholeHourTrue_LastMinuteOfHour_StillUsesSpecialWord()
    {
        // 12:59 is still within the "noon" hour when wholeHour=true.
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        StringAssert.StartsWith(converter.Convert(new TimeOnly(12, 59, 0)), "NOON");
    }

    // ─── wholeHour="false" (default) — replaces only when minute/second are zero ──

    [TestMethod]
    public void Convert_TimeOnly_WholeHourFalse_ExactHour_UsesSpecialWord()
    {
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT"));

        Assert.AreEqual("MIDNIGHT", converter.Convert(new TimeOnly(0, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_WholeHourFalse_NonZeroMinute_FallsBackToNumeralHour()
    {
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT"));

        // Exact equality — not just "MIDNIGHT is absent" — so a regression that drops the hour
        // fragment entirely (rather than falling back to the numeral) would still fail this test.
        string expected = $"{converter.Convert(0)}{converter.Separator}HOURS{converter.Separator}{converter.Convert(1)}{converter.Separator}MINUTE";
        Assert.AreEqual(expected, converter.Convert(new TimeOnly(0, 1, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_WholeHourFalse_SubSecondComponent_StillCountsAsExactHour()
    {
        // Sub-second precision is silently discarded throughout time-of-day rendering (see
        // BuildTimeFragment): 12:00:00.500 has Minute==0 && Second==0, so it is treated as the
        // exact hour and still uses the special word, even though it is not, bit-for-bit, 12:00:00.
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON"));

        Assert.AreEqual("NOON", converter.Convert(new TimeOnly(12, 0, 0, 500)));
    }

    // ─── replaceSpecialHours=false — disables substitution entirely ──────────

    [TestMethod]
    public void Convert_TimeOnly_ReplaceSpecialHoursFalse_AlwaysUsesNumeralHour()
    {
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT", WholeHour: true), new SpecialHourRule(12, "NOON", WholeHour: true));

        // Exact equality against the plain numeral-hour rendering — not just "the special word is
        // absent" — so a regression that drops the hour fragment entirely would still fail this.
        string expectedMidnight = $"{converter.Convert(0)}{converter.Separator}HOURS";
        string expectedNoon = $"{converter.Convert(12)}{converter.Separator}HOURS";

        Assert.AreEqual(expectedMidnight, converter.Convert(new TimeOnly(0, 0, 0), replaceSpecialHours: false));
        Assert.AreEqual(expectedNoon, converter.Convert(new TimeOnly(12, 0, 0), replaceSpecialHours: false));
    }

    [TestMethod]
    public void Convert_TimeOnly_DefaultOverload_IsEquivalentToReplaceSpecialHoursTrue()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        Assert.AreEqual(
            converter.Convert(new TimeOnly(12, 0, 0), replaceSpecialHours: true),
            converter.Convert(new TimeOnly(12, 0, 0)));
    }

    // ─── DateTime propagates the flag to its time portion ─────────────────────

    [TestMethod]
    public void Convert_DateTime_ReplaceSpecialHoursTrue_UsesSpecialWordForTimePortion()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        StringAssert.Contains(converter.Convert(new DateTime(2026, 9, 16, 12, 0, 0)), "NOON");
    }

    [TestMethod]
    public void Convert_DateTime_ReplaceSpecialHoursFalse_UsesNumeralHourForTimePortion()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        string result = converter.Convert(new DateTime(2026, 9, 16, 12, 0, 0), replaceSpecialHours: false);

        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("NOON"));
        StringAssert.Contains(result, "HOUR");
    }

    // ─── TimeSpan (duration) is never affected — 12 hours of duration is not "noon" ───

    [TestMethod]
    public void Convert_TimeSpan_TwelveHours_IsNotReplacedBySpecialHour()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        string result = converter.Convert(new TimeSpan(12, 0, 0));

        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("NOON"));
        StringAssert.Contains(result, "HOUR");
    }

    [TestMethod]
    public void Convert_TimeSpan_TwelveHours_RendersExactPlainNumeralHour()
    {
        // Exact equality, not just "NOON is absent" — 12 hours of duration renders identically
        // whether or not a wholeHour=true rule is configured for hour 12.
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        string expected = $"{converter.Convert(12)}{converter.Separator}HOURS";
        Assert.AreEqual(expected, converter.Convert(new TimeSpan(12, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_ZeroHourComponent_OmitsHourFragmentEntirely()
    {
        // Unlike Convert(TimeOnly), which always renders the hour (even "zero hours"),
        // Convert(TimeSpan) omits the hour fragment entirely when there are zero whole hours —
        // so a duration matching the "midnight" hour value never even reaches the point where a
        // special-hour word could apply.
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT", WholeHour: true));

        string expected = $"{converter.Convert(30)}{converter.Separator}MINUTES";
        Assert.AreEqual(expected, converter.Convert(new TimeSpan(0, 30, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_MultiDayDuration_UsesTotalHoursNotClockHour()
    {
        // 1 day + 12 hours = 36 total hours. The clock-style ".Hours" component alone (12) would
        // coincide with the "noon" rule, but duration rendering sums Days*24+Hours before
        // formatting, and never consults SpecialHours at all — 36 is rendered as a plain numeral.
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        var duration = new TimeSpan(1, 12, 0, 0);
        string expected = $"{converter.Convert(36)}{converter.Separator}HOURS";
        Assert.AreEqual(expected, converter.Convert(duration));
    }

    [TestMethod]
    public void Convert_TimeSpan_NegativeTwelveHours_UsesMinusTemplateNotSpecialWord()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        string positive = $"{converter.Convert(12)}{converter.Separator}HOURS";
        string expected = converter.Minus.Replace("*", positive);
        Assert.AreEqual(expected, converter.Convert(new TimeSpan(-12, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_TwelveHoursFifteenMinutes_IsNotReplacedBySpecialHour()
    {
        var converter = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));

        string expected = $"{converter.Convert(12)}{converter.Separator}HOURS{converter.Separator}{converter.Convert(15)}{converter.Separator}MINUTES";
        Assert.AreEqual(expected, converter.Convert(new TimeSpan(12, 15, 0)));
    }

    [TestMethod]
    public void Convert_TimeSpan_EN_TwelveAndZeroHours_UseNumeralWordingNotNoonOrMidnight()
    {
        // Confirms the real, built-in EN configuration (which does declare "noon"/"midnight" for
        // TimeOnly) leaves Convert(TimeSpan) untouched.
        var en = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual("twelve hours", en.Convert(new TimeSpan(12, 0, 0)));
        Assert.AreEqual("thirty minutes", en.Convert(new TimeSpan(0, 30, 0)));
    }

    // ─── Cloning preserves SpecialHours ────────────────────────────────────────

    [TestMethod]
    public void NumberToStringConverterOptions_ClonedFromConverter_PreservesSpecialHours()
    {
        var original = WithSpecialHours(new SpecialHourRule(12, "NOON", WholeHour: true));
        var cloned = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("NOON", cloned.Convert(new TimeOnly(12, 0, 0)));
    }

    // ─── Real language data — FR/EN built-in configuration ─────────────────────

    [TestMethod]
    public void Convert_TimeOnly_EN_Noon_IsWordedNoon()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual("noon", en.Convert(new TimeOnly(12, 0, 0)));
        Assert.AreEqual("midnight", en.Convert(new TimeOnly(0, 0, 0)));
    }

    [TestMethod]
    public void Convert_TimeOnly_FR_MidiEtMinuit_SontUtilises()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        Assert.AreEqual("midi", fr.Convert(new TimeOnly(12, 0, 0)));
        Assert.AreEqual("minuit", fr.Convert(new TimeOnly(0, 0, 0)));
    }

    // ─── Interface default-implementation compatibility — pre-SpecialHourRule implementers ───

    [TestMethod]
    public void Convert_TimeOnly_LegacyInterfaceImplementer_IgnoresReplaceSpecialHoursFlag()
    {
        // A converter written before SpecialHourRule existed only overrides the params-only
        // overload; the new bool overload must still work by forwarding to it, unchanged —
        // including the variants it was called with, not just the flag-independent constant part.
        INumberToStringConverter converter = new LegacyConverter();

        Assert.AreEqual("legacy-time:gender=feminin", converter.Convert(new TimeOnly(12, 0), replaceSpecialHours: false, variants: "gender=feminin"));
        Assert.AreEqual("legacy-time:gender=feminin", converter.Convert(new TimeOnly(12, 0), replaceSpecialHours: true, variants: "gender=feminin"));
        Assert.AreEqual("legacy-time:", converter.Convert(new TimeOnly(12, 0), replaceSpecialHours: false));
    }

    [TestMethod]
    public void Convert_DateTime_LegacyInterfaceImplementer_IgnoresReplaceSpecialHoursFlag()
    {
        INumberToStringConverter converter = new LegacyConverter();

        Assert.AreEqual("legacy-datetime:gender=feminin", converter.Convert(new DateTime(2026, 9, 16, 12, 0, 0), replaceSpecialHours: false, variants: "gender=feminin"));
        Assert.AreEqual("legacy-datetime:gender=feminin", converter.Convert(new DateTime(2026, 9, 16, 12, 0, 0), replaceSpecialHours: true, variants: "gender=feminin"));
        Assert.AreEqual("legacy-datetime:", converter.Convert(new DateTime(2026, 9, 16, 12, 0, 0), replaceSpecialHours: false));
    }

    /// <summary>
    /// Minimal <see cref="INumberToStringConverter"/> implementer predating <see cref="SpecialHourRule"/>:
    /// it overrides only the original params-only time/date overloads, exactly like third-party code
    /// written before that feature existed. Echoes <c>variants</c> into the result so a future
    /// regression that drops them while forwarding to this overload (e.g. calling
    /// <c>Convert(time)</c> instead of <c>Convert(time, variants)</c>) fails these tests.
    /// </summary>
    private sealed class LegacyConverter : INumberToStringConverter
    {
        public BigInteger? MaxNumber => null;
        public string Convert(BigInteger number) => number.ToString();
        public string Convert(int number) => number.ToString();
        public string Convert(long number) => number.ToString();
        public string Convert(decimal number) => number.ToString();
        public string Convert(TimeOnly time, params string[] variants) => $"legacy-time:{string.Join(",", variants)}";
        public string Convert(DateTime dateTime, params string[] variants) => $"legacy-datetime:{string.Join(",", variants)}";
    }
}
