using System;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for items 29, 30, 31, 45, 46 of TODO.md (engine robustness and API symmetry fixes).
/// </summary>
[TestClass]
public class NumberToStringConverterEngineFixesTests
{
    // ─── Item 29 — GetMonthName catch scoped to expected exceptions ─────────

    [TestMethod]
    public void GetMonthName_OutOfRangeMonth_FallsBackToNumber()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var method = en.GetType().GetMethod("GetMonthName", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method);
        // Month 0 → index -1 is out of range for MonthNames[month - 1] → must fall back, not throw.
        string result = (string)method!.Invoke(en, [0])!;
        Assert.AreEqual("0", result);
    }

    // ─── Item 30 — BuildFractionText with negative numerator ────────────────

    // ─── Item 31 — ApplyVariantRules / ApplyVariantRulesForScale factored ───

    // ─── Item 45 — Convert(double)/Convert(float) overload + NaN/Infinity ───

    [TestMethod]
    public void Convert_Double_WithoutVariants_Works()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(en.Convert(3.5, System.Array.Empty<string>()), en.Convert(3.5));
    }

    [TestMethod]
    public void Convert_Float_WithoutVariants_Works()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(en.Convert(3.5f, System.Array.Empty<string>()), en.Convert(3.5f));
    }

    [TestMethod]
    public void Convert_Double_NaN_ThrowsArgumentException()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.ThrowsExactly<ArgumentException>(() => en.Convert(double.NaN));
    }

    [TestMethod]
    public void Convert_Double_Infinity_ThrowsArgumentException()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.ThrowsExactly<ArgumentException>(() => en.Convert(double.PositiveInfinity));
        Assert.ThrowsExactly<ArgumentException>(() => en.Convert(double.NegativeInfinity));
    }

    [TestMethod]
    public void Convert_Float_NaN_ThrowsArgumentException()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.ThrowsExactly<ArgumentException>(() => en.Convert(float.NaN));
    }

    // ─── Item 46 — ConvertFraction(int/long) overloads ───────────────────────

    [TestMethod]
    public void ConvertFraction_Int_MatchesBigInteger()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(en.ConvertFraction((BigInteger)1, (BigInteger)3), en.ConvertFraction(1, 3));
    }

    [TestMethod]
    public void ConvertFraction_Long_MatchesBigInteger()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(en.ConvertFraction((BigInteger)3L, (BigInteger)4L), en.ConvertFraction(3L, 4L));
    }
}
