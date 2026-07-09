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

    [TestMethod]
    public void ConvertFraction_EN_NegativeNumerator_UsesNamedSuffix()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // Denominator 10 is a power of ten → named suffix branch ("tenth(s)").
        // -1/10 should reuse the named suffix like 1/10 does, prefixed by the sign.
        string positive = en.ConvertFraction(1, 10);
        string negative = en.ConvertFraction(-1, 10);
        Assert.AreEqual("one tenth", positive);
        Assert.IsTrue(negative.EndsWith("one tenth"), $"Actual: {negative}");
        Assert.AreNotEqual(positive, negative);
    }

    [TestMethod]
    public void ConvertFraction_FR_NegativeNumerator_UsesNamedSuffix()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        string positive = fr.ConvertFraction(3, 10);
        string negative = fr.ConvertFraction(-3, 10);
        Assert.AreEqual("trois dixièmes", positive);
        Assert.IsTrue(negative.EndsWith("trois dixièmes"), $"Actual: {negative}");
    }

    // ─── Item 31 — ApplyVariantRules / ApplyVariantRulesForScale factored ───

    [TestMethod]
    public void ApplyVariantRules_StillAppliesAfterRefactor_FR()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // Regression check on gender variant application post-refactor.
        Assert.AreEqual("une", fr.Convert(1, "gender=feminin"));
        Assert.AreEqual("un", fr.Convert(1));
    }

    [TestMethod]
    public void ApplyVariantRulesForScale_StillAppliesAfterRefactor_RO()
    {
        var ro = NumberToStringConverter.GetConverter("RO");
        // Scale-scoped variant rules (unu → o mie at scale 1) must still apply post-refactor.
        Assert.AreEqual("o mie", ro.Convert(1000));
        Assert.AreEqual("un milion", ro.Convert(1_000_000));
    }

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
        Assert.ThrowsException<ArgumentException>(() => en.Convert(double.NaN));
    }

    [TestMethod]
    public void Convert_Double_Infinity_ThrowsArgumentException()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.ThrowsException<ArgumentException>(() => en.Convert(double.PositiveInfinity));
        Assert.ThrowsException<ArgumentException>(() => en.Convert(double.NegativeInfinity));
    }

    [TestMethod]
    public void Convert_Float_NaN_ThrowsArgumentException()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.ThrowsException<ArgumentException>(() => en.Convert(float.NaN));
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
