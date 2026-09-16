using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
    public void Constructor_DuplicateHour_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WithSpecialHours(new SpecialHourRule(12, "NOON"), new SpecialHourRule(12, "MIDDAY")));
    }

    [TestMethod]
    public void Constructor_NoSpecialHours_DefaultsToEmpty()
    {
        var converter = NumberToStringConverter.GetConverter("DE");
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

    // ─── wholeHour="false" (default) — replaces only the exact instant ────────

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

        string result = converter.Convert(new TimeOnly(0, 1, 0));

        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("MIDNIGHT"));
        StringAssert.Contains(result, "MINUTE");
    }

    // ─── replaceSpecialHours=false — disables substitution entirely ──────────

    [TestMethod]
    public void Convert_TimeOnly_ReplaceSpecialHoursFalse_AlwaysUsesNumeralHour()
    {
        var converter = WithSpecialHours(new SpecialHourRule(0, "MIDNIGHT", WholeHour: true), new SpecialHourRule(12, "NOON", WholeHour: true));

        string midnight = converter.Convert(new TimeOnly(0, 0, 0), replaceSpecialHours: false);
        string noon = converter.Convert(new TimeOnly(12, 0, 0), replaceSpecialHours: false);

        StringAssert.DoesNotMatch(midnight, new System.Text.RegularExpressions.Regex("MIDNIGHT"));
        StringAssert.DoesNotMatch(noon, new System.Text.RegularExpressions.Regex("NOON"));
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
}
