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
        AssertSingleFinalization(converter => converter.Convert(new BigInteger(2)), "two");
        AssertSingleFinalization(converter => converter.Convert(2.0m), "two");
        AssertSingleFinalization(converter => converter.Convert(2.25m), "two point twenty-five hundredths");
    }

    /// <summary>Verifies both fraction entry points use the same single phrase boundary.</summary>
    [TestMethod]
    public void Convert_FractionSurfaces_FinalizeCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new Number(1, 2)), "one over two");
        AssertSingleFinalization(converter => converter.ConvertFraction(1, 2), "one over two");
    }

    /// <summary>Verifies currencies with and without subunits are finalized only after assembly.</summary>
    [TestMethod]
    public void ConvertCurrency_FinalizesCompletePhraseOnce()
    {
        CurrencyDefinition currency = CreateCurrency();
        AssertSingleFinalization(converter => converter.ConvertCurrency(1.25m, currency), "one euro and twenty-five cents");
        AssertSingleFinalization(converter => converter.ConvertCurrency(1.00m, currency), "one euro");
    }

    /// <summary>Verifies year and temporal composites are finalized at their public boundary.</summary>
    [TestMethod]
    public void Convert_YearAndTime_FinalizeCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.ConvertYear(2024), "twenty twenty-four");
        AssertSingleFinalization(converter => converter.Convert(new TimeSpan(1, 2, 0)), "one hour two minutes");
        AssertSingleFinalization(converter => converter.Convert(new TimeOnly(1, 2)), "one hour two minutes");
    }

    /// <summary>Verifies a date containing an ordinal is finalized only after the date is assembled.</summary>
    [TestMethod]
    public void Convert_DateOnly_FinalizesCompletePhraseOnce()
    {
        AssertSingleFinalization(converter => converter.Convert(new DateOnly(2024, 8, 21)), "August twenty-first, twenty twenty-four");
    }

    /// <summary>Verifies a combined date and time is assembled before its only finalization call.</summary>
    [TestMethod]
    public void Convert_DateTime_FinalizesCompletePhraseOnce()
    {
        AssertSingleFinalization(
            converter => converter.Convert(new DateTime(2024, 8, 21, 1, 2, 0)),
            "August twenty-first, twenty twenty-four one hour two minutes");
    }

    /// <summary>Verifies units at the limit succeed even when subunits exceed it.</summary>
    [TestMethod]
    public void ConvertCurrency_UnitsAtMaxNumber_Succeeds()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.AreEqual("one euro and twenty-five cents", converter.ConvertCurrency(1.25m, CreateCurrency()));
    }

    /// <summary>Verifies whole-currency units above the cardinal limit are rejected.</summary>
    [TestMethod]
    public void ConvertCurrency_UnitsAboveMaxNumber_Throws()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => converter.ConvertCurrency(2.00m, CreateCurrency()));
    }

    /// <summary>Verifies the units limit is evaluated after rounded subunits carry into it.</summary>
    [TestMethod]
    public void ConvertCurrency_RoundingCarryAboveMaxNumber_Throws()
    {
        NumberToStringConverter converter = CreateConverterWithMaxNumber(1);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => converter.ConvertCurrency(1.999m, CreateCurrency()));
    }

    /// <summary>Verifies raw adjustment precedes fragment variants without premature finalization.</summary>
    [TestMethod]
    public void CompositeConversion_AdjustFunctionRunsBeforeVariantRules()
    {
        var finalizer = new RecordingFinalizer();
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            AdjustFunction = text => text.Replace("two", "adjusted", StringComparison.Ordinal),
            LanguageSpecifics = finalizer,
            VariantDimensions = [new NumberToStringConverter.VariantDimension("style", ["proof"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["style"] = "proof" },
                    [new NumberToStringConverter.ReplacementRule("adjusted", "variant", ReplacementScope.Anywhere)])
            ],
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.ConvertCurrency(2m, CreateCurrency(), "style=proof");

        Assert.AreEqual("<1:variant euros>", result);
        CollectionAssert.AreEqual(new[] { "variant euros" }, finalizer.Inputs);
    }

    /// <summary>Verifies end triggers retain their historical fragment scope in composites.</summary>
    [TestMethod]
    public void CompositeConversion_EndTriggerStillAppliesToNumericFragment()
    {
        var finalizer = new RecordingFinalizer();
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = finalizer,
            Triggers =
            [
                new NumberToStringConverter.TriggerRule(
                    NumberToStringConverter.TriggerAt.End,
                    null,
                    [new NumberToStringConverter.TriggerReplace("two", false, [], "triggered")])
            ],
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.ConvertCurrency(2m, CreateCurrency());

        Assert.AreEqual("<1:triggered euros>", result);
        CollectionAssert.AreEqual(new[] { "triggered euros" }, finalizer.Inputs);
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
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = finalizer,
            VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masculine", "feminine"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["gender"] = "feminine" },
                    [new NumberToStringConverter.ReplacementRule("two", "two-F", ReplacementScope.Anywhere)])
            ],
        };
        var converter = new NumberToStringConverter(options);
        var currency = new CurrencyDefinition
        {
            UnitSingular = "euro",
            UnitPlural = "euros",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            SubunitDigits = 2,
            Connector = "and",
            // Only the subunit forces gender=feminine; the unit uses the unforced (masculine) default.
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminine")),
        };

        string result = converter.ConvertCurrency(2.02m, currency);

        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { "two euros and two-F cents" }, finalizer.Inputs);
        Assert.AreEqual("<1:two euros and two-F cents>", result);
    }

    /// <summary>Verifies signs remain outside the once-finalized fraction and currency phrases.</summary>
    [TestMethod]
    public void Convert_NegativeComposite_AppliesSignAfterSingleFinalization()
    {
        AssertSingleFinalization(converter => converter.ConvertFraction(-1, 2), "one over two", "minus <1:one over two>");
        AssertSingleFinalization(converter => converter.ConvertCurrency(-1.25m, CreateCurrency()), "one euro and twenty-five cents", "minus <1:one euro and twenty-five cents>");
    }

    /// <summary>Runs one conversion with a fresh non-idempotent finalizer and verifies its boundary.</summary>
    /// <param name="convert">The public conversion to invoke.</param>
    /// <param name="expectedInput">The complete phrase expected before finalization.</param>
    private static void AssertSingleFinalization(
        Func<NumberToStringConverter, string> convert,
        string expectedInput,
        string? expectedResult = null)
    {
        var finalizer = new RecordingFinalizer();
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = finalizer,
        };
        var converter = new NumberToStringConverter(options);

        string result = convert(converter);

        Assert.AreEqual(1, finalizer.CallCount);
        CollectionAssert.AreEqual(new[] { expectedInput }, finalizer.Inputs);
        Assert.AreEqual(expectedResult ?? $"<1:{expectedInput}>", result);
    }

    /// <summary>Creates the deterministic currency definition shared by composite tests.</summary>
    /// <returns>A two-decimal euro definition.</returns>
    private static CurrencyDefinition CreateCurrency() => new()
    {
        UnitSingular = "euro",
        UnitPlural = "euros",
        SubunitSingular = "cent",
        SubunitPlural = "cents",
        SubunitDigits = 2,
        Connector = "and",
    };

    /// <summary>Creates an English converter with the requested top-level cardinal limit.</summary>
    /// <param name="maxNumber">The maximum whole cardinal value.</param>
    /// <returns>A converter configured with the limit.</returns>
    private static NumberToStringConverter CreateConverterWithMaxNumber(BigInteger maxNumber) =>
        new(new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = maxNumber,
        });

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
