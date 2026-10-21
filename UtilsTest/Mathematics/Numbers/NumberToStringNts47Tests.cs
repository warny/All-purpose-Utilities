using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-47 — the last audit of NTS-42, which it closes: the Slovak genitive singular of a generated <c>-ón</c> scale noun
/// is opened, the families D (CS and SK <c>lomeno</c>/<c>lomené</c> under an external case) and E/CS (Czech zero in an
/// oblique case) are closed as deliberately unsupported.
/// <list type="bullet">
/// <item><b>SK <c>-ón</c></b>: the series is declined after the pattern <c>dub</c> (L. Navrátil, "Neohybnosť v ohybných
/// slovných druhoch", Kultúra slova 37, 2003, 341: "Číslovky milión, bilión, trilión majú substantívnu povahu a skloňujú sa
/// podľa vzoru dub"), with the genitive singular in <c>-a</c> for every member a dictionary gives: <i>milión, -a</i>,
/// <i>bilión, -a</i>, <i>trilión, -a</i> (Pravidlá slovenského pravopisu, 1940, pp. 254, 144, 412); <i>bilión -a</i>
/// (KSSJ 4); <i>bilión ‑a</i>, <i>kvadrilión ‑a</i>, <i>kvintilión ‑a</i> (Pravidlá slovenského pravopisu, current text of
/// the JÚĽŠ). The higher Conway names are the productive continuation of that series (AGENTS.md): only the configured
/// suffix fragment <c>óna</c> is added, the plugin holds no name.</item>
/// <item><b>D</b> and <b>E/CS</b>: deliberately unsupported (<c>docs/NTS-42-deliberate-closures.md</c>). No source states
/// the form of the first operand of a whole <c>lomeno</c>/<c>lomené</c> phrase under an external case, and the Czech
/// cardinal <c>nula</c> is attested both declined and indeclinable (NovaMorf: <i>záruka na nula/nulu vadných bodů</i>)
/// with no canonical form. These tests lock that decision: reopening either needs a new source, an update of that
/// document and an explicit change of these tests.</item>
/// </list>
/// The expected strings apply these rules; they are not quotations.
/// </summary>
[TestClass]
public class NumberToStringNts47Tests
{
    private static readonly string[] s_czechObliqueCases = ["case=genitiv", "case=dativ", "case=akuzativ", "case=lokál", "case=instrumentál"];
    private static readonly string[] s_slovakObliqueCases = ["case=genitív", "case=datív", "case=akuzatív", "case=lokál", "case=inštrumentál"];

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Returns <paramref name="multiplier"/> × 10^<paramref name="exponent"/>.</summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <returns>The value.</returns>
    private static BigInteger E(long multiplier, int exponent) => multiplier * BigInteger.Pow(10, exponent);

    /// <summary>Asserts that a fraction fails closed under each of the given case variants.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="cases">The case variants.</param>
    private static void AssertClosed(string language, BigInteger numerator, BigInteger denominator, IEnumerable<string> cases)
    {
        NumberToStringConverter converter = Get(language);
        foreach (string variant in cases)
            Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertFraction(numerator, denominator, variant),
                $"{language} {numerator}/{denominator} {variant}");
    }

    // ===== SK — genitive singular of a generated -ón noun =============================================

    /// <summary>
    /// The attested members of the series and their first continuations in the genitive singular, after the multiplier
    /// one (<c>jedného</c>); the fraction noun stays in the genitive plural after a scale noun (Morfológia 330).
    /// </summary>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(12, "jedného bilióna tretín")]
    [DataRow(18, "jedného trilióna tretín")]
    [DataRow(24, "jedného kvadrilióna tretín")]
    [DataRow(30, "jedného kvintilióna tretín")]
    [DataRow(36, "jedného sextilióna tretín")]
    [DataRow(42, "jedného septilióna tretín")]
    public void Sk_GeneratedGenitiveSingular_FirstNames(int exponent, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(BigInteger.Pow(10, exponent), 3, "case=genitív"));

    /// <summary>The same nouns over other denominators: only the fraction noun changes.</summary>
    /// <param name="denominator">The denominator.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(2L, "jedného trilióna polovíc")]
    [DataRow(5L, "jedného trilióna pätín")]
    [DataRow(100L, "jedného trilióna stotín")]
    public void Sk_GeneratedGenitiveSingular_OtherDenominators(long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(BigInteger.Pow(10, 18), denominator, "case=genitív"));

    /// <summary>
    /// Names generated by the engine only: the expected noun is taken from the converter's own cardinal and receives the
    /// configured <c>-a</c>; nothing in the plugin spells it.
    /// </summary>
    /// <param name="exponent">The power of ten.</param>
    [TestMethod]
    [DataRow(60)]
    [DataRow(84)]
    [DataRow(600)]
    [DataRow(4800)]
    [DataRow(6024)]
    [DataRow(60_006)]
    public void Sk_GeneratedGenitiveSingular_EngineNames(int exponent)
    {
        NumberToStringConverter sk = Get("SK");
        BigInteger value = BigInteger.Pow(10, exponent);
        string cardinal = sk.Convert(value);
        StringAssert.StartsWith(cardinal, "jeden ");
        StringAssert.EndsWith(cardinal, "ión");
        string noun = cardinal["jeden ".Length..];
        Assert.AreEqual("jedného " + noun + "a tretín", sk.ConvertFraction(value, 3, "case=genitív"));
    }

    /// <summary>
    /// Compounds: only the last component declines (NTS-40), so the genitive singular appears only on a final generated
    /// noun after one; the words before it keep their absolute form.
    /// </summary>
    [TestMethod]
    public void Sk_GeneratedGenitiveSingular_Compounds()
    {
        NumberToStringConverter sk = Get("SK");
        Assert.AreEqual("jeden trilión jedného bilióna tretín", sk.ConvertFraction(E(1, 18) + E(1, 12), 3, "case=genitív"));
        Assert.AreEqual("dva trilióny jedného bilióna tretín", sk.ConvertFraction(E(2, 18) + E(1, 12), 3, "case=genitív"));
        Assert.AreEqual("jeden kvadrilión jedného milióna tretín", sk.ConvertFraction(E(1, 24) + E(1, 6), 3, "case=genitív"));
        Assert.AreEqual("jeden kvadrilión piatich tretín", sk.ConvertFraction(E(1, 24) + 5, 3, "case=genitív"));
    }

    /// <summary>
    /// The neighbouring cells are unchanged: the other singular cases of the <c>-ón</c> family, its plurals, the
    /// genitive after the other multipliers, the feminine <c>-arda</c> family and the static <c>milión</c>.
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <param name="variant">The case.</param>
    /// <param name="expected">The expected reading.</param>
    [TestMethod]
    [DataRow(1L, 18, "case=datív", "jednému triliónu tretín")]
    [DataRow(1L, 18, "case=akuzatív", "jeden trilión tretín")]
    [DataRow(1L, 18, "case=lokál", "jednom trilióne tretín")]
    [DataRow(1L, 18, "case=inštrumentál", "jedným triliónom tretín")]
    [DataRow(2L, 18, "case=genitív", "dvoch triliónov tretín")]
    [DataRow(2L, 18, "case=datív", "dvom triliónom tretín")]
    [DataRow(3L, 18, "case=lokál", "troch triliónoch tretín")]
    [DataRow(5L, 18, "case=inštrumentál", "piatimi triliónmi tretín")]
    [DataRow(5L, 18, "case=genitív", "piatich triliónov tretín")]
    [DataRow(21L, 18, "case=genitív", "dvadsaťjeden triliónov tretín")]
    [DataRow(1L, 15, "case=genitív", "jednej biliardy tretín")]
    [DataRow(1L, 21, "case=genitív", "jednej triliardy tretín")]
    [DataRow(1L, 6, "case=genitív", "jedného milióna tretín")]
    public void Sk_Neighbours_Unchanged(long multiplier, int exponent, string variant, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(E(multiplier, exponent), 3, variant));

    /// <summary>The nominative fraction and the cardinal ignore the case keys: both read as before.</summary>
    [TestMethod]
    public void Sk_NominativeAndCardinal_Unchanged()
    {
        NumberToStringConverter sk = Get("SK");
        Assert.AreEqual("jeden trilión", sk.Convert(E(1, 18)));
        Assert.AreEqual("dva trilióny", sk.Convert(E(2, 18)));
        Assert.AreEqual("päť triliónov", sk.Convert(E(5, 18)));
        Assert.AreEqual("jeden trilión tretín", sk.ConvertFraction(E(1, 18), 3));
        Assert.AreEqual("jeden trilión tretín", sk.ConvertFraction(E(1, 18), 3, "case=nominatív"));
    }

    /// <summary>
    /// The public overload called without the converter still has no scale noun from 10^12, the genitive included; the
    /// Czech genitive singular (<c>-u</c>, IJP) is not affected.
    /// </summary>
    [TestMethod]
    public void Sk_PublicOverloadAndCzech_Unchanged()
    {
        IFractionLanguageSpecifics sk = new SlovakOrdinalLanguageSpecifics();
        var genitive = new Dictionary<string, string> { ["case"] = "genitív" };
        Assert.IsFalse(sk.TryGetFractionDenominator(E(1, 18), 3, genitive, out _));
        Assert.AreEqual("jednoho trilionu třetin", Get("CS").ConvertFraction(E(1, 18), 3, "case=genitiv"));
    }

    // ===== D — lomeno / lomené under an external case (deliberately unsupported) ======================

    /// <summary>
    /// Family D is deliberately unsupported (<c>docs/NTS-42-deliberate-closures.md</c>): a denominator without a
    /// fraction noun is read <c>X lomeno/lomené Y</c> in the nominative only; under an external case the form of
    /// <c>X</c> (<i>s dvěma lomeno třemi</i> or <i>s dvě lomeno třemi</i>) is not established, so no reading is invented.
    /// </summary>
    /// <param name="language">The culture.</param>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="nominative">The nominative reading, unchanged.</param>
    [TestMethod]
    [DataRow("CS", 2L, 23L, "dvě lomeno dvaceti třemi")]
    [DataRow("CS", 1L, 21L, "jedna lomeno dvaceti jednou")]
    [DataRow("SK", 2L, 23L, "dva lomené dvadsiatimi troma")]
    [DataRow("SK", 1L, 142L, "jedna lomené sto štyridsiatimi dvoma")]
    public void D_SlashExternalCase_DeliberatelyUnsupported(string language, long numerator, long denominator, string nominative)
    {
        AssertClosed(language, numerator, denominator, language == "CS" ? s_czechObliqueCases : s_slovakObliqueCases);
        Assert.AreEqual(nominative, Get(language).ConvertFraction(numerator, denominator));
    }

    /// <summary>
    /// The new Slovak genitive key does not open D: a <c>lomené</c> denominator from 10^12 under an external case, the
    /// genitive included, and a Slovak zero over a denominator without a fraction noun stay deliberately unsupported.
    /// </summary>
    [TestMethod]
    public void D_GeneratedSlashAndSlovakZero_DeliberatelyUnsupported()
    {
        AssertClosed("SK", BigInteger.One, E(1, 12), s_slovakObliqueCases);
        AssertClosed("SK", BigInteger.One, E(1, 18), s_slovakObliqueCases);
        AssertClosed("SK", BigInteger.Zero, 23, s_slovakObliqueCases);
        AssertClosed("CS", BigInteger.One, E(1, 12), s_czechObliqueCases);
        Assert.AreEqual("jedna lomené jedným triliónom", Get("SK").ConvertFraction(BigInteger.One, E(1, 18)));
    }

    /// <summary>
    /// A fraction with a fraction noun is not family D: 2/3 reads in every case in both languages (NTS-40), which
    /// delimits what stays closed.
    /// </summary>
    [TestMethod]
    public void D_FractionNounIsNotFamilyD()
    {
        Assert.AreEqual("dvou třetin", Get("CS").ConvertFraction(2, 3, "case=genitiv"));
        Assert.AreEqual("dvěma třetinami", Get("CS").ConvertFraction(2, 3, "case=instrumentál"));
        Assert.AreEqual("dvoch tretín", Get("SK").ConvertFraction(2, 3, "case=genitív"));
        Assert.AreEqual("dvoma tretinami", Get("SK").ConvertFraction(2, 3, "case=inštrumentál"));
    }

    // ===== E/CS — Czech zero in an oblique case (deliberately unsupported) =============================

    /// <summary>
    /// Family E/CS is deliberately unsupported (<c>docs/NTS-42-deliberate-closures.md</c>): the cardinal <c>nula</c> is
    /// attested both declined and indeclinable before a counted noun (NovaMorf, § 3.1.1: <i>záruka na nula/nulu vadných
    /// bodů</i>; <i>do nula celá sedmi</i>) with no canonical preference, and no source gives the fraction paradigm, so
    /// no variant is selected; the nominative reading is unchanged.
    /// </summary>
    /// <param name="denominator">The denominator.</param>
    /// <param name="nominative">The nominative reading, unchanged.</param>
    [TestMethod]
    [DataRow(3L, "nula lomeno třemi")]
    [DataRow(5L, "nula lomeno pěti")]
    [DataRow(100L, "nula lomeno stem")]
    public void E_CzechZeroOblique_DeliberatelyUnsupported(long denominator, string nominative)
    {
        AssertClosed("CS", BigInteger.Zero, denominator, s_czechObliqueCases);
        Assert.AreEqual(nominative, Get("CS").ConvertFraction(0, denominator));
    }
}
