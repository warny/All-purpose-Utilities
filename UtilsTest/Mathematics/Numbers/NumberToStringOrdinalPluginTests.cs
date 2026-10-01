using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of the productive <see cref="IOrdinalLanguageSpecifics"/> plugins added for the
/// NTS-08 languages: range limits, irregular forms, compounds, round scales, and the fail-closed
/// contract outside the implemented range. Linguistic coverage of ordinary values lives in the
/// language <c>.feature</c> files.
/// </summary>
[TestClass]
public class NumberToStringOrdinalPluginTests
{
    /// <summary>Ensures the Scandinavian plugins cover the whole <see cref="int"/> range with productive compounds.</summary>
    [TestMethod]
    [DataRow("DA", "to milliarder hundrede og syvogfyrre millioner fire hundrede og treogfirs tusind seks hundrede og syvogfyrretyvende")]
    [DataRow("NO", "to milliarder hundre og førtisju millioner fire hundre og åttitre tusen seks hundre og førtisjuende")]
    [DataRow("SV", "två miljarder hundrafyrtiosju miljoner fyrahundraåttiotretusensexhundrafyrtiosjunde")]
    public void ScandinavianOrdinals_IntMaxValue_IsProductive(string culture, string expected)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.IsTrue(converter.SupportsOrdinals);
        Assert.AreEqual(expected, converter.ConvertOrdinal(int.MaxValue));
    }

    /// <summary>Ensures round scale values ordinalize only their last scale word.</summary>
    [TestMethod]
    [DataRow("DA", 21_000, "enogtyve tusinde")]
    [DataRow("DA", 2_000_000, "to millionte")]
    [DataRow("DA", 1_000_000_000, "milliardte")]
    [DataRow("DA", 1_000_001, "en million og første")]
    [DataRow("NO", 21_000, "tjueen tusende")]
    [DataRow("NO", 1_000_000_000, "milliardte")]
    [DataRow("SV", 21_000, "tjugoetttusende")]
    [DataRow("SV", 1_100, "tusenhundrade")]
    [DataRow("SV", 1_000_000_000, "miljardte")]
    public void ScandinavianOrdinals_RoundScales_OrdinalizeLastScaleWord(string culture, int number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(culture).ConvertOrdinal(number));

    /// <summary>
    /// Ensures values outside the implemented range fail closed instead of returning a cardinal:
    /// the Scandinavian plugins only implement the <see cref="int"/> range and have no declarative
    /// fallback, and zero has no ordinal.
    /// </summary>
    [TestMethod]
    [DataRow("DA")]
    [DataRow("NO")]
    [DataRow("SV")]
    public void ScandinavianOrdinals_OutsideImplementedRange_FailClosed(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
    }

    /// <summary>
    /// Ensures an ordinal is never the unchanged cardinal for a broad sample. 100 is excluded: its
    /// Danish ordinal is genuinely identical to the cardinal (<c>hundrede</c>, Retskrivningsordbogen).
    /// </summary>
    [TestMethod]
    [DataRow("DA")]
    [DataRow("NO")]
    [DataRow("SV")]
    public void ScandinavianOrdinals_NeverReturnTheCardinal(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        foreach (int value in new[] { 1, 2, 3, 7, 11, 19, 20, 21, 45, 99, 101, 999, 1000, 1001, 21000, 1_000_000 })
            Assert.AreNotEqual(converter.Convert(value), converter.ConvertOrdinal(value), $"{culture} {value}");
    }
}
