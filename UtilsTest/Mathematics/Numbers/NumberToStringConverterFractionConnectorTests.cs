using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;
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
        /// Provides one culture identifier per supported language family.
        /// </summary>
        /// <returns>An enumerable of culture identifiers used to validate conversion coverage.</returns>
        public static IEnumerable<object[]> LanguageCoverageCultures()
        {
            yield return new object[] { "AR" };
            yield return new object[] { "CA" };
            yield return new object[] { "DE" };
            yield return new object[] { "EE" };
            yield return new object[] { "EL" };
            yield return new object[] { "EN" };
            yield return new object[] { "ES" };
            yield return new object[] { "EU" };
            yield return new object[] { "FI" };
            yield return new object[] { "FR-fr" };
            yield return new object[] { "GL" };
            yield return new object[] { "HE" };
            yield return new object[] { "HI" };
            yield return new object[] { "IT" };
            yield return new object[] { "JA" };
            yield return new object[] { "KO" };
            yield return new object[] { "NL" };
            yield return new object[] { "PL" };
            yield return new object[] { "PT" };
            yield return new object[] { "RU" };
            yield return new object[] { "WO" };
            yield return new object[] { "ZH" };
            yield return new object[] { "ZU" };
        }

        /// <summary>
        /// Ensures that the localized connector also appears when both numerator and denominator
        /// require full wording instead of shortcut fraction names.
        /// </summary>
        /// <param name="culture">The culture identifier used to resolve the converter.</param>
        [DataTestMethod]
        [DynamicData(nameof(LanguageCoverageCultures), DynamicDataSourceType.Method)]
        public void FractionsUseLocalizedConnectorForComplexValues(string culture)
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            const int numerator = 117;
            const int denominator = 1013;

            string numeratorText = converter.Convert(numerator);
            string denominatorText = converter.Convert(denominator);

            string combined = string.Concat(
                numeratorText,
                converter.Separator,
                converter.FractionSeparator,
                converter.Separator,
                denominatorText).Trim();

            string expected = converter.AdjustFunction(combined);

            Assert.AreEqual(expected, converter.Convert(new Number(numerator, denominator)));
        }

        /// <summary>
        /// Ensures that each supported language can render the value 21001.
        /// </summary>
        /// <param name="culture">The target culture identifier.</param>
        [DataTestMethod]
        [DynamicData(nameof(LanguageCoverageCultures), DynamicDataSourceType.Method)]
        public void AllSupportedLanguagesCanWrite21001(string culture)
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            string value = converter.Convert(21001);

            Assert.IsFalse(string.IsNullOrWhiteSpace(value));
        }
    }
}
