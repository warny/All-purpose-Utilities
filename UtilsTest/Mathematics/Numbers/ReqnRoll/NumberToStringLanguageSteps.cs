using System.Globalization;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Utils.NumberToString;
using Utils.Numerics;

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

    /// <summary>Selects the converter used by the current scenario.</summary>
    [Given("I use the {string} number converter")]
    public void GivenIUseTheNumberConverter(string culture) => converter = NumberToStringConverter.GetConverter(culture);

    /// <summary>Configures opaque converter variants for the current scenario.</summary>
    [Given("I use the variants {string}")]
    public void GivenIUseTheVariants(string value) => variants = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Converts an invariant integer cardinal through the public API.</summary>
    [When("I convert the cardinal number {word}")]
    public void WhenIConvertTheCardinalNumber(string number) => result = Converter.Convert(BigInteger.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Converts an invariant integer ordinal through the public API.</summary>
    [When("I convert the ordinal number {word}")]
    public void WhenIConvertTheOrdinalNumber(string number) => result = Converter.ConvertOrdinal(BigInteger.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Converts an invariant decimal through the public API.</summary>
    [When("I convert the decimal number {word}")]
    public void WhenIConvertTheDecimalNumber(string number) => result = Converter.Convert(decimal.Parse(number, CultureInfo.InvariantCulture), variants);

    /// <summary>Converts an invariant fraction through the public API.</summary>
    [When(@"I convert the fraction (\d+)/(\d+)")]
    public void WhenIConvertTheFraction(int numerator, int denominator) => result = Converter.Convert(new Number(numerator, denominator), variants);

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

    /// <summary>Verifies the exact localized result.</summary>
    [Then("the result is {string}")]
    public void ThenTheResultIs(string expected) => Assert.AreEqual(expected, result);

    /// <summary>Verifies observable cardinal-wording equivalence between two converter registrations.</summary>
    [Then("the {string} and {string} converters produce the same cardinal wording for {word}")]
    public void ThenConvertersProduceTheSameCardinalWording(string firstCulture, string secondCulture, string number)
    {
        BigInteger value = BigInteger.Parse(number, CultureInfo.InvariantCulture);
        Assert.AreEqual(
            NumberToStringConverter.GetConverter(firstCulture).Convert(value, variants),
            NumberToStringConverter.GetConverter(secondCulture).Convert(value, variants));
    }

    private INumberToStringConverter Converter => converter ?? throw new InvalidOperationException("A converter must be selected before conversion.");
}
