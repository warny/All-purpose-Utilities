using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests temporal capability flags, unsupported operations, and regional converter inheritance.
/// </summary>
[TestClass]
public class NumberToStringConverterTemporalCapabilityTests
{
    /// <summary>Verifies that Hungarian advertises ordinal conversion support.</summary>
    [TestMethod]
    public void SupportsOrdinals_HU_True()
    {
        Assert.IsTrue(NumberToStringConverter.GetConverter("HU").SupportsOrdinals);
    }

    // ─── SupportsTimeConversion / SupportsDateConversion feature flags ──────

    /// <summary>Verifies that the English, French, and German converters advertise time conversion support.</summary>
    [TestMethod]
    public void SupportsTimeConversion_EN_FR_DE_True()
    {
        Assert.IsTrue(NumberToStringConverter.GetConverter("EN").SupportsTimeConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("FR").SupportsTimeConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("DE").SupportsTimeConversion);
    }

    /// <summary>Verifies that the Croatian and Hungarian converters do not advertise time conversion support.</summary>
    [TestMethod]
    public void SupportsTimeConversion_HR_HU_False()
    {
        Assert.IsFalse(NumberToStringConverter.GetConverter("HR").SupportsTimeConversion);
        Assert.IsFalse(NumberToStringConverter.GetConverter("HU").SupportsTimeConversion);
    }

    /// <summary>Verifies that the English, French, and German converters advertise date conversion support.</summary>
    [TestMethod]
    public void SupportsDateConversion_EN_FR_DE_True()
    {
        Assert.IsTrue(NumberToStringConverter.GetConverter("EN").SupportsDateConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("FR").SupportsDateConversion);
        Assert.IsTrue(NumberToStringConverter.GetConverter("DE").SupportsDateConversion);
    }

    /// <summary>Verifies that converting time with an unsupported converter throws.</summary>
    [TestMethod]
    public void NotSupported_TimeConversion_Throws()
    {
        var hr = NumberToStringConverter.GetConverter("HR");
        Assert.IsFalse(hr.SupportsTimeConversion);
        Assert.ThrowsExactly<NotSupportedException>(() => hr.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── EN-GB (British English, derived from EN via baseOn) ──────────────────

    /// <summary>Verifies that British English inherits cardinal conversion behavior from English.</summary>
    [TestMethod]
    public void Convert_EN_GB_BasicNumbers_SameAsEN()
    {
        // Numbers must be identical to EN since EN-GB inherits all number rules.
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.AreEqual(en.Convert(0),        gb.Convert(0));
        Assert.AreEqual(en.Convert(1),        gb.Convert(1));
        Assert.AreEqual(en.Convert(42),       gb.Convert(42));
        Assert.AreEqual(en.Convert(1_000),    gb.Convert(1_000));
        Assert.AreEqual(en.Convert(1_000_000), gb.Convert(1_000_000));
    }

    /// <summary>Verifies that British English inherits ordinal capability and conversion behavior from English.</summary>
    [TestMethod]
    public void Convert_EN_GB_Ordinals_SameAsEN()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.IsTrue(gb.SupportsOrdinals);
        Assert.AreEqual(en.ConvertOrdinal(1),  gb.ConvertOrdinal(1));
        Assert.AreEqual(en.ConvertOrdinal(2),  gb.ConvertOrdinal(2));
        Assert.AreEqual(en.ConvertOrdinal(21), gb.ConvertOrdinal(21));
    }

    /// <summary>Verifies that British English inherits short-scale names from English.</summary>
    [TestMethod]
    public void Convert_EN_GB_ScaleNames_InheritedFromScaleShort()
    {
        // EN-GB inherits EN → SCALE-SHORT chain; scale names must match EN exactly.
        var en = NumberToStringConverter.GetConverter("EN");
        var gb = NumberToStringConverter.GetConverter("EN-GB");
        Assert.AreEqual(en.Convert(1_000_000_000L),     gb.Convert(1_000_000_000L));
        Assert.AreEqual(en.Convert(1_000_000_000_000L), gb.Convert(1_000_000_000_000L));
    }
}
