using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
using System.Collections.Generic;
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
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(converter.ConvertOrdinal(1L), converter.ConvertOrdinal((BigInteger)1));
        Assert.AreEqual(converter.ConvertOrdinal(21L), converter.ConvertOrdinal((BigInteger)21));
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_WithVariants()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
            OrdinalVariants =
            [
                new NumberToStringConverter.OrdinalVariantRule(
                    new Dictionary<string, string> { ["form"] = "alternate" },
                    new Dictionary<long, string> { [1] = "ALT_ONE", [10] = "ALT_TEN" },
                    new Dictionary<string, string>(), null, null),
            ],
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("ALT_ONE", converter.ConvertOrdinal((BigInteger)1, "form=alternate"));
        Assert.AreEqual(converter.ConvertOrdinal(1L, "form=alternate"), converter.ConvertOrdinal((BigInteger)1, "form=alternate"));
        Assert.AreEqual(converter.ConvertOrdinal(10L, "form=alternate"), converter.ConvertOrdinal((BigInteger)10, "form=alternate"));
    }

    // ─── G4 — ConvertYear(int, params string[]) ──────────────────────────────

    [TestMethod]
    public void ConvertYear_WithVariants_PassedToConvert()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(source)
        {
            YearFormat = new YearFormatOptions(null, null, null),
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["form"] = "alternate" },
                    [new NumberToStringConverter.ReplacementRule(source.Convert(2021), "ALT_YEAR", ReplacementScope.Standalone)]),
            ],
        };
        var converter = new NumberToStringConverter(options);

        // No split range → delegates to Convert(abs, variants)
        Assert.AreEqual("ALT_YEAR", converter.ConvertYear(2021, "form=alternate"));
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

        Assert.AreEqual($"{converter.ConvertYear(44)} BC", converter.ConvertYear(-44));
        Assert.AreEqual($"{converter.ConvertYear(1984)} BC", converter.ConvertYear(-1984));
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
