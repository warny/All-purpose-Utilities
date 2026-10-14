using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30B: agreement of the low scale nouns (scales 1–3: thousand, million, milliard) with their
/// multiplier in Ukrainian, Bulgarian, Czech and Slovak, the Slovak one-word writing of the thousands
/// and of the tens with units, and the selectors added for it. Every expected form is sourced in
/// <c>docs/NTS-08-linguistic-sources.md</c> (NTS-30B section). The generated Conway scales from 10^12
/// use the generated forms of NTS-30C (<see cref="NumberToStringNts30CTests"/>): only the absence of an exact form there is pinned.
/// </summary>
[TestClass]
public class NumberToStringNts30BTests
{
    /// <summary>Converts <paramref name="value"/> with the built-in converter of <paramref name="culture"/>.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="value">The value.</param>
    /// <param name="variants">The optional variants.</param>
    /// <returns>The cardinal.</returns>
    private static string C(string culture, BigInteger value, params string[] variants)
        => NumberToStringConverter.GetConverter(culture).Convert(value, variants);

    /// <summary>Returns the key a selector gives to <paramref name="value"/>.</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="value">The governing value.</param>
    /// <returns>The form key.</returns>
    private static string Select(ILexicalFormSelector selector, BigInteger value)
        => selector.SelectForm(new LexicalFormContext(value, new Dictionary<string, string>()));

    /// <summary>Asserts every multiplier of <paramref name="expected"/> at the given scale.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="scale">The scale index (1 = thousands).</param>
    /// <param name="expected">The expected cardinal by multiplier.</param>
    private static void AssertScale(string culture, int scale, IReadOnlyDictionary<int, string> expected)
    {
        foreach (var (multiplier, text) in expected)
            Assert.AreEqual(text, C(culture, multiplier * BigInteger.Pow(1000, scale)), $"{culture} {multiplier} × 1000^{scale}");
    }

    // ----- selectors ---------------------------------------------------------------------------------

    /// <summary>CLDR uk categories: one (…1 but not …11), few (…2-4 but not …12-14), many otherwise.</summary>
    [TestMethod]
    public void UkrainianPluralSelector_FollowsCldr()
    {
        var selector = new UkrainianPluralLexicalFormSelector();
        var expected = new Dictionary<long, string>
        {
            [0] = "many", [1] = "one", [2] = "few", [4] = "few", [5] = "many", [11] = "many", [12] = "many",
            [14] = "many", [21] = "one", [22] = "few", [24] = "few", [25] = "many", [101] = "one", [102] = "few",
            [111] = "many", [112] = "many", [-21] = "one",
        };
        foreach (var (value, category) in expected)
            Assert.AreEqual(category, Select(selector, value), $"value {value}");
        Assert.AreEqual("one", Select(selector, BigInteger.Pow(10, 30) + 1));
    }

    /// <summary>The Ukrainian and Russian selectors share the CLDR integer rule.</summary>
    [TestMethod]
    public void UkrainianAndRussianSelectors_Agree()
    {
        var ukrainian = new UkrainianPluralLexicalFormSelector();
        var russian = new RussianPluralLexicalFormSelector();
        for (int value = 0; value <= 1200; value++)
            Assert.AreEqual(Select(russian, value), Select(ukrainian, value), $"value {value}");
    }

    /// <summary>CLDR cs/sk integer categories: one (exactly 1), few (exactly 2-4), other otherwise (22-24 included).</summary>
    [TestMethod]
    public void CzechSlovakPluralSelector_FollowsCldr()
    {
        var selector = new CzechSlovakPluralLexicalFormSelector();
        var expected = new Dictionary<long, string>
        {
            [0] = "other", [1] = "one", [2] = "few", [3] = "few", [4] = "few", [5] = "other", [11] = "other",
            [12] = "other", [21] = "other", [22] = "other", [24] = "other", [25] = "other", [101] = "other",
            [102] = "other", [-1] = "one", [-3] = "few",
        };
        foreach (var (value, category) in expected)
            Assert.AreEqual(category, Select(selector, value), $"value {value}");
        Assert.AreEqual("other", Select(selector, BigInteger.Pow(10, 30) + 2));
    }

    /// <summary>The built-in configurations bind the scales 1–3 to the intended selectors, and no exact form from scale 4 (generated forms, NTS-30C).</summary>
    [TestMethod]
    public void Configurations_BindLowScalesOnly()
    {
        foreach (var (culture, scales, selectorType) in new (string, int[], System.Type)[]
        {
            ("UK", [1, 2, 3], typeof(UkrainianPluralLexicalFormSelector)),
            ("BG", [1, 2, 3], typeof(DefaultLexicalFormSelector)),
            ("CS", [1, 2, 3], typeof(CzechSlovakPluralLexicalFormSelector)),
            ("SK", [2, 3], typeof(CzechSlovakPluralLexicalFormSelector)),
        })
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            CollectionAssert.AreEquivalent(scales, converter.ScaleForms.Keys.ToArray(), culture);
            foreach (int scale in scales)
                Assert.IsInstanceOfType(converter.ScaleFormSelectors[scale], selectorType, $"{culture} scale {scale}");
        }
    }

    // ----- Ukrainian ---------------------------------------------------------------------------------

    /// <summary>Ukrainian multipliers in their masculine and feminine forms.</summary>
    private static readonly Dictionary<int, (string Masculine, string Feminine)> UkrainianMultipliers = new()
    {
        [1] = ("один", "одна"), [2] = ("два", "дві"), [4] = ("чотири", "чотири"), [5] = ("п'ять", "п'ять"),
        [11] = ("одинадцять", "одинадцять"), [12] = ("дванадцять", "дванадцять"), [14] = ("чотирнадцять", "чотирнадцять"),
        [21] = ("двадцять один", "двадцять одна"), [22] = ("двадцять два", "двадцять дві"),
        [24] = ("двадцять чотири", "двадцять чотири"), [25] = ("двадцять п'ять", "двадцять п'ять"),
        [101] = ("сто один", "сто одна"), [102] = ("сто два", "сто дві"), [105] = ("сто п'ять", "сто п'ять"),
        [111] = ("сто одинадцять", "сто одинадцять"), [112] = ("сто дванадцять", "сто дванадцять"),
    };

    /// <summary>The multipliers taking the nominative singular (…1 but not …11) in the matrix.</summary>
    private static readonly HashSet<int> UkrainianOne = [1, 21, 101];

    /// <summary>The multipliers taking the nominative plural (…2-4 but not …12-14) in the matrix.</summary>
    private static readonly HashSet<int> UkrainianFew = [2, 4, 22, 24, 102];

    /// <summary>Builds the expected Ukrainian matrix of one scale noun.</summary>
    /// <param name="one">The nominative singular.</param>
    /// <param name="few">The nominative plural.</param>
    /// <param name="many">The genitive plural.</param>
    /// <param name="feminine">Whether the noun is feminine (тисяча).</param>
    /// <returns>The expected cardinal by multiplier.</returns>
    private static Dictionary<int, string> UkrainianMatrix(string one, string few, string many, bool feminine)
        => UkrainianMultipliers.ToDictionary(
            kv => kv.Key,
            kv => (feminine ? kv.Value.Feminine : kv.Value.Masculine) + " " +
                  (UkrainianOne.Contains(kv.Key) ? one : UkrainianFew.Contains(kv.Key) ? few : many));

    /// <summary>тисяча (feminine): одна/дві, тисяча/тисячі/тисяч; 1000 is the bare noun.</summary>
    [TestMethod]
    public void Ukrainian_Thousands()
    {
        var expected = UkrainianMatrix("тисяча", "тисячі", "тисяч", feminine: true);
        expected[1] = "тисяча";
        AssertScale("UK", 1, expected);
        Assert.AreEqual("тисяча один", C("UK", 1001));
        Assert.AreEqual("три тисячі триста шістдесят чотири", C("UK", 3364));
        Assert.AreEqual("п'ятсот п'ятдесят вісім тисяч", C("UK", 558000));
    }

    /// <summary>мільйон and мільярд (masculine): мільйон/мільйони/мільйонів, мільярд/мільярди/мільярдів.</summary>
    [TestMethod]
    public void Ukrainian_MillionsAndMilliards()
    {
        AssertScale("UK", 2, UkrainianMatrix("мільйон", "мільйони", "мільйонів", feminine: false));
        AssertScale("UK", 3, UkrainianMatrix("мільярд", "мільярди", "мільярдів", feminine: false));
        Assert.AreEqual("два мільйони триста сорок п'ять тисяч шістсот сімдесят вісім", C("UK", 2345678));
    }

    // ----- Bulgarian ---------------------------------------------------------------------------------

    /// <summary>
    /// хиляда (feminine) takes хиляди after any multiplier but one; милион and милиард take their count
    /// form (милиона, милиарда) with a masculine multiplier (един, два). The compounds pin only the
    /// multiplier gender and the noun: the missing conjunction of the cardinal itself (двадесет и …) is
    /// the separate NTS-30D.
    /// </summary>
    [TestMethod]
    public void Bulgarian_LowScales()
    {
        AssertScale("BG", 1, new Dictionary<int, string> { [1] = "хиляда", [2] = "две хиляди", [5] = "пет хиляди", [10] = "десет хиляди" });
        AssertScale("BG", 2, new Dictionary<int, string> { [1] = "един милион", [2] = "два милиона", [5] = "пет милиона", [10] = "десет милиона" });
        AssertScale("BG", 3, new Dictionary<int, string> { [1] = "един милиард", [2] = "два милиарда", [5] = "пет милиарда", [10] = "десет милиарда" });
        foreach (var (scale, tail1, tail2, tail5) in new[]
        {
            (1, "една хиляди", "две хиляди", "пет хиляди"),
            (2, "един милиона", "два милиона", "пет милиона"),
            (3, "един милиарда", "два милиарда", "пет милиарда"),
        })
        {
            StringAssert.EndsWith(C("BG", 21 * BigInteger.Pow(1000, scale)), tail1, $"21 × 1000^{scale}");
            StringAssert.EndsWith(C("BG", 22 * BigInteger.Pow(1000, scale)), tail2, $"22 × 1000^{scale}");
            StringAssert.EndsWith(C("BG", 25 * BigInteger.Pow(1000, scale)), tail5, $"25 × 1000^{scale}");
        }
    }

    /// <summary>The standalone counting forms (едно, две) and the explicit genders are unchanged.</summary>
    [TestMethod]
    public void Bulgarian_StandaloneFormsUnchanged()
    {
        Assert.AreEqual("едно", C("BG", 1));
        Assert.AreEqual("две", C("BG", 2));
        Assert.AreEqual("един", C("BG", 1, "gender=masculine"));
        Assert.AreEqual("два", C("BG", 2, "gender=masculine"));
    }

    // ----- Czech -------------------------------------------------------------------------------------

    /// <summary>
    /// tisíc/tisíce, milion/miliony/milionů, miliarda/miliardy/miliard: the nominative after 1-4 only, the
    /// genitive after every other multiplier including 21-24 (IJP: dvacet jedna žáků, dvacet dva žáků).
    /// </summary>
    [TestMethod]
    public void Czech_LowScales()
    {
        AssertScale("CS", 1, new Dictionary<int, string>
        {
            [1] = "tisíc", [2] = "dva tisíce", [3] = "tři tisíce", [4] = "čtyři tisíce", [5] = "pět tisíc",
            [21] = "dvacet jedna tisíc", [22] = "dvacet dva tisíc", [24] = "dvacet čtyři tisíc", [25] = "dvacet pět tisíc",
        });
        AssertScale("CS", 2, new Dictionary<int, string>
        {
            [1] = "jeden milion", [2] = "dva miliony", [3] = "tři miliony", [4] = "čtyři miliony", [5] = "pět milionů",
            [21] = "dvacet jedna milionů", [22] = "dvacet dva milionů", [24] = "dvacet čtyři milionů", [25] = "dvacet pět milionů",
        });
        AssertScale("CS", 3, new Dictionary<int, string>
        {
            [1] = "jedna miliarda", [2] = "dvě miliardy", [3] = "tři miliardy", [4] = "čtyři miliardy", [5] = "pět miliard",
            [21] = "dvacet jedna miliard", [24] = "dvacet čtyři miliard", [25] = "dvacet pět miliard",
        });
        // The multiplier gender of 22 before the feminine miliarda in the genitive construction is not
        // sourced (see the NTS-30B limitation); only the noun form is pinned.
        StringAssert.EndsWith(C("CS", 22_000_000_000), " miliard");
    }

    /// <summary>The IJP reading examples (Členění čísel, víceslovné číslovkové výrazy).</summary>
    [TestMethod]
    public void Czech_HandbookExamples()
    {
        Assert.AreEqual("dva tisíce dvě stě třicet jedna", C("CS", 2231));
        Assert.AreEqual("jeden milion tři sta tisíc sedm set osmdesát tři", C("CS", 1300783));
        Assert.AreEqual("dva tisíce pět set dvacet tři", C("CS", 2523));
    }

    // ----- Slovak ------------------------------------------------------------------------------------

    /// <summary>
    /// The thousands are one word with their multiplier (dvetisíc, tritisíc, dvadsaťsedemtisíc), the
    /// millions and milliards agree apart (štyri milióny, päť miliónov, dve miliardy, päť miliárd), a
    /// compound being one indeclinable word governing the genitive plural (dvadsaťdva miliónov).
    /// </summary>
    [TestMethod]
    public void Slovak_LowScales()
    {
        AssertScale("SK", 1, new Dictionary<int, string>
        {
            [1] = "tisíc", [2] = "dvetisíc", [3] = "tritisíc", [4] = "štyritisíc", [5] = "päťtisíc", [10] = "desaťtisíc",
            [21] = "dvadsaťjedentisíc", [22] = "dvadsaťdvatisíc", [24] = "dvadsaťštyritisíc", [25] = "dvadsaťpäťtisíc",
            [27] = "dvadsaťsedemtisíc", [182] = "stoosemdesiatdvatisíc",
        });
        AssertScale("SK", 2, new Dictionary<int, string>
        {
            [1] = "jeden milión", [2] = "dva milióny", [3] = "tri milióny", [4] = "štyri milióny", [5] = "päť miliónov",
            [21] = "dvadsaťjeden miliónov", [22] = "dvadsaťdva miliónov", [24] = "dvadsaťštyri miliónov", [25] = "dvadsaťpäť miliónov",
        });
        AssertScale("SK", 3, new Dictionary<int, string>
        {
            [1] = "jedna miliarda", [2] = "dve miliardy", [3] = "tri miliardy", [4] = "štyri miliardy", [5] = "päť miliárd",
            [21] = "dvadsaťjeden miliárd", [22] = "dvadsaťdva miliárd", [24] = "dvadsaťštyri miliárd", [25] = "dvadsaťpäť miliárd",
        });
    }

    /// <summary>The PSP and Jarošová (2021) examples in the readable spelling (thousands, hundreds, tens with units apart).</summary>
    [TestMethod]
    public void Slovak_SpellingRuleExamples()
    {
        Assert.AreEqual("sedemtisíc dvesto osemdesiattri", C("SK", 7283));
        Assert.AreEqual("pätnásť miliónov tristoštyridsaťosemtisíc osemsto štyridsaťosem", C("SK", 15348848));
        Assert.AreEqual("tisíc dvadsaťdva", C("SK", 1022));
        Assert.AreEqual("tisíc sto dva", C("SK", 1102));
        Assert.AreEqual("tritisíc tristo dvadsaťdva", C("SK", 3322));
        Assert.AreEqual("päťstoštyridsaťtritisíc tristo dvadsaťšesť", C("SK", 543326));
        Assert.AreEqual("päť miliónov dvestotridsaťštyritisíc päťsto šesťdesiatsedem", C("SK", 5234567));
        Assert.AreEqual("dvadsaťsedem", C("SK", 27));
        Assert.AreEqual("päťsto", C("SK", 500));
    }

    /// <summary>A tens-with-units compound is indeclinable: no gender form (dvadsaťjeden, dvadsaťdva žien).</summary>
    [TestMethod]
    public void Slovak_CompoundsIgnoreGender()
    {
        Assert.AreEqual("dve", C("SK", 2, "gender=ženský"));
        Assert.AreEqual("dvadsaťdva", C("SK", 22, "gender=ženský"));
        Assert.AreEqual("dvadsaťjeden", C("SK", 21, "gender=ženský"));
    }

    // ----- generated scales (NTS-30C boundary) ---------------------------------------------------------

    /// <summary>The scale 4 and above has no exact ScaleForm in any of the four languages: it uses the GeneratedScaleForm of its suffix (NTS-30C).</summary>
    [TestMethod]
    public void GeneratedScales_HaveNoConfiguredForms()
    {
        foreach (string culture in new[] { "UK", "BG", "CS", "SK" })
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            for (int scale = 4; scale <= 8; scale++)
                Assert.IsFalse(converter.ScaleForms.ContainsKey(scale), $"{culture} scale {scale}");
        }
    }
}
