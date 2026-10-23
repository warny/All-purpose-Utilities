using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-50 and NTS-51, production behaviour.
/// <list type="number">
/// <item><description>
/// NTS-50: the Basque ordinal of zero, <c>zerogarren</c> (Euskaltzaindia rule 203 §5, "0. zerogarren"), formed by the
/// general rule (the cardinal <c>zero</c> + <c>-garren</c>).
/// </description></item>
/// <item><description>
/// NTS-51/ES: a numeral ending on the noun <c>millón</c> introduces the counted noun with <c>de</c> (DPD
/// "cardinales" §6: <c>un millón de personas</c>, <c>dos billones de pesos</c>, but <c>un millón doscientas mil
/// personas</c>).
/// </description></item>
/// <item><description>
/// NTS-51/IT: <c>uno</c> alone before a masculine noun is <c>un</c> under the rules of the article <c>il/lo</c>
/// (Treccani, "uno": <c>un uomo</c>, <c>uno scolaro</c>); its compounds stay invariable (Treccani, "uno, numerali
/// composti con": <c>ventuno banchi</c>, <c>un milione e uno cittadini</c>).
/// </description></item>
/// </list>
/// Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-50/NTS-51 section.
/// </summary>
[TestClass]
public class NumberToStringNts50Nts51Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>A caller-defined masculine Spanish currency.</summary>
    private static readonly CurrencyDefinition Euro = new()
    {
        UnitSingular = "euro",
        UnitPlural = "euros",
        SubunitSingular = "céntimo",
        SubunitPlural = "céntimos",
        Connector = "con",
    };

    /// <summary>A caller-defined feminine Spanish currency.</summary>
    private static readonly CurrencyDefinition Peseta = new()
    {
        UnitSingular = "peseta",
        UnitPlural = "pesetas",
        SubunitSingular = "céntimo",
        SubunitPlural = "céntimos",
        Connector = "con",
        UnitForcedVariants = ForcedVariantSet.Create(("gender", "femenino")),
    };

    /// <summary>Builds a caller-defined Italian currency with invariable nouns.</summary>
    /// <param name="unit">The unit noun.</param>
    /// <param name="subunit">The subunit noun, singular.</param>
    /// <param name="subunitPlural">The subunit noun, plural.</param>
    /// <returns>The currency.</returns>
    private static CurrencyDefinition Italian(string unit, string subunit = "centesimo", string subunitPlural = "centesimi") => new()
    {
        UnitSingular = unit,
        UnitPlural = unit,
        SubunitSingular = subunit,
        SubunitPlural = subunitPlural,
        Connector = "e",
    };

    // ===== NTS-50: Basque zero ordinal ===============================================================

    /// <summary>Rule 203 §5 lists "0. zerogarren"; the general rule forms it from the cardinal.</summary>
    [TestMethod]
    [DataRow("EU")]
    [DataRow("eu-ES")]
    public void Basque_ZeroOrdinal(string language)
    {
        var converter = Get(language);
        Assert.AreEqual("zerogarren", converter.ConvertOrdinal(0));
        Assert.AreEqual("zero", converter.Convert(0));
        Assert.AreEqual("lehenengo", converter.ConvertOrdinal(1));
    }

    /// <summary>The upper bound of the domain is unchanged.</summary>
    [TestMethod]
    [DataRow("EU")]
    [DataRow("eu-ES")]
    public void Basque_OrdinalDomainBounds(string language)
    {
        var converter = Get(language);
        Assert.AreEqual("bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi milioi bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzigarren",
            converter.ConvertOrdinal(999_999_999_999));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1_000_000_000_000));
    }

    // ===== NTS-51/ES: de after a numeral ending on millón ============================================

    /// <summary>DPD "cardinales" §6 and "uno" §2.2, with the apocope of NTS-30 kept.</summary>
    [TestMethod]
    [DataRow("1", "un euro")]
    [DataRow("21", "veintiún euros")]
    [DataRow("31", "treinta y un euros")]
    [DataRow("1000", "mil euros")]
    [DataRow("21000", "veintiún mil euros")]
    [DataRow("1000000", "un millón de euros")]
    [DataRow("2000000", "dos millones de euros")]
    [DataRow("21000000", "veintiún millones de euros")]
    [DataRow("1200000", "un millón doscientos mil euros")]
    [DataRow("1000001", "un millón un euros")]
    [DataRow("2000001", "dos millones un euros")]
    [DataRow("1000000000", "mil millones de euros")]
    [DataRow("2500000000", "dos mil quinientos millones de euros")]
    [DataRow("2500000001", "dos mil quinientos millones un euros")]
    [DataRow("1000000.50", "un millón de euros con cincuenta céntimos")]
    [DataRow("-2000000", "menos dos millones de euros")]
    public void Spanish_Currency_De(string amount, string expected)
        => Assert.AreEqual(expected, Get("ES").ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Euro));

    /// <summary>
    /// <c>millón</c> stays masculine before a feminine counted noun (NTS-30G); the DPD example of a complex numeral,
    /// <c>un millón doscientas mil personas</c>, has no <c>de</c>.
    /// </summary>
    [TestMethod]
    [DataRow("1", "una peseta")]
    [DataRow("1000000", "un millón de pesetas")]
    [DataRow("21000000", "veintiún millones de pesetas")]
    [DataRow("1200000", "un millón doscientas mil pesetas")]
    [DataRow("2000000000", "dos mil millones de pesetas")]
    public void Spanish_Currency_De_Feminine(string amount, string expected)
        => Assert.AreEqual(expected, Get("ES").ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Peseta));

    /// <summary>The connector belongs to the currency phrase only: the cardinal and the ordinal are unchanged.</summary>
    [TestMethod]
    public void Spanish_OtherSurfaces_Unchanged()
    {
        var converter = Get("ES");
        Assert.AreEqual("un millón", converter.Convert(1_000_000));
        Assert.AreEqual("dos millones", converter.Convert(2_000_000));
        Assert.AreEqual("mil millones", converter.Convert(1_000_000_000));
        Assert.AreEqual("millonésimo", converter.ConvertOrdinal(1_000_000));
    }

    // ===== NTS-51/IT: un before a masculine currency noun ============================================

    /// <summary><c>uno</c> alone is <c>un</c> before a noun taking the article <c>il</c>, for the unit and the subunit.</summary>
    [TestMethod]
    [DataRow("1", "euro", "un euro")]
    [DataRow("1.50", "euro", "un euro e cinquanta centesimi")]
    [DataRow("2.01", "euro", "due euro e un centesimo")]
    [DataRow("1.01", "euro", "un euro e un centesimo")]
    [DataRow("1", "dollaro", "un dollaro")]
    [DataRow("1", "franco", "un franco")]
    [DataRow("2", "euro", "due euro")]
    public void Italian_Currency_Un(string amount, string unit, string expected)
        => Assert.AreEqual(expected, Get("IT").ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Italian(unit)));

    /// <summary>
    /// Before a noun taking the article <c>lo</c> (s + consonant, z, y …), <c>uno</c> stays: <c>uno scolaro</c>, <c>uno
    /// zoccolo</c> (Treccani, "uno").
    /// </summary>
    [TestMethod]
    [DataRow("scellino", "uno scellino")]
    [DataRow("zloty", "uno zloty")]
    [DataRow("yen", "uno yen")]
    [DataRow("Zloty", "uno Zloty")]
    public void Italian_Currency_UnoBeforeLoNouns(string unit, string expected)
        => Assert.AreEqual(expected, Get("IT").ConvertCurrency(1m, Italian(unit)));

    /// <summary>The compounds of <c>uno</c> stay invariable (Treccani: <c>ventuno banchi</c>, <c>un milione e uno cittadini</c>).</summary>
    [TestMethod]
    [DataRow("21", "ventuno euro")]
    [DataRow("101", "centouno euro")]
    [DataRow("1001", "milleuno euro")]
    [DataRow("1000001", "un milione e uno euro")]
    public void Italian_Currency_CompoundsInvariable(string amount, string expected)
        => Assert.AreEqual(expected, Get("IT").ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Italian("euro")));

    /// <summary>A feminine currency keeps <c>una</c>.</summary>
    [TestMethod]
    public void Italian_Currency_Feminine()
    {
        var lira = new CurrencyDefinition
        {
            UnitSingular = "lira", UnitPlural = "lire", SubunitSingular = "centesimo", SubunitPlural = "centesimi", Connector = "e",
            UnitForcedVariants = ForcedVariantSet.Create(("gender", "femminile")),
        };
        Assert.AreEqual("una lira", Get("IT").ConvertCurrency(1m, lira));
        Assert.AreEqual("una lira e un centesimo", Get("IT").ConvertCurrency(1.01m, lira));
    }

    /// <summary>The replacement belongs to the currency phrase only.</summary>
    [TestMethod]
    public void Italian_OtherSurfaces_Unchanged()
    {
        var converter = Get("IT");
        Assert.AreEqual("uno", converter.Convert(1));
        Assert.AreEqual("ventuno", converter.Convert(21));
        Assert.AreEqual("un milione e uno", converter.Convert(1_000_001));
        Assert.AreEqual("primo", converter.ConvertOrdinal(1));
        Assert.AreEqual("ventunesimo", converter.ConvertOrdinal(21));
        Assert.AreEqual("una", converter.Convert(1, "gender=femminile"));
    }
}
