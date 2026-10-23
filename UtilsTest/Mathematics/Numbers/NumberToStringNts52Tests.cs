using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-52, production behaviour of the currency noun connector (<c>&lt;Currency&gt;&lt;NounConnector&gt;</c>):
/// <list type="bullet">
/// <item><description>
/// FR (and FR-fr, FR-ca, FR-be, FR-ch): <c>de</c> after a numeral ending on <c>million</c>, <c>milliard</c> …, elided
/// <c>d'</c> before a vowel (Académie française, "million", "milliard"; OQLF, "Écriture de million et de milliard").
/// </description></item>
/// <item><description>
/// CA (and ca-ES, ca-ES-valencia): the same, elided before a vowel or a mute h, never before a consonantal i or u (UOC,
/// "Escriptura dels numerals", "L'apòstrof").
/// </description></item>
/// <item><description>GL (and gl-ES): <c>de</c>, never elided (Real Academia Galega, "millón").</description></item>
/// <item><description>
/// RO (and RO-RO): <c>de</c> from 20, except when the last two digits are 1 to 19 (GALR 2005, vol. I, pp. 296-297).
/// </description></item>
/// <item><description>PT: attested (Priberam, "milhão") but outside the cardinal domain, unchanged.</description></item>
/// <item><description>ES and IT: kept on the NTS-51 shorthand (public getters unchanged), identical output.</description></item>
/// </list>
/// Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-52 section.
/// </summary>
[TestClass]
public class NumberToStringNts52Tests
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
    private static CurrencyDefinition Currency(string singular, string plural, string connector, string subunitSingular = "cent",
        string subunitPlural = "cents", (string Dimension, string Value)? gender = null) => new()
    {
        UnitSingular = singular,
        UnitPlural = plural,
        SubunitSingular = subunitSingular,
        SubunitPlural = subunitPlural,
        Connector = connector,
        UnitForcedVariants = gender is { } g ? ForcedVariantSet.Create((g.Dimension, g.Value)) : ForcedVariantSet.Empty,
    };

    // ===== FR ========================================================================================

    /// <summary>French euro.</summary>
    private static readonly CurrencyDefinition FrenchEuro = Currency("euro", "euros", "et", "centime", "centimes");

    /// <summary>French franc.</summary>
    private static readonly CurrencyDefinition FrenchFranc = Currency("franc", "francs", "et", "centime", "centimes");

    /// <summary><c>de</c> / <c>d'</c> after a numeral ending on a scale noun; the NTS-49 hyphenated numeral is kept.</summary>
    [TestMethod]
    [DataRow("FR", "1000000", "un-million d'euros")]
    [DataRow("FR", "2000000", "deux-millions d'euros")]
    [DataRow("FR", "1000000000", "un-milliard d'euros")]
    [DataRow("FR", "2500000000", "deux-milliards-cinq-cents-millions d'euros")]
    [DataRow("FR", "1000000.50", "un-million d'euros et cinquante centimes")]
    [DataRow("FR", "-2000000", "moins deux-millions d'euros")]
    [DataRow("FR", "1200000", "un-million-deux-cent-mille euros")]
    [DataRow("FR", "1000001", "un-million-un euros")]
    [DataRow("FR", "1001000", "un-million-mille euros")]
    [DataRow("FR", "1000", "mille euros")]
    [DataRow("FR", "1", "un euro")]
    [DataRow("FR-fr", "1000000", "un-million d'euros")]
    [DataRow("FR-ca", "2000000", "deux-millions d'euros")]
    [DataRow("FR-be", "1000000", "un-million d'euros")]
    [DataRow("FR-ch", "2000000", "deux-millions d'euros")]
    [DataRow("FR-be", "71000000", "septante-et-un-millions d'euros")]
    public void French_Euro(string language, string amount, string expected)
        => Assert.AreEqual(expected, Amount(language, amount, FrenchEuro));

    /// <summary>Before a consonant the preposition is not elided.</summary>
    [TestMethod]
    [DataRow("FR", "1000000", "un-million de francs")]
    [DataRow("FR", "2000000", "deux-millions de francs")]
    [DataRow("FR", "1200000", "un-million-deux-cent-mille francs")]
    [DataRow("FR-ch", "1000000", "un-million de francs")]
    [DataRow("FR-be", "2000000", "deux-millions de francs")]
    public void French_Franc(string language, string amount, string expected)
        => Assert.AreEqual(expected, Amount(language, amount, FrenchFranc));

    /// <summary>
    /// Accented vowels elide too (<c>d'écus</c>); <c>y</c> and <c>h</c> are not listed (<c>de yens</c>; a mute and an
    /// aspirated h cannot be told apart), and a feminine currency keeps the masculine noun <c>million</c>.
    /// </summary>
    [TestMethod]
    public void French_Onsets_And_Gender()
    {
        Assert.AreEqual("un-million d'écus", Amount("FR", "1000000", Currency("écu", "écus", "et")));
        Assert.AreEqual("un-million d'Euros", Amount("FR", "1000000", Currency("Euro", "Euros", "et")));
        Assert.AreEqual("un-million de yens", Amount("FR", "1000000", Currency("yen", "yens", "et")));
        Assert.AreEqual("un-million de hryvnias", Amount("FR", "1000000", Currency("hryvnia", "hryvnias", "et")));
        var livre = Currency("livre", "livres", "et", "penny", "pence", ("gender", "feminin"));
        Assert.AreEqual("une livre", Amount("FR", "1", livre));
        Assert.AreEqual("vingt-et-une livres", Amount("FR", "21", livre));
        Assert.AreEqual("un-million de livres", Amount("FR", "1000000", livre));
    }

    /// <summary>The connector belongs to the currency phrase only.</summary>
    [TestMethod]
    public void French_OtherSurfaces_Unchanged()
    {
        var converter = Get("FR");
        Assert.AreEqual("un-million", converter.Convert(1_000_000));
        Assert.AreEqual("deux-millions", converter.Convert(2_000_000));
        Assert.AreEqual("millionième", converter.ConvertOrdinal(1_000_000));
        Assert.AreEqual(2, converter.CurrencyNounConnector!.Substitutions.Count);
        Assert.IsNull(converter.CurrencyTerminalScaleConnector);
    }

    // ===== CA ========================================================================================

    /// <summary>Catalan euro.</summary>
    private static readonly CurrencyDefinition CatalanEuro = Currency("euro", "euros", "i", "cèntim", "cèntims");

    /// <summary>Catalan peseta (feminine).</summary>
    private static readonly CurrencyDefinition Pesseta = Currency("pesseta", "pessetes", "i", "cèntim", "cèntims", ("gender", "femení"));

    /// <summary><c>de</c> / <c>d'</c> after a numeral ending on <c>milió</c>, for each registered culture.</summary>
    [TestMethod]
    [DataRow("CA")]
    [DataRow("ca-ES")]
    [DataRow("ca-ES-valencia")]
    public void Catalan(string language)
    {
        Assert.AreEqual("un milió d'euros", Amount(language, "1000000", CatalanEuro));
        Assert.AreEqual("un milió de pessetes", Amount(language, "1000000", Pesseta));
        Assert.AreEqual("un milió dos-cents mil euros", Amount(language, "1200000", CatalanEuro));
        Assert.AreEqual("un milió un euros", Amount(language, "1000001", CatalanEuro));
        Assert.AreEqual("un milió mil euros", Amount(language, "1001000", CatalanEuro));
        Assert.AreEqual("mil euros", Amount(language, "1000", CatalanEuro));
        Assert.AreEqual("un euro", Amount(language, "1", CatalanEuro));
        Assert.AreEqual("una pesseta", Amount(language, "1", Pesseta));
        // The plural numeral itself is the subject of NTS-53 (milións): only the connector is pinned here.
        StringAssert.EndsWith(Amount(language, "2000000", CatalanEuro), " d'euros");
        StringAssert.EndsWith(Amount(language, "2000000", Pesseta), " de pessetes");
    }

    /// <summary>
    /// Elided before a mute h, not before a consonantal i or u (UOC: <c>de iens</c>, <c>de iod</c>, <c>la hiena</c>,
    /// <c>el uombat</c>): the consonantal onsets are declared first and win at equal specificity.
    /// </summary>
    [TestMethod]
    public void Catalan_Onsets()
    {
        Assert.AreEqual("un milió d'hores", Amount("CA", "1000000", Currency("hora", "hores", "i")));
        Assert.AreEqual("un milió de iens", Amount("CA", "1000000", Currency("ien", "iens", "i")));
        Assert.AreEqual("un milió de hienes", Amount("CA", "1000000", Currency("hiena", "hienes", "i")));
        Assert.AreEqual("un milió de uombats", Amount("CA", "1000000", Currency("uombat", "uombats", "i")));
        Assert.AreEqual("un milió de iods", Amount("CA", "1000000", Currency("iod", "iods", "i")));
        Assert.AreEqual("un milió d'àligues", Amount("CA", "1000000", Currency("àliga", "àligues", "i")));
        Assert.AreEqual("un milió d'unces", Amount("CA", "1000000", Currency("unça", "unces", "i")));
    }

    // ===== GL ========================================================================================

    /// <summary>Galician euro.</summary>
    private static readonly CurrencyDefinition GalicianEuro = Currency("euro", "euros", "e", "céntimo", "céntimos");

    /// <summary><c>de</c> after a numeral ending on <c>millón</c>, never elided.</summary>
    [TestMethod]
    [DataRow("GL")]
    [DataRow("gl-ES")]
    public void Galician(string language)
    {
        Assert.AreEqual("un millón de euros", Amount(language, "1000000", GalicianEuro));
        Assert.AreEqual("dous millóns de euros", Amount(language, "2000000", GalicianEuro));
        Assert.AreEqual("un millón douscentos mil euros", Amount(language, "1200000", GalicianEuro));
        Assert.AreEqual("un millón un euros", Amount(language, "1000001", GalicianEuro));
        Assert.AreEqual("mil euros", Amount(language, "1000", GalicianEuro));
        Assert.AreEqual("un euro", Amount(language, "1", GalicianEuro));
        Assert.AreEqual("un millón de euros e cincuenta céntimos", Amount(language, "1000000.50", GalicianEuro));
    }

    // ===== RO ========================================================================================

    /// <summary>A neutral, invariable caller-defined Romanian currency.</summary>
    private static readonly CurrencyDefinition RomanianEuro = Currency("euro", "euro", "și", "cent", "cenți");

    /// <summary>Romanian leu.</summary>
    private static readonly CurrencyDefinition Leu = Currency("leu", "lei", "și", "ban", "bani");

    /// <summary>GALR: <c>de</c> from twenty, except after a compound ending on one to nineteen.</summary>
    [TestMethod]
    [DataRow("RO", "2", "doi euro")]
    [DataRow("RO", "19", "nouăsprezece euro")]
    [DataRow("RO", "20", "douăzeci de euro")]
    [DataRow("RO", "21", "douăzeci și unu de euro")]
    [DataRow("RO", "99", "nouăzeci și nouă de euro")]
    [DataRow("RO", "100", "o sută de euro")]
    [DataRow("RO", "101", "o sută unu euro")]
    [DataRow("RO", "119", "o sută nouăsprezece euro")]
    [DataRow("RO", "120", "o sută douăzeci de euro")]
    [DataRow("RO", "1000", "o mie de euro")]
    [DataRow("RO", "1001", "o mie unu euro")]
    [DataRow("RO", "5200", "cinci mii două sute de euro")]
    [DataRow("RO", "1000000", "un milion de euro")]
    [DataRow("RO", "1000001", "un milion unu euro")]
    [DataRow("RO", "2000308", "două milioane trei sute opt euro")]
    [DataRow("RO", "0", "zero euro")]
    [DataRow("RO-RO", "20", "douăzeci de euro")]
    [DataRow("RO-RO", "101", "o sută unu euro")]
    public void Romanian(string language, string amount, string expected)
        => Assert.AreEqual(expected, Amount(language, amount, RomanianEuro));

    /// <summary>
    /// The numeral of one before a noun is the subject of NTS-53: only the absence of a connector is pinned.
    /// </summary>
    [TestMethod]
    public void Romanian_One_NoConnector()
    {
        string text = Amount("RO", "1", RomanianEuro);
        StringAssert.EndsWith(text, " euro");
        Assert.IsFalse(text.Contains(" de ", StringComparison.Ordinal), text);
    }

    /// <summary>The unit and the subunit are decided on their own counts.</summary>
    [TestMethod]
    public void Romanian_UnitAndSubunit()
    {
        Assert.AreEqual("doi lei și douăzeci de bani", Amount("RO", "2.20", Leu));
        Assert.AreEqual("douăzeci de lei și doi bani", Amount("RO", "20.02", Leu));
        Assert.AreEqual("o sută unu lei și nouăsprezece bani", Amount("RO", "101.19", Leu));
        Assert.AreEqual("o sută douăzeci de lei", Amount("RO", "120", Leu));
        Assert.AreEqual("o sută nouăsprezece lei", Amount("RO", "119", Leu));
        Assert.AreEqual("cinci mii două sute de lei", Amount("RO", "5200", Leu));
        Assert.AreEqual("două milioane trei sute opt lei", Amount("RO", "2000308", Leu));
    }

    /// <summary>The connector is distinct from scaleConnector, which acts inside the numeral.</summary>
    [TestMethod]
    public void Romanian_OtherSurfaces_Unchanged()
    {
        var converter = Get("RO");
        Assert.AreEqual("douăzeci", converter.Convert(20));
        Assert.AreEqual("douăzeci de mii", converter.Convert(20_000));
        Assert.AreEqual("douăzeci de mii de euro", Amount("RO", "20000", RomanianEuro));
    }

    // ===== PT ========================================================================================

    /// <summary>
    /// Portuguese writes <c>um milhão de</c> (Priberam), but its cardinal domain stops at 999 999: nothing is configured
    /// and every supported amount is unchanged.
    /// </summary>
    [TestMethod]
    public void Portuguese_OutOfDomain_Unchanged()
    {
        var converter = Get("PT");
        var euro = Currency("euro", "euros", "e", "cêntimo", "cêntimos");
        Assert.IsNull(converter.CurrencyNounConnector);
        Assert.AreEqual(999_999L, converter.MaxNumber);
        Assert.AreEqual("mil euros", Amount("PT", "1000", euro));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Amount("PT", "1000000", euro));
    }

    // ===== ES, IT (NTS-51 shorthand) =================================================================

    /// <summary>
    /// Spanish and Italian keep the NTS-51 shorthand, so its public getters keep their #657 values (and so do the
    /// clones); the shorthand runs through the same noun-connector engine, with the NTS-51 output.
    /// </summary>
    [TestMethod]
    [DataRow("ES", "de")]
    [DataRow("IT", "di")]
    public void Spanish_Italian_LegacyShorthand(string language, string connector)
    {
        var converter = Get(language);
        Assert.AreEqual(connector, converter.CurrencyTerminalScaleConnector);
        Assert.AreEqual("2..", converter.CurrencyTerminalScales);
        Assert.IsNull(converter.CurrencyNounConnector);
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual(connector, clone.CurrencyTerminalScaleConnector);
        Assert.AreEqual("2..", clone.CurrencyTerminalScales);
        Assert.IsNull(clone.CurrencyNounConnector);
    }

    /// <summary>The Spanish and Italian currency phrases are those of NTS-51.</summary>
    [TestMethod]
    public void Spanish_Italian_Output()
    {
        var spanishEuro = Currency("euro", "euros", "con", "céntimo", "céntimos");
        var italianEuro = Currency("euro", "euro", "e", "centesimo", "centesimi");
        Assert.AreEqual("un millón de euros", Amount("ES", "1000000", spanishEuro));
        Assert.AreEqual("mil millones de euros", Amount("ES", "1000000000", spanishEuro));
        Assert.AreEqual("un millón doscientos mil euros", Amount("ES", "1200000", spanishEuro));
        Assert.AreEqual("un millón un euros", Amount("ES", "1000001", spanishEuro));
        Assert.AreEqual("un milione di euro", Amount("IT", "1000000", italianEuro));
        Assert.AreEqual("due miliardi di euro", Amount("IT", "2000000000", italianEuro));
        Assert.AreEqual("un milione e uno euro", Amount("IT", "1000001", italianEuro));
        Assert.AreEqual("un euro", Amount("IT", "1", italianEuro));
    }
}
