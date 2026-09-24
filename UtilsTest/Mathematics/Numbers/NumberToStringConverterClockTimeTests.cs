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
    /// <summary>Verifies concrete ordinal overloads fail closed when no ordinal implementation exists.</summary>
    [TestMethod]
    public void ConvertOrdinal_UnsupportedConverter_ThrowsForIntAndLong()
    {
        NumberToStringConverter converter = CreateWithoutOrdinalSupport();

        Assert.IsFalse(converter.SupportsOrdinals);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(21));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1L));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(21L));
    }

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

    /// <summary>Verifies disabling English special-hour words restores the complete numeric-hour pattern.</summary>
    [TestMethod]
    public void ConvertClockTime_EnglishDisabledSpecialHours_UsesOClockPattern()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        var options = new ClockTimeConversionOptions { ReplaceSpecialHours = false };

        Assert.AreEqual("twelve o'clock", converter.ConvertClockTime(new TimeOnly(0, 0), options, []));
        Assert.AreEqual("twelve o'clock", converter.ConvertClockTime(new TimeOnly(12, 0), options, []));
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
        Assert.ThrowsExactly<ArgumentException>(() => Create(60,
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")
            {
                SpecialHourPattern = "{unknown}",
            },
        ]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(60,
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")
            {
                SpecialHourPattern = "literal",
            },
        ]));
        Assert.ThrowsExactly<ArgumentException>(() => Create(60,
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")
            {
                SpecialHourPattern = "{hour",
            },
        ]));
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

    /// <summary>Verifies hour and amount forcings override independently and aliases are canonicalized.</summary>
    [TestMethod]
    public void ConvertClockTime_ForcedVariants_AreIndependentAndCanonicalized()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 1,
                Rules =
                [
                    new ClockTimeRule(new IntRange<int>("0-59"), 0, ClockHourForm.Cardinal,
                        "{hour}/{amount}", 0, ClockAmountDirection.After)
                    {
                        HourForcedVariants = ForcedVariantSet.Parse("genre=feminin"),
                        AmountForcedVariants = ForcedVariantSet.Parse("gender=masculin"),
                    },
                ],
            },
        };

        var converter = new NumberToStringConverter(options);
        Assert.AreEqual("vingt et une/vingt et un", converter.ConvertClockTime(new TimeOnly(21, 21), "gender=masculin"));
        ClockTimeRule snapshot = converter.ClockTime!.Rules[0];
        Assert.AreNotSame(options.ClockTime.Rules[0], snapshot);
        Assert.AreEqual("vingt et une/vingt et un", new NumberToStringConverter(new NumberToStringConverterOptions(converter))
            .ConvertClockTime(new TimeOnly(21, 21), "gender=masculin"));
    }

    /// <summary>Verifies a clock-rule forcing has higher precedence than a time-unit forcing.</summary>
    [TestMethod]
    public void ConvertClockTime_TimeUnitThenRuleForcing_UsesRuleValueOnConflict()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules =
                [
                    new ClockTimeRule(new IntRange<int>("0"), 0, ClockHourForm.TimeUnit, "{hour}")
                    {
                        HourForcedVariants = ForcedVariantSet.Create(("gender", "masculin")),
                    },
                ],
            },
        };

        Assert.AreEqual("un heure", new NumberToStringConverter(options).ConvertClockTime(new TimeOnly(1, 0)));
    }

    /// <summary>Verifies a contextual forcing overrides a legacy literal count-one time-unit form.</summary>
    [TestMethod]
    public void ConvertClockTime_TimeUnitContextualForcing_BypassesCount1Form()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("DE"))
        {
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules =
                [
                    new ClockTimeRule(new IntRange<int>("0"), 0, ClockHourForm.TimeUnit, "{hour}")
                    {
                        HourForcedVariants = ForcedVariantSet.Parse("genus=feminin,kasus=dativ"),
                    },
                ],
            },
        };

        var converter = new NumberToStringConverter(options);
        Assert.AreEqual("einer Stunde", converter.ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("eine Stunde", converter.Convert(new TimeOnly(1, 0)));
    }

    /// <summary>Verifies a forced ordinal variant is intentional even when the caller supplies none.</summary>
    [TestMethod]
    public void ConvertClockTime_OrdinalHourForcing_SelectsVariantWithoutCallerVariants()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules =
                [
                    new ClockTimeRule(new IntRange<int>("0"), 0, ClockHourForm.Ordinal, "{hour}")
                    {
                        HourForcedVariants = ForcedVariantSet.Create(("gender", "feminin")),
                    },
                ],
            },
        };

        Assert.AreEqual("première", new NumberToStringConverter(options).ConvertClockTime(new TimeOnly(1, 0)));
    }

    /// <summary>Verifies display-hour conditions select different rules at the same minute in a 12-hour cycle.</summary>
    [TestMethod]
    public void ConvertClockTime_DisplayHourRange_SelectsByProjectedHour()
    {
        ClockTimeRule[] rules =
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}"),
            new(new IntRange<int>("30"), 0, ClockHourForm.Cardinal, "one:{hour}") { DisplayHourRange = new IntRange<int>("1") },
            new(new IntRange<int>("30"), 0, ClockHourForm.Cardinal, "two:{hour}") { DisplayHourRange = new IntRange<int>("2") },
            new(new IntRange<int>("30"), 0, ClockHourForm.Cardinal, "other:{hour}") { DisplayHourRange = new IntRange<int>("3-12") },
        ];
        NumberToStringConverter converter = Create(30, rules, hourCycle: 12);

        Assert.AreEqual("other:twelve", converter.ConvertClockTime(new TimeOnly(0, 30)));
        Assert.AreEqual("one:one", converter.ConvertClockTime(new TimeOnly(1, 30)));
        Assert.AreEqual("two:two", converter.ConvertClockTime(new TimeOnly(2, 30)));
        Assert.AreEqual("one:one", converter.ConvertClockTime(new TimeOnly(13, 30)));
        Assert.AreEqual("other:eleven", converter.ConvertClockTime(new TimeOnly(23, 30)));
    }

    /// <summary>Verifies each candidate's own offset is used while evaluating its display-hour condition.</summary>
    [TestMethod]
    public void ConvertClockTime_DisplayHourRange_UsesCandidateOffset()
    {
        ClockTimeRule[] rules =
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}"),
            new(new IntRange<int>("30"), 0, ClockHourForm.Cardinal, "current {hour}") { DisplayHourRange = new IntRange<int>("1") },
            new(new IntRange<int>("30"), 1, ClockHourForm.Cardinal, "shifted {hour}") { DisplayHourRange = new IntRange<int>("1") },
            new(new IntRange<int>("30"), 0, ClockHourForm.Cardinal, "remaining {hour}") { DisplayHourRange = new IntRange<int>("2-11") },
        ];
        NumberToStringConverter converter = Create(30, rules, hourCycle: 12);

        Assert.AreEqual("shifted one", converter.ConvertClockTime(new TimeOnly(0, 30)));
        Assert.AreEqual("current one", converter.ConvertClockTime(new TimeOnly(1, 30)));
        Assert.AreEqual("remaining two", converter.ConvertClockTime(new TimeOnly(2, 30)));
    }

    /// <summary>Verifies display-hour conditions retain the full 0-through-23 projection in a 24-hour cycle.</summary>
    [TestMethod]
    public void ConvertClockTime_DisplayHourRange_UsesTwentyFourHourProjection()
    {
        ClockTimeRule[] rules =
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "edge {hour}")
            {
                DisplayHourRange = new IntRange<int>("0,1,12,13,23"),
            },
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "middle {hour}")
            {
                DisplayHourRange = new IntRange<int>("2-11,14-22"),
            },
        ];
        NumberToStringConverter converter = Create(60, rules, hourCycle: 24);

        Assert.AreEqual("edge midnight", converter.ConvertClockTime(new TimeOnly(0, 0)));
        Assert.AreEqual("edge one", converter.ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("edge noon", converter.ConvertClockTime(new TimeOnly(12, 0)));
        Assert.AreEqual("edge thirteen", converter.ConvertClockTime(new TimeOnly(13, 0)));
        Assert.AreEqual("edge twenty-three", converter.ConvertClockTime(new TimeOnly(23, 0)));
    }

    /// <summary>Verifies hour-conditioned gaps, overlaps, and out-of-cycle values fail during construction.</summary>
    [TestMethod]
    public void Constructor_DisplayHourRange_RejectsInvalidCoverage()
    {
        ClockTimeRule[] gap =
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}") { DisplayHourRange = new IntRange<int>("1-11") },
        ];
        ClockTimeRule[] overlap =
        [
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}") { DisplayHourRange = new IntRange<int>("1-6") },
            new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}") { DisplayHourRange = new IntRange<int>("6-12") },
        ];
        ArgumentException gapException = Assert.ThrowsExactly<ArgumentException>(() => Create(60, gap, hourCycle: 12));
        ArgumentException overlapException = Assert.ThrowsExactly<ArgumentException>(() => Create(60, overlap, hourCycle: 12));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(60,
            [new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}") { DisplayHourRange = new IntRange<int>("0-12") }], hourCycle: 12));
        StringAssert.Contains(gapException.Message, "source hour");
        StringAssert.Contains(gapException.Message, "minute 0");
        StringAssert.Contains(overlapException.Message, "source hour");
        StringAssert.Contains(overlapException.Message, "minute 0");
    }

    /// <summary>Verifies rule selection occurs before special-hour lexical replacement.</summary>
    [TestMethod]
    public void ConvertClockTime_DisplayHourRange_PrecedesSpecialHourReplacement()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            SpecialHours = [new(0, "midnight", WholeHour: true)],
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                HourCycle = 12,
                Rules =
                [
                    new ClockTimeRule(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "special {hour}")
                    {
                        DisplayHourRange = new IntRange<int>("12"),
                    },
                    new ClockTimeRule(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "ordinary {hour}")
                    {
                        DisplayHourRange = new IntRange<int>("1-11"),
                    },
                ],
            },
        };

        Assert.AreEqual("special midnight", new NumberToStringConverter(options).ConvertClockTime(new TimeOnly(0, 0)));
    }

    /// <summary>Verifies a plugin-only ordinal implementation advertises support and renders ordinal clock hours.</summary>
    [TestMethod]
    public void ConvertClockTime_PluginOnlyOrdinal_IsSupported()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            OrdinalSuffix = null,
            OrdinalPrefix = null,
            OrdinalExceptions = new Dictionary<long, string>(),
            OrdinalWordRules = new Dictionary<string, string>(),
            OrdinalVariants = [],
            LanguageSpecifics = new ClockOrdinalPlugin(),
            ClockTime = new ClockTimeFormatOptions
            {
                Step = 60,
                Rules = [new(new IntRange<int>("0"), 0, ClockHourForm.Ordinal, "hour {hour}")],
            },
        };
        var converter = new NumberToStringConverter(options);

        Assert.IsTrue(converter.SupportsOrdinals);
        Assert.AreEqual("ordinal-2", converter.ConvertOrdinal(2));
        Assert.AreEqual("hour ordinal-2", converter.ConvertClockTime(new TimeOnly(2, 0)));
    }

    /// <summary>Verifies a plugin-only converter fails closed when its plugin declines a value.</summary>
    [TestMethod]
    public void ConvertOrdinal_PluginOnlyDeclinesValue_ThrowsWithoutCardinalFallback()
    {
        NumberToStringConverter converter = CreatePluginOnlyOrdinalConverter(new PartialClockOrdinalPlugin());

        Assert.IsTrue(converter.SupportsOrdinals);
        Assert.AreEqual("plugin-first", converter.ConvertOrdinal(1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(2));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
    }

    /// <summary>Verifies a declining plugin delegates to a genuine declarative ordinal pipeline when configured.</summary>
    [TestMethod]
    public void ConvertOrdinal_PluginDeclinesValue_UsesDeclarativeFallback()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new PartialClockOrdinalPlugin(),
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("plugin-first", converter.ConvertOrdinal(1));
        Assert.AreEqual("second", converter.ConvertOrdinal(2));
    }

    /// <summary>Verifies null and unused clock forcings are rejected during construction.</summary>
    [TestMethod]
    public void Constructor_ClockForcedVariants_RejectsNullAndUnusedValues()
    {
        ClockTimeRule nullForcing = new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")
        {
            HourForcedVariants = null!,
        };
        ClockTimeRule unusedHour = new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "literal")
        {
            HourForcedVariants = ForcedVariantSet.Create(("gender", "masculin")),
        };
        ClockTimeRule unusedAmount = new(new IntRange<int>("0"), 0, ClockHourForm.Cardinal, "{hour}")
        {
            AmountForcedVariants = ForcedVariantSet.Create(("gender", "masculin")),
        };

        Assert.ThrowsExactly<ArgumentException>(() => Create(60, [nullForcing]));
        ArgumentException unusedHourException = Assert.ThrowsExactly<ArgumentException>(() => CreateFrench(60, [unusedHour]));
        ArgumentException unusedAmountException = Assert.ThrowsExactly<ArgumentException>(() => CreateFrench(60, [unusedAmount]));
        StringAssert.Contains(unusedHourException.Message, "does not use {hour}");
        StringAssert.Contains(unusedAmountException.Message, "does not use {amount}");
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

    /// <summary>Creates a converter with French grammatical dimensions and supplied clock rules.</summary>
    private static NumberToStringConverter CreateFrench(int step, IReadOnlyList<ClockTimeRule> rules)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR"))
        {
            ClockTime = new ClockTimeFormatOptions { Step = step, Rules = rules },
        };
        return new NumberToStringConverter(options);
    }

    /// <summary>Creates a synthetic converter with neither declarative nor plugin ordinal support.</summary>
    private static NumberToStringConverter CreateWithoutOrdinalSupport()
    {
        var options = CreateOptionsWithoutDeclarativeOrdinalSupport();
        options.LanguageSpecifics = new DefaultNumberToStringLanguageSpecifics();
        return new NumberToStringConverter(options);
    }

    /// <summary>Creates a synthetic plugin-only ordinal converter.</summary>
    private static NumberToStringConverter CreatePluginOnlyOrdinalConverter(PartialClockOrdinalPlugin plugin)
    {
        var options = CreateOptionsWithoutDeclarativeOrdinalSupport();
        options.LanguageSpecifics = plugin;
        return new NumberToStringConverter(options);
    }

    /// <summary>Creates options with all declarative ordinal mechanisms removed.</summary>
    private static NumberToStringConverterOptions CreateOptionsWithoutDeclarativeOrdinalSupport()
        => new(NumberToStringConverter.GetConverter("EN"))
        {
            OrdinalSuffix = null,
            OrdinalPrefix = null,
            OrdinalExceptions = new Dictionary<long, string>(),
            OrdinalWordRules = new Dictionary<string, string>(),
            OrdinalVariants = [],
            ClockTime = null,
        };

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

    /// <summary>Provides a synthetic plugin-only ordinal implementation for capability tests.</summary>
    private sealed class ClockOrdinalPlugin : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <summary>Returns the supplied text unchanged.</summary>
        public string FinalizeWriting(string language, string text) => text;

        /// <summary>Returns a deterministic synthetic ordinal for every integer.</summary>
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        {
            result = $"ordinal-{number}";
            return true;
        }
    }

    /// <summary>Provides an int-only ordinal plugin that deliberately handles only the value one.</summary>
    private sealed class PartialClockOrdinalPlugin : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <summary>Returns the supplied text unchanged.</summary>
        public string FinalizeWriting(string language, string text) => text;

        /// <summary>Handles one and declines every other value.</summary>
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        {
            result = number == 1 ? "plugin-first" : null;
            return number == 1;
        }
    }
}
