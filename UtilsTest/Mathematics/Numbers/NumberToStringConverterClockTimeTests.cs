using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.NumberToString;
using Utils.Range;

namespace UtilsTest.NumberToString;

/// <summary>Tests configurable idiomatic clock-time conversion and its validation.</summary>
[TestClass]
public class NumberToStringConverterClockTimeTests
{
    /// <summary>Verifies nearest rounding, forward half-step ties, and midnight wrapping.</summary>
    [TestMethod]
    public void ConvertClockTime_RoundsNearestWithForwardTies()
    {
        var converter = NumberToStringConverter.GetConverter("DE");
        Assert.AreEqual("eins", converter.ConvertClockTime(new TimeOnly(1, 2, 29)));
        Assert.AreEqual("fünf nach eins", converter.ConvertClockTime(new TimeOnly(1, 2, 30)));
        Assert.AreEqual("fünf vor halb zwei", converter.ConvertClockTime(new TimeOnly(1, 27)));
        Assert.AreEqual("halb zwei", converter.ConvertClockTime(new TimeOnly(1, 28)));
        Assert.AreEqual("fünf vor zwölf", converter.ConvertClockTime(new TimeOnly(23, 57)));
        Assert.AreEqual("zwölf", converter.ConvertClockTime(new TimeOnly(23, 58)));
    }

    /// <summary>Verifies DateTime rounding advances its date when midnight is crossed.</summary>
    [TestMethod]
    public void ConvertClockTime_DateTimeMidnightCarry_UsesFollowingDate()
    {
        var converter = NumberToStringConverter.GetConverter("FR");
        string result = converter.ConvertClockTime(new DateTime(2025, 1, 1, 23, 58, 0));
        Assert.AreEqual("deux janvier deux mille vingt cinq minuit", result);
    }

    /// <summary>Verifies call options can disable special-hour replacement without overload ambiguity.</summary>
    [TestMethod]
    public void ConvertClockTime_OptionsCanDisableSpecialHours()
    {
        var converter = NumberToStringConverter.GetConverter("FR");
        string result = converter.ConvertClockTime(
            new TimeOnly(12, 15),
            new ClockTimeConversionOptions { ReplaceSpecialHours = false },
            System.Array.Empty<string>());
        Assert.AreEqual("douze heures et quart", result);
    }

    /// <summary>Verifies the legacy exact TimeOnly API retains seconds and does not use clock rules.</summary>
    [TestMethod]
    public void Convert_TimeOnly_RemainsExact()
    {
        var converter = NumberToStringConverter.GetConverter("DE");
        Assert.AreEqual("eine Stunde siebenundzwanzig Minuten drei Sekunden", converter.Convert(new TimeOnly(1, 27, 3)));
    }

    /// <summary>Verifies construction rejects structurally ambiguous or incomplete clock rules.</summary>
    [TestMethod]
    public void Constructor_InvalidClockRules_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(0, ValidRules()));
        Assert.ThrowsExactly<ArgumentException>(() => Create(7, ValidRules()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(5, ValidRules(), hourCycle: 13));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>(), 0, ClockHourForm.Cardinal, "{hour}")]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(5, [new(IntRange<int>.FullRange, 0, ClockHourForm.Cardinal, "{hour}")]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(5, [new(new IntRange<int>("0,60"), 0, ClockHourForm.Cardinal, "{hour}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0-5"), 0, ClockHourForm.Cardinal, "{hour}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, " ")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{unknown}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "hour}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour.Length}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{amount + 1}")]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{amount}", 0)]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}", 0, ClockAmountDirection.After)]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(5, [new(new IntRange<int>("0,5"), 0, ClockHourForm.Cardinal, "{hour}"), new(new IntRange<int>("5"), 0, ClockHourForm.Cardinal, "{hour}")]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(5, [new(new IntRange<int>("0,5,10,15,20,25,30,35,40,45,50,55"), 0, (ClockHourForm)42, "{hour}")]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(5, [new(new IntRange<int>("0,5,10,15,20,25,30,35,40,45,50,55"), 0, ClockHourForm.Cardinal, "{amount}", 60, (ClockAmountDirection)42)]));
    }

    /// <summary>Verifies extreme offsets normalize without overflow and variants reach numeric placeholders.</summary>
    [TestMethod]
    public void ConvertClockTime_ExtremeOffsetsAndVariants_AreHandledSafely()
    {
        var source = NumberToStringConverter.GetConverter("FR");
        var options = new NumberToStringConverterOptions(source)
        {
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                HourCycle = 12,
                Rules = [new(new IntRange<int>("0"), int.MaxValue, ClockHourForm.Cardinal, "{hour}")],
            },
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("une", converter.ConvertClockTime(new TimeOnly(18, 0), "gender=feminin"));
    }

    /// <summary>Verifies an inherited built-in clock section remains available through the interface.</summary>
    [TestMethod]
    public void ClockTime_BaseOnAbsent_InheritsCompleteParentSection()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("DE-ch");
        Assert.IsTrue(converter.SupportsClockTimeConversion);
        Assert.AreEqual("halb zwei", converter.ConvertClockTime(new TimeOnly(13, 30)));
    }

    /// <summary>Verifies precompiled patterns never rescan placeholder text injected by a special hour.</summary>
    [TestMethod]
    public void ConvertClockTime_InjectedPlaceholderText_RemainsLiteral()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            SpecialHours = [new(12, "about {amount}", WholeHour: true)],
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 5,
                Rules =
                [
                    new(new IntRange<int>("0,10,15,20,25,30,35,40,45,50,55"), 0, ClockHourForm.Cardinal, "{hour}"),
                    new(new IntRange<int>("5"), 0, ClockHourForm.Cardinal, "{hour} past {amount}", 0, ClockAmountDirection.After),
                ],
            },
        };

        Assert.AreEqual("about {amount} past five", new NumberToStringConverter(options).ConvertClockTime(new TimeOnly(12, 5)));
    }

    /// <summary>Verifies clock conversion preserves exact-only and whole-hour special-hour semantics.</summary>
    [TestMethod]
    public void ConvertClockTime_SpecialHourWholeHourContract_IsPreserved()
    {
        NumberToStringConverter exactOnly = CreateSpecialHourConverter(wholeHour: false);
        NumberToStringConverter wholeHour = CreateSpecialHourConverter(wholeHour: true);

        Assert.AreEqual("twelve", exactOnly.ConvertClockTime(new TimeOnly(11, 55)));
        Assert.AreEqual("noon", wholeHour.ConvertClockTime(new TimeOnly(11, 55)));
        Assert.AreEqual("midnight", exactOnly.ConvertClockTime(new TimeOnly(23, 58)));
    }

    /// <summary>Verifies statically unavailable hour rendering capabilities fail at construction.</summary>
    [TestMethod]
    public void Constructor_UnavailableHourFormCapabilities_AreRejected()
    {
        var noUnits = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnits = new Dictionary<string, (string, string, string?)>(),
            TimeUnitForcedVariants = new Dictionary<string, ForcedVariantSet>(),
            TimeUnitForms = new Dictionary<string, LexicalFormSet>(),
            TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>(),
            SpecialHours = [],
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules = [new(new IntRange<int>("0"), 0, ClockHourForm.TimeUnit, "{hour}")],
            },
        };
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(noUnits));

        var noOrdinals = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            OrdinalSuffix = null,
            OrdinalPrefix = null,
            OrdinalExceptions = new Dictionary<long, string>(),
            OrdinalWordRules = new Dictionary<string, string>(),
            OrdinalVariants = [],
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules = [new(new IntRange<int>("0"), 0, ClockHourForm.Ordinal, "{hour}")],
            },
        };
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(noOrdinals));
    }

    /// <summary>Verifies every supported minimal pattern shape compiles and renders.</summary>
    [TestMethod]
    public void ConvertClockTime_SupportedPatternShapes_Render()
    {
        Assert.AreEqual("one", Create(60, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")]).ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("zero", Create(60, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{amount}", 0, ClockAmountDirection.After)]).ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("one minus zero", Create(60, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour} minus {amount}", 0, ClockAmountDirection.After)]).ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("half one", Create(60, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "half {hour}")]).ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("noon", Create(60, [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "noon")]).ConvertClockTime(new TimeOnly(1, 0)));
    }

    /// <summary>Verifies converter construction snapshots rules and option cloning preserves them.</summary>
    [TestMethod]
    public void Constructor_ClockRules_AreSnapshottedAndCloned()
    {
        var rules = ValidRules();
        var converter = Create(5, rules);
        rules.Clear();
        Assert.AreEqual("one", converter.ConvertClockTime(new TimeOnly(1, 0)));
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual("one", clone.ConvertClockTime(new TimeOnly(1, 0)));
    }

    /// <summary>Creates a converter using English number words and supplied clock rules.</summary>
    private static NumberToStringConverter Create(int step, IReadOnlyList<ClockTimeRule> rules, int hourCycle = 24)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            ClockTime = new ClockTimeFormatOptions { Step = step, HourCycle = hourCycle, Rules = rules },
        };
        return new NumberToStringConverter(options);
    }

    /// <summary>Creates a complete five-minute rule list suitable for validation helpers.</summary>
    private static List<ClockTimeRule> ValidRules()
        => [new(new IntRange<int>("0,5,10,15,20,25,30,35,40,45,50,55"), 0, ClockHourForm.Cardinal, "{hour}")];

    /// <summary>Creates a converter that exercises offset and rounded-exact special-hour behavior.</summary>
    private static NumberToStringConverter CreateSpecialHourConverter(bool wholeHour)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            SpecialHours = [new(12, "noon", wholeHour), new(0, "midnight")],
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 5,
                Rules =
                [
                    new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}"),
                    new(new IntRange<int>("5,10,15,20,25,30,35,40,45,50,55"), 1, ClockHourForm.Cardinal, "{hour}"),
                ],
            },
        };
        return new NumberToStringConverter(options);
    }
}
