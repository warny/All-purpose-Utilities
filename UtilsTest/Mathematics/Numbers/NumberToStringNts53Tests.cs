using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-53, production behaviour of the numeral defects observed by the NTS-52 dump:
/// <list type="bullet">
/// <item><description>
/// CA (and ca-ES, ca-ES-valencia): the plural of <c>milió</c> is <c>milions</c> (IEC, Ortografia catalana §4.4.2.2a:
/// <c>dos milions cent mil</c>; DIEC2 "milió": <c>Dos milions d'habitants</c>), and 10^9 is <c>mil milions</c> (DIEC2
/// "miliard": <c>Mil milions</c>), the multiplier of <c>milió</c> being a complete cardinal (<c>pairedScalesFrom="2"</c>).
/// </description></item>
/// <item><description>
/// GL (and gl-ES): 10^9 is <c>mil millóns</c> (RAG, Dicionario, "eón": <c>mil millóns de anos</c>), through the same
/// pairing.
/// </description></item>
/// <item><description>
/// RO (and RO-RO): no <c>de</c> between a scale multiplier ending on 1 to 19 and its scale noun (GALR 2005, vol. I,
/// pp. 296-297): <c>o sută nouăsprezece mii</c>, <c>o sută douăzeci de mii</c>. One before a currency noun is
/// attributive: <c>un euro</c>, <c>o liră</c> (DEX, "unu"), the cardinal and the compounds keeping <c>unu</c>.
/// </description></item>
/// </list>
/// Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-53 section.
/// </summary>
[TestClass]
public class NumberToStringNts53Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>Converts an amount written in the invariant culture.</summary>
    /// <param name="language">The culture.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The text.</returns>
    private static string Amount(string language, string amount, CurrencyDefinition currency)
        => Get(language).ConvertCurrency(decimal.Parse(amount, CultureInfo.InvariantCulture), currency);

    /// <summary>Builds a caller-defined currency.</summary>
    /// <param name="singular">The unit noun, singular.</param>
    /// <param name="plural">The unit noun, plural.</param>
    /// <param name="connector">The connector before the subunit.</param>
    /// <param name="subunitSingular">The subunit noun, singular.</param>
    /// <param name="subunitPlural">The subunit noun, plural.</param>
    /// <param name="gender">The gender variant forced on the unit numeral, or <see langword="null"/>.</param>
    /// <returns>The currency.</returns>
    private static CurrencyDefinition Currency(string singular, string plural, string connector, string subunitSingular,
        string subunitPlural, (string Dimension, string Value)? gender = null) => new()
    {
        UnitSingular = singular,
        UnitPlural = plural,
        SubunitSingular = subunitSingular,
        SubunitPlural = subunitPlural,
        Connector = connector,
        UnitForcedVariants = gender is { } g ? ForcedVariantSet.Create((g.Dimension, g.Value)) : ForcedVariantSet.Empty,
    };

    // ===== CA ========================================================================================

    /// <summary>
    /// <c>milió</c> / <c>milions</c>, and the multiplier of <c>milió</c> spans the thousands of millions:
    /// <c>mil milions</c>, <c>dos mil milions</c> (IEC Revista de Tecnologia 10: <c>dos mil milions de dòlars</c>).
    /// </summary>
    [TestMethod]
    [DataRow("CA", 1_000_000L, "un milió")]
    [DataRow("CA", 2_000_000L, "dos milions")]
    [DataRow("CA", 21_000_000L, "vint-i-un milions")]
    [DataRow("CA", 100_000_000L, "cent milions")]
    [DataRow("CA", 2_100_000L, "dos milions cent mil")]
    [DataRow("CA", 1_000_000_000L, "mil milions")]
    [DataRow("CA", 2_000_000_000L, "dos mil milions")]
    [DataRow("CA", 2_500_000_000L, "dos mil cinc-cents milions")]
    [DataRow("CA", 1_001_000_000L, "mil un milions")]
    [DataRow("CA", 1_000_001_000L, "mil milions mil")]
    [DataRow("CA", 1_000_000_001L, "mil milions un")]
    [DataRow("CA", 21_000_000_000L, "vint-i-un mil milions")]
    [DataRow("CA", 999_999_999_999L, "nou-cents noranta-nou mil nou-cents noranta-nou milions nou-cents noranta-nou mil nou-cents noranta-nou")]
    [DataRow("ca-ES", 2_000_000L, "dos milions")]
    [DataRow("ca-ES", 1_000_000_000L, "mil milions")]
    [DataRow("ca-ES-valencia", 2_000_000L, "dos milions")]
    [DataRow("ca-ES-valencia", 1_000_000_000L, "mil milions")]
    [DataRow("ca-ES-valencia", 2_500_000_000L, "dos mil cinc-cents milions")]
    public void Catalan_Cardinals(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(number));

    /// <summary>The feminine caller variant still changes only the gender-variable words, never the scale noun.</summary>
    [TestMethod]
    public void Catalan_Feminine()
    {
        var converter = Get("CA");
        Assert.AreEqual("dos milions", converter.Convert(2_000_000, "gender=femení"));
        Assert.AreEqual("mil milions una", converter.Convert(1_000_000_001, "gender=femení"));
        Assert.AreEqual("dos mil milions dues", converter.Convert(2_000_000_002, "gender=femení"));
        Assert.AreEqual("un milió", converter.Convert(1_000_000, "gender=femení"));
    }

    /// <summary>The cardinals below the million are unchanged.</summary>
    [TestMethod]
    [DataRow(1_000L, "mil")]
    [DataRow(2_000L, "dos mil")]
    [DataRow(999_999L, "nou-cents noranta-nou mil nou-cents noranta-nou")]
    [DataRow(1_000_001L, "un milió un")]
    public void Catalan_BelowAndAtTheMillion_Unchanged(long number, string expected)
        => Assert.AreEqual(expected, Get("CA").Convert(number));

    /// <summary>10^12 stays outside the declared domain.</summary>
    [TestMethod]
    public void Catalan_Domain_Unchanged()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Get("CA").Convert(1_000_000_000_000L));

    /// <summary>A numeral ending on the paired <c>milió</c> keeps the NTS-52 connector: <c>mil milions d'euros</c>.</summary>
    [TestMethod]
    [DataRow("CA", "1000000000", "mil milions d'euros")]
    [DataRow("CA", "2000000000", "dos mil milions d'euros")]
    [DataRow("CA", "2500000000", "dos mil cinc-cents milions d'euros")]
    [DataRow("CA", "2000000", "dos milions d'euros")]
    [DataRow("CA", "1000001000", "mil milions mil euros")]
    [DataRow("ca-ES-valencia", "1000000000", "mil milions d'euros")]
    public void Catalan_Currency(string language, string amount, string expected)
        => Assert.AreEqual(expected, Amount(language, amount, Currency("euro", "euros", "i", "cèntim", "cèntims")));

    // ===== GL ========================================================================================

    /// <summary><c>mil millóns</c>, <c>dous mil millóns</c>: the multiplier of <c>millón</c> is a complete cardinal.</summary>
    [TestMethod]
    [DataRow("GL", 1_000_000L, "un millón")]
    [DataRow("GL", 2_000_000L, "dous millóns")]
    [DataRow("GL", 1_000_000_000L, "mil millóns")]
    [DataRow("GL", 2_000_000_000L, "dous mil millóns")]
    [DataRow("GL", 2_500_000_000L, "dous mil cincocentos millóns")]
    [DataRow("GL", 1_001_000_000L, "mil un millóns")]
    [DataRow("GL", 1_000_001_000L, "mil millóns mil")]
    [DataRow("GL", 1_000_000_001L, "mil millóns un")]
    [DataRow("GL", 21_000_000_000L, "vinte e un mil millóns")]
    [DataRow("gl-ES", 1_000_000_000L, "mil millóns")]
    public void Galician_Cardinals(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(number));

    /// <summary>The feminine caller variant keeps its historical scope.</summary>
    [TestMethod]
    public void Galician_Feminine()
    {
        var converter = Get("GL");
        Assert.AreEqual("mil millóns unha", converter.Convert(1_000_000_001, "gender=feminino"));
        Assert.AreEqual("dous millóns", converter.Convert(2_000_000, "gender=feminino"));
    }

    /// <summary>
    /// <c>millón</c> is masculine (RAG "millón") and governs its multiplier whatever the gender of the counted noun; the
    /// thousands below it keep agreeing.
    /// </summary>
    [TestMethod]
    public void Galician_Feminine_MultiplierOfMillonStaysMasculine()
    {
        var converter = Get("GL");
        Assert.AreEqual("douscentos millóns", converter.Convert(200_000_000, "gender=feminino"));
        Assert.AreEqual("douscentos mil millóns", converter.Convert(200_000_000_000, "gender=feminino"));
        Assert.AreEqual("douscentos millóns douscentas mil", converter.Convert(200_200_000, "gender=feminino"));
        Assert.AreEqual("douscentas mil", converter.Convert(200_000, "gender=feminino"));
        Assert.AreEqual("douscentos millóns", Get("gl-ES").Convert(200_000_000, "gender=feminino"));
    }

    /// <summary>A numeral ending on the paired <c>millón</c> keeps the NTS-52 connector.</summary>
    [TestMethod]
    [DataRow("1000000000", "mil millóns de euros")]
    [DataRow("2000000000", "dous mil millóns de euros")]
    [DataRow("1000000001", "mil millóns un euros")]
    public void Galician_Currency(string amount, string expected)
        => Assert.AreEqual(expected, Amount("GL", amount, Currency("euro", "euros", "e", "céntimo", "céntimos")));

    /// <summary>The ordinal guard of Galician is unchanged (domain 1-1999).</summary>
    [TestMethod]
    public void Galician_Ordinals_Unchanged()
    {
        var converter = Get("GL");
        Assert.AreEqual("milésimo", converter.ConvertOrdinal(1000));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1_000_000_000L));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(2_000_000L));
    }

    /// <summary>
    /// The ordinal of <c>milió</c>: the IEC attests <c>milionè - milionena</c> and <c>milionèsim - milionèsima</c> (GBU
    /// §18.5, quadre 18.6) and prefers the <c>-è/-ena</c> series (GIEC §17.2.5), which the rest of the Catalan series
    /// already uses (<c>milè</c>); the multiples are written on the cardinal (GIEC §17.2.5: <c>cent milionèsim</c>,
    /// <c>mil bilionèsim</c>). The master text (<c>un milióè</c>, <c>dos miliónsè</c>, <c>un mil milióè</c>) was neither.
    /// </summary>
    [TestMethod]
    [DataRow("CA", 1_000_000L, "gender=masculí", "milionè")]
    [DataRow("CA", 2_000_000L, "gender=masculí", "dos milionè")]
    [DataRow("CA", 100_000_000L, "gender=masculí", "cent milionè")]
    [DataRow("CA", 21_000_000L, "gender=masculí", "vint-i-un milionè")]
    [DataRow("CA", 200_000_000L, "gender=masculí", "dos-cents milionè")]
    [DataRow("CA", 1_000_000_000L, "gender=masculí", "mil milionè")]
    [DataRow("CA", 2_500_000_000L, "gender=masculí", "dos mil cinc-cents milionè")]
    [DataRow("CA", 1_000_000L, "gender=femení", "milionena")]
    [DataRow("CA", 2_000_000L, "gender=femení", "dos milionena")]
    [DataRow("CA", 1_000_000_000L, "gender=femení", "mil milionena")]
    [DataRow("ca-ES-valencia", 1_000_000L, "gender=masculí", "milionè")]
    [DataRow("ca-ES-valencia", 1_000_000_000L, "gender=femení", "mil milionena")]
    public void Catalan_Ordinals_Millions(string language, long number, string variant, string expected)
        => Assert.AreEqual(expected, Get(language).ConvertOrdinal(number, variant));

    /// <summary>
    /// <c>milió</c> is masculine and governs its multiplier whatever the gender of the counted noun (GEIEC §12.3.2:
    /// <c>una dos-cents mil milionèsima (part) de segon</c>): the feminine falls on the ordinal suffix, never on
    /// <c>dos-cents</c>; the thousands below the million keep agreeing with the counted noun.
    /// </summary>
    [TestMethod]
    public void Catalan_Feminine_MultiplierOfMilioStaysMasculine()
    {
        var converter = Get("CA");
        Assert.AreEqual("dos-cents milionena", converter.ConvertOrdinal(200_000_000, "gender=femení"));
        Assert.AreEqual("dos-cents mil milionena", converter.ConvertOrdinal(200_000_000_000, "gender=femení"));
        Assert.AreEqual("dos-cents milions", converter.Convert(200_000_000, "gender=femení"));
        Assert.AreEqual("dos-cents un milions", converter.Convert(201_000_000, "gender=femení"));
        Assert.AreEqual("dos-cents mil milions", converter.Convert(200_000_000_000, "gender=femení"));
        Assert.AreEqual("dos-cents milions dues-centes mil", converter.Convert(200_200_000, "gender=femení"));
        Assert.AreEqual("dues-centes mil", converter.Convert(200_000, "gender=femení"));
        Assert.AreEqual("dues-centes", converter.Convert(200, "gender=femení"));
        Assert.AreEqual("dos-cents milions de pessetes", Amount("CA", "200000000",
            Currency("pesseta", "pessetes", "i", "cèntim", "cèntims", ("gender", "femení"))));
        Assert.AreEqual("dos-cents milions", Get("ca-ES-valencia").Convert(200_000_000, "gender=femení"));
    }

    /// <summary>The other Catalan ordinals keep their master output.</summary>
    [TestMethod]
    public void Catalan_Ordinals_Unchanged()
    {
        var converter = Get("CA");
        Assert.AreEqual("milè", converter.ConvertOrdinal(1000));
        Assert.AreEqual("dos milè", converter.ConvertOrdinal(2000));
        Assert.AreEqual("mil unè", converter.ConvertOrdinal(1001));
        Assert.AreEqual("milena", converter.ConvertOrdinal(1000, "gender=femení"));
        Assert.AreEqual("un milió unè", converter.ConvertOrdinal(1_000_001));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
    }

    // ===== RO scale connector ========================================================================

    /// <summary>
    /// GALR: no <c>de</c> after a multiplier from 1 to 19 or a compound ending on 1 to 19 (last two digits), <c>de</c>
    /// after the other multipliers from twenty. Only multipliers whose spelling does not depend on gender are pinned.
    /// </summary>
    [TestMethod]
    [DataRow("RO", 19_000L, "nouăsprezece mii")]
    [DataRow("RO", 20_000L, "douăzeci de mii")]
    [DataRow("RO", 100_000L, "o sută de mii")]
    [DataRow("RO", 103_000L, "o sută trei mii")]
    [DataRow("RO", 113_000L, "o sută treisprezece mii")]
    [DataRow("RO", 119_000L, "o sută nouăsprezece mii")]
    [DataRow("RO", 120_000L, "o sută douăzeci de mii")]
    [DataRow("RO", 133_000L, "o sută treizeci și trei de mii")]
    [DataRow("RO", 219_000L, "două sute nouăsprezece mii")]
    [DataRow("RO", 999_000L, "nouă sute nouăzeci și nouă de mii")]
    [DataRow("RO", 103_000_000L, "o sută trei milioane")]
    [DataRow("RO", 119_000_000L, "o sută nouăsprezece milioane")]
    [DataRow("RO", 120_000_000L, "o sută douăzeci de milioane")]
    [DataRow("RO", 133_000_000L, "o sută treizeci și trei de milioane")]
    [DataRow("RO", 105_000_000_000L, "o sută cinci miliarde")]
    [DataRow("RO", 119_000_000_000L, "o sută nouăsprezece miliarde")]
    [DataRow("RO", 120_000_000_000L, "o sută douăzeci de miliarde")]
    [DataRow("RO", 2_500_000_000L, "două miliarde cinci sute de milioane")]
    [DataRow("RO", 119_119_119L, "o sută nouăsprezece milioane o sută nouăsprezece mii o sută nouăsprezece")]
    [DataRow("RO-RO", 119_000L, "o sută nouăsprezece mii")]
    [DataRow("RO-RO", 120_000L, "o sută douăzeci de mii")]
    public void Romanian_ScaleConnector(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(number));

    /// <summary>
    /// The compounds ending on one or two (whose multiplier agreement is not in the scope of NTS-53) take no <c>de</c>;
    /// those from twenty without such an ending take it.
    /// </summary>
    [TestMethod]
    [DataRow(101_000L, false)]
    [DataRow(102_000L, false)]
    [DataRow(201_000L, false)]
    [DataRow(101_000_000L, false)]
    [DataRow(21_000L, true)]
    [DataRow(132_000L, true)]
    [DataRow(121_000_000L, true)]
    public void Romanian_ScaleConnector_CompoundsEndingOnOneOrTwo(long number, bool takesDe)
    {
        string text = Get("RO").Convert(number);
        Assert.AreEqual(takesDe, text.Contains(" de ", StringComparison.Ordinal), text);
    }

    /// <summary>The rule depends on the multiplier, never on the caller's gender.</summary>
    [TestMethod]
    public void Romanian_ScaleConnector_Feminine()
    {
        var converter = Get("RO");
        Assert.AreEqual("o sută nouăsprezece mii", converter.Convert(119_000, "gen=feminin"));
        Assert.AreEqual("o sută douăzeci de mii", converter.Convert(120_000, "gen=feminin"));
    }

    /// <summary>The public legacy getters keep their master values.</summary>
    [TestMethod]
    public void Romanian_LegacyGetters_Unchanged()
    {
        Assert.AreEqual("de", Get("RO").ScaleConnector);
        Assert.AreEqual(20, Get("RO").ScaleConnectorThreshold);
        Assert.AreEqual("de", Get("RO-RO").ScaleConnector);
        Assert.AreEqual(20, Get("RO-RO").ScaleConnectorThreshold);
        Assert.IsNull(Get("CA").ScaleConnector);
        Assert.IsNull(Get("ES").ScaleConnector);
        Assert.IsNull(Get("CA").ScaleConnectorRules);
        var rules = Get("RO").ScaleConnectorRules!;
        Assert.AreEqual("", rules.Default);
        Assert.AreEqual(1, rules.Substitutions.Count);
        Assert.AreEqual("", rules.Substitutions[0].Value);
        Assert.AreEqual(100L, rules.Substitutions[0].NumberModulo);
    }

    /// <summary>A clone of the Romanian converter keeps the getters and the output.</summary>
    [TestMethod]
    public void Romanian_Clone()
    {
        var clone = new NumberToStringConverter(NumberToStringConverterOptions.FromCulture("RO"));
        Assert.AreEqual("de", clone.ScaleConnector);
        Assert.AreEqual(20, clone.ScaleConnectorThreshold);
        Assert.AreSame(Get("RO").ScaleConnectorRules, clone.ScaleConnectorRules);
        foreach (long n in new long[] { 19_000, 20_000, 101_000, 119_000, 120_000, 133_000, 119_000_000, 2_500_000_000 })
            Assert.AreEqual(Get("RO").Convert(n), clone.Convert(n), n.ToString(CultureInfo.InvariantCulture));
        Assert.AreEqual("un euro", clone.ConvertCurrency(1m, RomanianEuro));
    }

    // ===== RO currency one ===========================================================================

    /// <summary>A neutral, invariable caller-defined masculine/neuter currency.</summary>
    private static readonly CurrencyDefinition RomanianEuro = Currency("euro", "euro", "și", "cent", "cenți");

    /// <summary>Romanian leu.</summary>
    private static readonly CurrencyDefinition Leu = Currency("leu", "lei", "și", "ban", "bani");

    /// <summary>A caller-defined feminine currency (the pound): its unit numeral takes the feminine.</summary>
    private static readonly CurrencyDefinition Lira = Currency("liră", "lire", "și", "penny", "pence", ("gen", "feminin"));

    /// <summary>
    /// One before a currency noun is attributive, <c>un</c> (DEX, "unu": "când însoțește un substantiv are forma un,
    /// o"), while a compound keeps <c>unu</c> (DOOM 3, §4.1.5: <c>douăzeci și unu</c>, <c>o sută unu</c>).
    /// </summary>
    [TestMethod]
    [DataRow("1", "un euro")]
    [DataRow("21", "douăzeci și unu de euro")]
    [DataRow("101", "o sută unu euro")]
    [DataRow("119", "o sută nouăsprezece euro")]
    [DataRow("120", "o sută douăzeci de euro")]
    [DataRow("1001", "o mie unu euro")]
    [DataRow("1000", "o mie de euro")]
    [DataRow("1000000", "un milion de euro")]
    [DataRow("1.01", "un euro și un cent")]
    [DataRow("21.21", "douăzeci și unu de euro și douăzeci și unu de cenți")]
    [DataRow("120000", "o sută douăzeci de mii de euro")]
    [DataRow("119000", "o sută nouăsprezece mii de euro")]
    [DataRow("119019", "o sută nouăsprezece mii nouăsprezece euro")]
    public void Romanian_Currency_Masculine(string amount, string expected)
        => Assert.AreEqual(expected, Amount("RO", amount, RomanianEuro));

    /// <summary><c>un leu</c>, <c>un ban</c>.</summary>
    [TestMethod]
    public void Romanian_Currency_Leu()
    {
        Assert.AreEqual("un leu", Amount("RO", "1", Leu));
        Assert.AreEqual("un leu și un ban", Amount("RO", "1.01", Leu));
        Assert.AreEqual("un leu", Amount("RO-RO", "1", Leu));
    }

    /// <summary>
    /// Before a feminine noun one is <c>o</c> (DEX, "unu"); a compound keeps the form the cardinal pipeline gives its
    /// last word (its agreement is not in the scope of NTS-53): the replacement never reaches it.
    /// </summary>
    [TestMethod]
    public void Romanian_Currency_Feminine()
    {
        Assert.AreEqual("o liră", Amount("RO", "1", Lira));
        Assert.AreEqual("o liră și un penny", Amount("RO", "1.01", Lira));
        Assert.AreEqual("două lire", Amount("RO", "2", Lira));
        foreach (var (amount, cardinal) in new[] { ("21", 21), ("101", 101) })
        {
            string expectedNumeral = Get("RO").Convert(cardinal, "gen=feminin");
            StringAssert.StartsWith(Amount("RO", amount, Lira), expectedNumeral + " ");
        }
    }

    /// <summary>The caller's feminine variant also gives <c>o</c> before the noun.</summary>
    [TestMethod]
    public void Romanian_Currency_CallerFeminine()
        => Assert.AreEqual("o liră",
            Get("RO").ConvertCurrency(1m, Currency("liră", "lire", "și", "penny", "pence"), "gen=feminin"));

    /// <summary>The standalone cardinal and the other surfaces are unchanged.</summary>
    [TestMethod]
    public void Romanian_OtherSurfaces_Unchanged()
    {
        var converter = Get("RO");
        Assert.AreEqual("unu", converter.Convert(1));
        Assert.AreEqual("una", converter.Convert(1, "gen=feminin"));
        Assert.AreEqual("douăzeci și unu", converter.Convert(21));
        Assert.AreEqual("o sută unu", converter.Convert(101));
        Assert.AreEqual("o mie unu", converter.Convert(1001));
        Assert.AreEqual("un milion", converter.Convert(1_000_000));
        Assert.AreEqual("ora unu", converter.ConvertClockTime(new TimeOnly(1, 0)));
    }
}
