using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers
{
    /// <summary>
    /// Validates that fractional conversions leverage the localized connector configured for each language.
    /// </summary>
    [TestClass]
    public class NumberToStringConverterFractionConnectorTests
    {
        /// <summary>
        /// Provides the expected fractional wording for supported cultures.
        /// </summary>
        /// <returns>An enumerable of test cases pairing a culture with its expected 3/2 rendering.</returns>
        public static IEnumerable<object[]> FractionExpectations()
        {
            yield return new object[] { "AR", "ثلاثة على اثنان" };
            yield return new object[] { "DE", "drei durch zwei" };
            yield return new object[] { "EE", "eto kple eve" };
            yield return new object[] { "EL", "τρία διά δύο" };
            yield return new object[] { "EN", "three over two" };
            yield return new object[] { "ES", "tres sobre dos" };
            yield return new object[] { "FI", "kolme yli kaksi" };
            yield return new object[] { "FR-fr", "trois sur deux" };
            yield return new object[] { "HE", "שלוש על שתיים" };
            yield return new object[] { "HI", "तीन बटे दो" };
            yield return new object[] { "IT", "tre su due" };
            yield return new object[] { "JA", "三 割る 二" };
            yield return new object[] { "KO", "삼 나누기 이" };
            yield return new object[] { "NL", "drie op twee" };
            yield return new object[] { "PL", "trzy przez dwa" };
            yield return new object[] { "PT", "três sobre dois" };
            yield return new object[] { "RU", "три на два" };
            yield return new object[] { "WO", "ñett ci ñaar" };
            yield return new object[] { "ZH", "三 除以 二" };
            yield return new object[] { "ZU", "kuthathu ngaphezu kubili" };
        }

        /// <summary>
        /// Ensures that the configured fraction connector is used when converting rational numbers.
        /// </summary>
        /// <param name="culture">The target culture to retrieve the converter for.</param>
        /// <param name="expected">The expected textual representation for 3/2.</param>
        [DataTestMethod]
        [DynamicData(nameof(FractionExpectations), DynamicDataSourceType.Method)]
        public void FractionsUseLocalizedConnector(string culture, string expected)
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            Assert.AreEqual(expected, converter.Convert(new Number(3, 2)));
        }

        /// <summary>
        /// Ensures that the localized connector also appears when both numerator and denominator
        /// require full wording instead of shortcut fraction names.
        /// </summary>
        /// <param name="culture">The culture identifier used to resolve the converter.</param>
        /// <param name="_">Unused parameter required by the <see cref="DynamicDataAttribute"/> signature.</param>
        [DataTestMethod]
        [DynamicData(nameof(FractionExpectations), DynamicDataSourceType.Method)]
        public void FractionsUseLocalizedConnectorForComplexValues(string culture, string _)
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            const int numerator = 117;
            const int denominator = 1013;

            string numeratorText = converter.Convert(numerator).Replace("-", " ");
            string denominatorText = converter.Convert(denominator).Replace("-", " ");

            string combined = string.Concat(
                numeratorText,
                converter.Separator,
                converter.FractionSeparator,
                converter.Separator,
                denominatorText).Trim();

            string expected = converter.AdjustFunction(combined);

            Assert.AreEqual(expected, converter.Convert(new Number(numerator, denominator)));
        }
    }
}
