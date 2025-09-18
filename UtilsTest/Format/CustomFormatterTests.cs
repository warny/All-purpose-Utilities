using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Format;

namespace UtilsTest.Format;

[TestClass]
public class CustomFormatterTests
{
    [TestMethod]
    public void AddFormatterWithoutProviderFormatsValue()
    {
        var formatter = new CustomFormatter(CultureInfo.InvariantCulture);
        formatter.AddFormatter<DateTime>("ymd", dt => dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        var date = new DateTime(2024, 5, 17);
        var result = formatter.Format("ymd", date, formatter);

        Assert.AreEqual("20240517", result);
    }

    [TestMethod]
    public void AddFormatterWithProviderUsesGivenCulture()
    {
        var formatter = new CustomFormatter(CultureInfo.InvariantCulture);
        formatter.AddFormatter<double>("num", (value, provider) => value.ToString("N2", provider));

        var provider = new CultureInfo("fr-FR");
        var result = formatter.Format("num", 1234.5, provider);
        var expected = 1234.5.ToString("N2", provider);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FormatFallsBackToBaseFormatterWhenUnregistered()
    {
        var formatter = new CustomFormatter(CultureInfo.InvariantCulture);

        var result = formatter.Format("G", 42, null);

        Assert.AreEqual("42", result);
    }
}
