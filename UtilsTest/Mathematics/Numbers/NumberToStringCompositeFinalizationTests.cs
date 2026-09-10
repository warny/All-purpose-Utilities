using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Verifies that every composite public conversion has one final phrase boundary.</summary>
[TestClass]
public class NumberToStringCompositeFinalizationTests
{
    /// <summary>Verifies cardinal and decimal results are finalized once after complete assembly.</summary>
    [TestMethod]
    public void Convert_CardinalAndDecimals_FinalizeCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new BigInteger(2)));
        AssertSingleFinalization(converter => converter.Convert(2.0m));
        AssertSingleFinalization(converter => converter.Convert(2.25m));
    }

    /// <summary>Verifies both fraction entry points use the same single phrase boundary.</summary>
    [TestMethod]
    public void Convert_FractionSurfaces_FinalizeCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new Number(1, 2)));
        AssertSingleFinalization(converter => converter.ConvertFraction(1, 2));
    }

    /// <summary>Verifies currencies with and without subunits are finalized only after assembly.</summary>
    [TestMethod]
    public void ConvertCurrency_FinalizesCompletePhraseOnce()
    {
        CurrencyDefinition currency = CreateCurrency();
        AssertSingleFinalization(converter => converter.ConvertCurrency(1.25m, currency));
        AssertSingleFinalization(converter => converter.ConvertCurrency(1.00m, currency));
    }

    /// <summary>Verifies year and temporal composites are finalized at their public boundary.</summary>
    [TestMethod]
    public void Convert_YearAndTime_FinalizeCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.ConvertYear(2024));
        AssertSingleFinalization(converter => converter.Convert(new TimeSpan(1, 2, 0)));
        AssertSingleFinalization(converter => converter.Convert(new TimeOnly(1, 2)));
    }

    /// <summary>Verifies a date containing an ordinal is finalized only after the date is assembled.</summary>
    [TestMethod]
    public void Convert_DateOnly_FinalizesCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new DateOnly(2024, 8, 21)));
    }

    /// <summary>Verifies a combined date and time is assembled before its only finalization call.</summary>
    [TestMethod]
    public void Convert_DateTime_FinalizesCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new DateTime(2024, 8, 21, 1, 2, 0)));
    }

    /// <summary>Verifies units at the limit succeed even when subunits exceed it.</summary>
    [TestMethod]
    public void ConvertCurrency_UnitsAtMaxNumber_Succeeds()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.IsFalse(string.IsNullOrWhiteSpace(converter.ConvertCurrency(1.25m, CreateCurrency())));
    }

    /// <summary>Verifies whole-currency units above the cardinal limit are rejected.</summary>
    [TestMethod]
    public void ConvertCurrency_UnitsAboveMaxNumber_Throws()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertCurrency(2.00m, CreateCurrency()));
    }

    /// <summary>Verifies the units limit is evaluated after rounded subunits carry into it.</summary>
    [TestMethod]
    public void ConvertCurrency_RoundingCarryAboveMaxNumber_Throws()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.ConvertCurrency(1.999m, CreateCurrency()));
    }

    /// <summary>Verifies raw adjustment precedes fragment variants without premature finalization.</summary>
    [TestMethod]
    public void CompositeConversion_AdjustFunctionRunsBeforeVariantRules()
    {
        var finalizer = new RecordingFinalizer();
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string baseToken = source.Convert(2);
        var options = new NumberToStringConverterOptions(source)
        {
            AdjustFunction = text => text.Replace(baseToken, "ADJUSTED", StringComparison.Ordinal),
            LanguageSpecifics = finalizer,
            VariantDimensions = [new NumberToStringConverter.VariantDimension("style", ["proof"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["style"] = "proof" },
                    [new NumberToStringConverter.ReplacementRule("ADJUSTED", "VARIANT", ReplacementScope.Anywhere)])
            ],
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.ConvertCurrency(2m, CreateCurrency(), "style=proof");

        Assert.AreEqual("<1:VARIANT UNITS>", result);
        CollectionAssert.AreEqual(new[] { "VARIANT UNITS" }, finalizer.Inputs);
    }

    /// <summary>Verifies end triggers retain their historical fragment scope in composites.</summary>
    [TestMethod]
    public void CompositeConversion_EndTriggerStillAppliesToNumericFragment()
    {
        var finalizer = new RecordingFinalizer();
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string baseToken = source.Convert(2);
        var options = new NumberToStringConverterOptions(source)
        {
            LanguageSpecifics = finalizer,
            Triggers =
            [
                new NumberToStringConverter.TriggerRule(
                    NumberToStringConverter.TriggerAt.End,
                    null,
                    [new NumberToStringConverter.TriggerReplace(baseToken, false, [], "TRIGGERED")])
            ],
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.ConvertCurrency(2m, CreateCurrency());

        Assert.AreEqual("<1:TRIGGERED UNITS>", result);
        CollectionAssert.AreEqual(new[] { "TRIGGERED UNITS" }, finalizer.Inputs);
    }

    /// <summary>
    /// Verifies a currency phrase where the unit and subunit force different local variant queries
    /// (NTS-04 ForcedVariants) is still finalized exactly once, with both locally-queried fragments
    /// present in the single text handed to the finalizer.
    /// </summary>
    [TestMethod]
    public void ConvertCurrency_UnitAndSubunit_DifferentForcedVariants_FinalizeCompletePhraseOnce()
    {
        var finalizer = new RecordingFinalizer();
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string baseToken = source.Convert(2);
        var options = new NumberToStringConverterOptions(source)
        {
            LanguageSpecifics = finalizer,
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule(baseToken, "ALT", ReplacementScope.Anywhere)])
            ],
        };
        var converter = new NumberToStringConverter(options);
        var currency = new CurrencyDefinition
        {
            UnitSingular = "UNIT",
            UnitPlural = "UNITS",
            SubunitSingular = "SUBUNIT",
            SubunitPlural = "SUBUNITS",
            SubunitDigits = 2,
            Connector = "CONNECTOR",
            // Only the subunit forces gender=feminine; the unit uses the unforced (masculine) default.
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminine")),
        };

        string result = converter.ConvertCurrency(2.02m, currency);

        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { $"{baseToken} UNITS CONNECTOR ALT SUBUNITS" }, finalizer.Inputs);
        Assert.AreEqual($"<1:{baseToken} UNITS CONNECTOR ALT SUBUNITS>", result);
    }

    /// <summary>
    /// Verifies a time phrase rendered through a custom <see cref="ILexicalFormSelector"/> (NTS-05)
    /// is still finalized exactly once, proving lexical form selection composes with the numeral
    /// pipeline without adding a per-fragment finalization call.
    /// </summary>
    [TestMethod]
    public void Convert_TimeSpan_WithCustomLexicalFormSelector_FinalizesCompletePhraseOnce()
    {
        var finalizer = new RecordingFinalizer();
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string baseToken = source.Convert(2);
        var options = new NumberToStringConverterOptions(source)
        {
            LanguageSpecifics = finalizer,
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("UNIT_ONE", "UNIT_MANY", null),
            },
            TimeUnitForms = new Dictionary<string, LexicalFormSet>
            {
                ["hour"] = LexicalFormSet.Create(("custom", "CUSTOM_UNIT")),
            },
            TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>
            {
                ["hour"] = new AlwaysCustomFormSelector(),
            },
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.Convert(new TimeSpan(2, 0, 0));

        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { $"{baseToken} CUSTOM_UNIT" }, finalizer.Inputs);
        Assert.AreEqual($"<1:{baseToken} CUSTOM_UNIT>", result);
    }

    /// <summary>Verifies signs remain outside the once-finalized fraction and currency phrases.</summary>
    [TestMethod]
    public void Convert_NegativeComposite_AppliesSignAfterSingleFinalization()
    {
        AssertNegativeSingleFinalization(
            converter => converter.ConvertFraction(-1, 2),
            converter => converter.ConvertFraction(1, 2));
        AssertNegativeSingleFinalization(
            converter => converter.ConvertCurrency(-1.25m, CreateCurrency()),
            converter => converter.ConvertCurrency(1.25m, CreateCurrency()));
    }

    /// <summary>Runs one conversion with a fresh non-idempotent finalizer and verifies its boundary.</summary>
    /// <param name="convert">The public conversion to invoke.</param>
    private static void AssertSingleFinalization(
        Func<NumberToStringConverter, string> convert)
    {
        NumberToStringConverter baseline = NumberToStringConverter.GetConverter("EN");
        string expectedInput = convert(baseline);
        var finalizer = new RecordingFinalizer();
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = finalizer,
        };
        var converter = new NumberToStringConverter(options);

        string result = convert(converter);

        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { expectedInput }, finalizer.Inputs);
        Assert.AreEqual($"<1:{expectedInput}>", result);
    }

    /// <summary>Verifies that a sign remains outside a phrase finalized exactly once.</summary>
    /// <param name="convertNegative">The negative composite conversion.</param>
    /// <param name="convertMagnitude">The corresponding unsigned conversion.</param>
    private static void AssertNegativeSingleFinalization(
        Func<NumberToStringConverter, string> convertNegative,
        Func<NumberToStringConverter, string> convertMagnitude)
    {
        NumberToStringConverter baseline = NumberToStringConverter.GetConverter("EN");
        string magnitude = convertMagnitude(baseline);
        var finalizer = new RecordingFinalizer();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(baseline)
        {
            LanguageSpecifics = finalizer,
            Minus = "SIGN *",
        });

        Assert.AreEqual($"SIGN <1:{magnitude}>", convertNegative(converter));
        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { magnitude }, finalizer.Inputs);
    }

    /// <summary>Creates the deterministic currency definition shared by composite tests.</summary>
    /// <returns>A two-decimal euro definition.</returns>
    private static CurrencyDefinition CreateCurrency() => new()
    {
        UnitSingular = "UNIT",
        UnitPlural = "UNITS",
        SubunitSingular = "SUBUNIT",
        SubunitPlural = "SUBUNITS",
        SubunitDigits = 2,
        Connector = "CONNECTOR",
    };

    /// <summary>Creates an English converter with the requested top-level cardinal limit.</summary>
    /// <param name="maxNumber">The maximum whole cardinal value.</param>
    /// <returns>A converter configured with the limit.</returns>
    private static NumberToStringConverter CreateConverterWithMaxNumber(BigInteger maxNumber) =>
        new(new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = maxNumber,
        });

    /// <summary>Always selects the "custom" form key, regardless of count/context.</summary>
    private sealed class AlwaysCustomFormSelector : ILexicalFormSelector
    {
        public string SelectForm(LexicalFormContext context) => "custom";
    }

    /// <summary>Records and visibly marks every language-finalization invocation.</summary>
    private sealed class RecordingFinalizer : INumberToStringLanguageSpecifics
    {
        /// <summary>Gets the number of finalization calls.</summary>
        public int CallCount { get; private set; }

        /// <summary>Gets the unfinalized inputs observed by the finalizer.</summary>
        public List<string> Inputs { get; } = [];

        /// <summary>Records and wraps a phrase so premature or repeated finalization remains observable.</summary>
        /// <param name="languageIdentifier">The active language identifier.</param>
        /// <param name="text">The phrase presented for finalization.</param>
        /// <returns>The phrase wrapped with its invocation number.</returns>
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            CallCount++;
            Inputs.Add(text);
            return $"<{CallCount}:{text}>";
        }
    }
}
