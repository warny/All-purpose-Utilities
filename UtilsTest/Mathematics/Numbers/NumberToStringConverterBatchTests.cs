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

        Assert.AreEqual("first",         en.ConvertOrdinal((BigInteger)1));
        Assert.AreEqual("twenty-first",  en.ConvertOrdinal((BigInteger)21));
        Assert.AreEqual("premier",       fr.ConvertOrdinal((BigInteger)1));
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_WithVariants()
    {
        var es = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual("primera", es.ConvertOrdinal((BigInteger)1,  "gender=femenino"));
        Assert.AreEqual("décima",  es.ConvertOrdinal((BigInteger)10, "gender=femenino"));
    }

    [TestMethod]
    public void ConvertOrdinal_BigInteger_OverflowThrows()
    {
        INumberToStringConverter iface = NumberToStringConverter.GetConverter("EN");
        // BigInteger > long.MaxValue → OverflowException from checked cast
        Assert.ThrowsException<OverflowException>(
            () => iface.ConvertOrdinal(new BigInteger(long.MaxValue) + 1));
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

        Assert.AreEqual("forty-four BC",        converter.ConvertYear(-44));
        Assert.AreEqual("nineteen eighty-four BC", converter.ConvertYear(-1984));
        Assert.AreEqual("nineteen eighty-four", converter.ConvertYear(1984));
    }

    [TestMethod]
    public void ConvertYear_Interface_WithVariants_DelegatesToConvertYear()
    {
        INumberToStringConverter iface = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(iface.ConvertYear(1984), iface.ConvertYear(1984, "some=variant"));
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
        var zh        = NumberToStringConverter.GetConverter("ZH");
        var zhHans    = NumberToStringConverter.GetConverter("zh-Hans");
        var zhHansCN  = NumberToStringConverter.GetConverter("zh-Hans-CN");

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

    // ─── G7 — Nouvelles langues VN, TR, SV, NO, UK ──────────────────────────

    [TestMethod]
    public void Convert_VN_Cardinals()
    {
        var vn = NumberToStringConverter.GetConverter("VN");

        (int n, string expected)[] cases =
        [
            (0,    "không"),
            (1,    "một"),
            (10,   "mười"),
            (15,   "mười lăm"),
            (21,   "hai mươi mốt"),
            (25,   "hai mươi lăm"),
            (100,  "một trăm"),
            (1000, "nghìn"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, vn.Convert(n), $"VN Convert({n})");
    }

    [TestMethod]
    public void ConvertOrdinal_VN_PrefixThu()
    {
        var vn = NumberToStringConverter.GetConverter("VN");

        Assert.AreEqual("thứ nhất", vn.ConvertOrdinal(1));
        Assert.AreEqual("thứ hai",  vn.ConvertOrdinal(2));
        Assert.AreEqual("thứ mười", vn.ConvertOrdinal(10));
    }

    [TestMethod]
    public void Convert_VI_VN_SameCultureAsVN()
    {
        var vn   = NumberToStringConverter.GetConverter("VN");
        var viVN = NumberToStringConverter.GetConverter("VI-VN");

        Assert.AreEqual(vn.Convert(21), viVN.Convert(21));
    }

    [TestMethod]
    public void Convert_TR_Cardinals()
    {
        var tr = NumberToStringConverter.GetConverter("TR");

        (int n, string expected)[] cases =
        [
            (0,    "sıfır"),
            (1,    "bir"),
            (11,   "on bir"),
            (21,   "yirmi bir"),
            (100,  "yüz"),
            (200,  "iki yüz"),
            (1000, "bin"),
            (2000, "iki bin"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, tr.Convert(n), $"TR Convert({n})");
    }

    [TestMethod]
    public void Convert_SV_Cardinals()
    {
        var sv = NumberToStringConverter.GetConverter("SV");

        (int n, string expected)[] cases =
        [
            (0,    "noll"),
            (11,   "elva"),
            (19,   "nitton"),
            (20,   "tjugo"),
            (21,   "tjugoett"),
            (31,   "trettio ett"),
            (1000, "tusen"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, sv.Convert(n), $"SV Convert({n})");
    }

    [TestMethod]
    public void Convert_NO_Cardinals()
    {
        var no = NumberToStringConverter.GetConverter("NO");

        (int n, string expected)[] cases =
        [
            (0,    "null"),
            (11,   "elleve"),
            (21,   "tjue en"),
            (1000, "tusen"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, no.Convert(n), $"NO Convert({n})");
    }

    [TestMethod]
    public void Convert_UK_Cardinals()
    {
        var uk = NumberToStringConverter.GetConverter("UK");

        (int n, string expected)[] cases =
        [
            (0,    "нуль"),
            (11,   "одинадцять"),
            (21,   "двадцять один"),
            (100,  "сто"),
            (1000, "тисяч"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, uk.Convert(n), $"UK Convert({n})");
    }

    // ─── G8 — ES buildString fix (Point 4) ──────────────────────────────────

    [TestMethod]
    public void Convert_ES_31to99_UseConnectorY()
    {
        var es = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual("treinta y uno",    es.Convert(31));
        Assert.AreEqual("cuarenta y cinco", es.Convert(45));
        Assert.AreEqual("noventa y nueve",  es.Convert(99));
    }

    [TestMethod]
    public void ConvertOrdinal_ES_31to99_UnitRuleFires()
    {
        var es = NumberToStringConverter.GetConverter("ES");

        // With "treinta y uno", last word is "uno" → ordinal word rule fires
        Assert.AreEqual("treinta y primero",    es.ConvertOrdinal(31));
        Assert.AreEqual("cuarenta y quinto",    es.ConvertOrdinal(45));
        Assert.AreEqual("noventa y noveno",     es.ConvertOrdinal(99));
    }

    // ─── G9 — IT buildString fix (Point 4) ──────────────────────────────────

    [TestMethod]
    public void Convert_IT_21to29_SpaceSeparated()
    {
        var it = NumberToStringConverter.GetConverter("IT");

        Assert.AreEqual("venti uno",  it.Convert(21));
        Assert.AreEqual("venti due",  it.Convert(22));
        Assert.AreEqual("venti nove", it.Convert(29));
    }

    [TestMethod]
    public void Convert_IT_21to29_FemminileVariant()
    {
        var it = NumberToStringConverter.GetConverter("IT");

        // After adding space in buildString, LastWord replacement now fires for 21-29
        Assert.AreEqual("venti una", it.Convert(21, "gender=femminile"));
        Assert.AreEqual("venti due", it.Convert(22, "gender=femminile"));
    }

    // ─── G10 — IntRange<int> pour SplitRanges ───────────────────────────────

    [TestMethod]
    public void ConvertYear_EN_IntRange_MultipleRanges()
    {
        var en = NumberToStringConverter.GetConverter("EN");

        // Both EN split ranges should still work after IntRange migration
        Assert.AreEqual("nineteen eighty-four", en.ConvertYear(1984)); // range 1100-1999
        Assert.AreEqual("twenty twenty-four",   en.ConvertYear(2024)); // range 2010-2099
        Assert.AreEqual("two thousand",          en.ConvertYear(2000)); // outside ranges
    }

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
