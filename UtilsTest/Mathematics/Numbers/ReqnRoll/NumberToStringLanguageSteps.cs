using System.Globalization;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers.ReqnRoll;

/// <summary>
/// Provides reusable steps for executable NumberToString language specifications.
/// </summary>
[Binding]
public sealed class NumberToStringLanguageSteps
{
    private INumberToStringConverter? converter;
    private string[] variants = [];
    private string? result;
    private CurrencyDefinition? currency;
    private Exception? exception;

    /// <summary>Selects the converter used by the current scenario.</summary>
    [Given("I use the {string} number converter")]
    public void GivenIUseTheNumberConverter(string culture) => converter = NumberToStringConverter.GetConverter(culture);

    /// <summary>Configures opaque converter variants for the current scenario.</summary>
    [Given("I use the variants {string}")]
    public void GivenIUseTheVariants(string value) => variants = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Configures a caller-supplied currency definition for the current scenario.</summary>
    [Given("I use this currency definition")]
    public void GivenIUseThisCurrencyDefinition(Table table)
    {
        IReadOnlyDictionary<string, string> values = table.Rows.ToDictionary(row => row["property"], row => row["value"]);
        currency = new CurrencyDefinition
        {
            UnitSingular = values["unit singular"],
            UnitPlural = values["unit plural"],
            SubunitSingular = values["subunit singular"],
            SubunitPlural = values["subunit plural"],
            Connector = values["connector"]
        };
    }

    /// <summary>Converts an invariant integer cardinal through the public API.</summary>
    [When("I convert the cardinal number {word}")]
    public void WhenIConvertTheCardinalNumber(string number) => result = Converter.Convert(BigInteger.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Attempts a cardinal conversion and captures its public validation failure.</summary>
    [When("I attempt to convert the cardinal number {word}")]
    public void WhenIAttemptToConvertTheCardinalNumber(string number)
    {
        try
        {
            result = Converter.Convert(BigInteger.Parse(number, CultureInfo.InvariantCulture), variants);
        }
        catch (Exception caught)
        {
            exception = caught;
        }
    }

    /// <summary>Converts an invariant integer ordinal through the public API.</summary>
    [When("I convert the ordinal number {word}")]
    public void WhenIConvertTheOrdinalNumber(string number) => result = Converter.ConvertOrdinal(BigInteger.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Converts an invariant decimal through the public API.</summary>
    [When("I convert the decimal number {word}")]
    public void WhenIConvertTheDecimalNumber(string number) => result = Converter.Convert(decimal.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Converts an invariant currency amount through the public API.</summary>
    [When("I convert the currency amount {word}")]
    public void WhenIConvertTheCurrencyAmount(string number) => result = Converter.ConvertCurrency(
        decimal.Parse(number, CultureInfo.InvariantCulture),
        currency ?? throw new InvalidOperationException("A currency definition must be selected before conversion."),
        variants);

    /// <summary>Converts an invariant fraction through the public API.</summary>
    [When(@"I convert the fraction (\d+)/(\d+)")]
    public void WhenIConvertTheFraction(int numerator, int denominator) => result = Converter.ConvertFraction(numerator, denominator, variants);

    /// <summary>Converts an invariant duration through the public API.</summary>
    [When("I convert the duration {string}")]
    public void WhenIConvertTheDuration(string value) => result = Converter.Convert(
        TimeSpan.ParseExact(value, [@"hh\:mm\:ss", @"d\.hh\:mm\:ss"], CultureInfo.InvariantCulture),
        variants);

    /// <summary>Converts an invariant time of day through the public API.</summary>
    [When("I convert the time {string}")]
    public void WhenIConvertTheTime(string value) => result = Converter.Convert(
        TimeOnly.ParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture),
        variants);

    /// <summary>Converts an invariant calendar date through the public API.</summary>
    [When("I convert the date {string}")]
    public void WhenIConvertTheDate(string value) => result = Converter.Convert(
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        variants);

    /// <summary>Converts an invariant date and time through the public API.</summary>
    [When("I convert the date and time {string}")]
    public void WhenIConvertTheDateAndTime(string value) => result = Converter.Convert(
        DateTime.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        variants);

    /// <summary>Converts an invariant year through the public API.</summary>
    [When("I convert the year {int}")]
    public void WhenIConvertTheYear(int value) => result = Converter.ConvertYear(value, variants);

    /// <summary>Verifies the exact localized result.</summary>
    [Then("the result is {string}")]
    public void ThenTheResultIs(string expected) => Assert.AreEqual(expected, result);

    /// <summary>Verifies that the selected converter advertises ordinal conversion.</summary>
    [Then("the converter supports ordinal conversion")]
    public void ThenTheConverterSupportsOrdinalConversion() => Assert.IsTrue(Converter.SupportsOrdinals);

    /// <summary>Verifies that the selected converter does not advertise ordinal conversion.</summary>
    [Then("the converter does not support ordinal conversion")]
    public void ThenTheConverterDoesNotSupportOrdinalConversion() => Assert.IsFalse(Converter.SupportsOrdinals);

    /// <summary>Verifies that the selected converter advertises time conversion.</summary>
    [Then("the converter supports time conversion")]
    public void ThenTheConverterSupportsTimeConversion() => Assert.IsTrue(Converter.SupportsTimeConversion);

    /// <summary>Verifies that the selected converter does not advertise time conversion.</summary>
    [Then("the converter does not support time conversion")]
    public void ThenTheConverterDoesNotSupportTimeConversion() => Assert.IsFalse(Converter.SupportsTimeConversion);

    /// <summary>Verifies that the selected converter advertises date conversion.</summary>
    [Then("the converter supports date conversion")]
    public void ThenTheConverterSupportsDateConversion() => Assert.IsTrue(Converter.SupportsDateConversion);

    /// <summary>Verifies that the selected converter does not advertise date conversion.</summary>
    [Then("the converter does not support date conversion")]
    public void ThenTheConverterDoesNotSupportDateConversion() => Assert.IsFalse(Converter.SupportsDateConversion);

    /// <summary>Verifies that conversion was rejected because the value exceeded the converter range.</summary>
    [Then("conversion is rejected because the value is out of range")]
    public void ThenConversionIsRejectedBecauseTheValueIsOutOfRange() => Assert.IsInstanceOfType<ArgumentOutOfRangeException>(exception);

    /// <summary>Verifies observable cardinal-wording equivalence between two converter registrations.</summary>
    [Then("the {string} and {string} converters produce the same cardinal wording for {word}")]
    public void ThenConvertersProduceTheSameCardinalWording(string firstCulture, string secondCulture, string number)
    {
        BigInteger value = BigInteger.Parse(number, CultureInfo.InvariantCulture);
        Assert.AreEqual(
            NumberToStringConverter.GetConverter(firstCulture).Convert(value, variants),
            NumberToStringConverter.GetConverter(secondCulture).Convert(value, variants));
    }

    /// <summary>Verifies observable decimal-wording equivalence between two converter registrations.</summary>
    [Then("the {string} and {string} converters produce the same decimal wording for {word}")]
    public void ThenConvertersProduceTheSameDecimalWording(string firstCulture, string secondCulture, string number)
    {
        decimal value = decimal.Parse(number, CultureInfo.InvariantCulture);
        Assert.AreEqual(
            NumberToStringConverter.GetConverter(firstCulture).Convert(value, variants),
            NumberToStringConverter.GetConverter(secondCulture).Convert(value, variants));
    }

    /// <summary>Gets the converter selected for the current scenario.</summary>
    private INumberToStringConverter Converter => converter ?? throw new InvalidOperationException("A converter must be selected before conversion.");
}
