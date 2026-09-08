using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
using Utils.NumberToString;
using Utils.Range;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for the improvements-batch PR (points 4–8, 12–14, new languages).
/// </summary>
[TestClass]
public class NumberToStringConverterBatchTests
{
    // ─── G3 — ConvertOrdinal(BigInteger) ────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_BigInteger_DelegatesToLong()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var fr = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("first", en.ConvertOrdinal((BigInteger)1));
        Assert.AreEqual("twenty-first", en.ConvertOrdinal((BigInteger)21));
        Assert.AreEqual("premier", fr.ConvertOrdinal((BigInteger)1));
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_WithVariants()
    {
        var es = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual("primera", es.ConvertOrdinal((BigInteger)1, "gender=femenino"));
        Assert.AreEqual("décima", es.ConvertOrdinal((BigInteger)10, "gender=femenino"));
    }

    // ─── G4 — ConvertYear(int, params string[]) ──────────────────────────────

    [TestMethod]
    public void ConvertYear_WithVariants_PassedToConvert()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var options = new NumberToStringConverterOptions(fr)
        {
            YearFormat = new YearFormatOptions(null, null, null)
        };
        var converter = new NumberToStringConverter(options);

        // No split range → delegates to Convert(abs, variants)
        Assert.AreEqual(fr.Convert(2021, "gender=feminin"),
                        converter.ConvertYear(2021, "gender=feminin"));
    }

    [TestMethod]
    public void ConvertYear_BeforeChristSuffix_AppliedForNegativeYears()
    {
        var enOptions = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            YearFormat = new YearFormatOptions(
                HundredWord: "hundred",
                ZeroConnector: "oh",
                SplitRanges: new IntRange<int>("1100-1999"),
                BeforeChristSuffix: "BC")
        };
        var converter = new NumberToStringConverter(enOptions);

        Assert.AreEqual("forty-four BC", converter.ConvertYear(-44));
        Assert.AreEqual("nineteen eighty-four BC", converter.ConvertYear(-1984));
        Assert.AreEqual("nineteen eighty-four", converter.ConvertYear(1984));
    }

    // ─── G5 — Compiled regex dans TriggerReplace ────────────────────────────

    [TestMethod]
    public void TriggerReplace_IsRegex_HasCompiledRegex()
    {
        var withRegex = new NumberToStringConverter.TriggerReplace("ein$", true, [], null);
        Assert.IsNotNull(withRegex.CompiledRegex);
        // Verify it actually matches
        Assert.IsTrue(withRegex.CompiledRegex!.IsMatch("ein"));
        Assert.IsFalse(withRegex.CompiledRegex!.IsMatch("einem"));
    }

    [TestMethod]
    public void TriggerReplace_NonRegex_CompiledRegexIsNull()
    {
        var noRegex = new NumberToStringConverter.TriggerReplace("ein", false, [], null);
        Assert.IsNull(noRegex.CompiledRegex);
    }

    // ─── G6 — GetConverter pour codes BCP-47 longs ──────────────────────────

    [TestMethod]
    public void GetConverter_LongBCP47_StripsSubtagsRecursively()
    {
        var zh = NumberToStringConverter.GetConverter("ZH");
        var zhHans = NumberToStringConverter.GetConverter("zh-Hans");
        var zhHansCN = NumberToStringConverter.GetConverter("zh-Hans-CN");

        Assert.AreEqual(zh.Convert(1), zhHans.Convert(1));
        Assert.AreEqual(zh.Convert(1), zhHansCN.Convert(1));
    }

    [TestMethod]
    public void GetConverter_UnknownCulture_FallsBackToEN()
    {
        var unknown = NumberToStringConverter.GetConverter("xx-Unknown-Region");
        var en = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(en.Convert(1), unknown.Convert(1));
    }

    // ─── G10 — IntRange<int> pour SplitRanges ───────────────────────────────

    [TestMethod]
    public void ConvertYear_YearFormatOptions_IntRange_Contains()
    {
        var opts = new YearFormatOptions(null, null, new IntRange<int>("1100-1999,2010-2099"));

        Assert.IsTrue(opts.SplitRanges!.Contains(1984));
        Assert.IsTrue(opts.SplitRanges!.Contains(2024));
        Assert.IsFalse(opts.SplitRanges!.Contains(2000));
        Assert.IsFalse(opts.SplitRanges!.Contains(1099));
    }

}
