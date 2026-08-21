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
        var currency = new CurrencyDefinition
        {
            UnitSingular = "euro",
            UnitPlural = "euros",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            SubunitDigits = 2,
            Connector = "and",
        };
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

    /// <summary>Runs one conversion with a fresh non-idempotent finalizer and verifies its boundary.</summary>
    /// <param name="convert">The public conversion to invoke.</param>
    /// <param name="expectedInput">The complete phrase expected before finalization.</param>
    private static void AssertSingleFinalization(Func<NumberToStringConverter, string> convert, string expectedInput)
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
        Assert.AreEqual($"<1:{expectedInput}>", result);
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
