using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30 (last two items) and NTS-38, production behaviour.
/// <list type="number">
/// <item><description>
/// VN: a whole zero class between two non-zero classes is read <c>không</c> + class name (SGK Toán 4, Chân trời sáng
/// tạo, Bài 25: <c>307 000 262</c> "Ba trăm linh bảy triệu không nghìn hai trăm sáu mươi hai").
/// </description></item>
/// <item><description>
/// ES currency: the numeral governing the currency noun is attributive (DPD "uno" §2.2: <c>un euro</c>,
/// <c>veintiún euros</c>).
/// </description></item>
/// <item><description>
/// NTS-38: Basque ordinals after Euskaltzaindia rule 203 §5 (<c>milioigarren</c>, <c>bi milioigarren</c>,
/// <c>milioi bat eta batgarren</c>) and Spanish ordinals from 10^9 after DPD "ordinales" §2d (<c>milmillonésimo</c>).
/// </description></item>
/// </list>
/// Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30/NTS-38 section.
/// </summary>
[TestClass]
public class NumberToStringNts30Nts38Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>A caller-defined masculine currency.</summary>
    private static readonly CurrencyDefinition Euro = new()
    {
        UnitSingular = "euro",
        UnitPlural = "euros",
        SubunitSingular = "céntimo",
        SubunitPlural = "céntimos",
        Connector = "con",
    };

    // ===== VN: whole zero class ======================================================================

    /// <summary>The readings printed by the SGK Toán 4 (Chân trời sáng tạo, Bài 25) and Kết nối tri thức keys.</summary>
    [TestMethod]
    [DataRow(50_000_102L, "năm mươi triệu không nghìn một trăm linh hai")]
    [DataRow(3_000_013L, "ba triệu không nghìn không trăm mười ba")]
    [DataRow(307_000_262L, "ba trăm linh bảy triệu không nghìn hai trăm sáu mươi hai")]
    [DataRow(68_000_742L, "sáu mươi tám triệu không nghìn bảy trăm bốn mươi hai")]
    [DataRow(210_000_335L, "hai trăm mười triệu không nghìn ba trăm ba mươi lăm")]
    public void Vietnamese_ZeroClass_Attested(long value, string expected)
        => Assert.AreEqual(expected, Get("VN").Convert(value));

    /// <summary>The same class-by-class reading, with the zero hundreds and <c>linh</c> of the lower class (NTS-27).</summary>
    [TestMethod]
    [DataRow(1_000_005L, "một triệu không nghìn không trăm linh năm")]
    [DataRow(1_000_001L, "một triệu không nghìn không trăm linh một")]
    [DataRow(1_000_100L, "một triệu không nghìn một trăm")]
    [DataRow(999_000_999L, "chín trăm chín mươi chín triệu không nghìn chín trăm chín mươi chín")]
    public void Vietnamese_ZeroClass_WithInnerForms(long value, string expected)
        => Assert.AreEqual(expected, Get("VN").Convert(value));

    /// <summary>A zero class is never read when it ends the number or leads it, and the non-zero classes are unchanged.</summary>
    [TestMethod]
    [DataRow(50_000_000L, "năm mươi triệu")]
    [DataRow(5_000_000L, "năm triệu")]
    [DataRow(1_000L, "một nghìn")]
    [DataRow(50_001_000L, "năm mươi triệu không trăm linh một nghìn")]
    [DataRow(50_001_002L, "năm mươi triệu không trăm linh một nghìn không trăm linh hai")]
    [DataRow(400_070_192L, "bốn trăm triệu không trăm bảy mươi nghìn một trăm chín mươi hai")]
    [DataRow(178_320_005L, "một trăm bảy mươi tám triệu ba trăm hai mươi nghìn không trăm linh năm")]
    [DataRow(430_008_000L, "bốn trăm ba mươi triệu không trăm linh tám nghìn")]
    [DataRow(100_005_100L, "một trăm triệu không trăm linh năm nghìn một trăm")]
    [DataRow(5_000_000_000L, "năm tỷ")]
    [DataRow(1_005L, "một nghìn không trăm linh năm")]
    public void Vietnamese_ZeroClass_OnlyBetweenNonZeroClasses(long value, string expected)
        => Assert.AreEqual(expected, Get("VN").Convert(value));

    /// <summary>
    /// Above the triệu class: a single zero class is read like the attested one; consecutive zero classes are each read
    /// (the class-by-class method, deduced: not attested by an example).
    /// </summary>
    [TestMethod]
    [DataRow(5_000_001_000L, "năm tỷ không triệu không trăm linh một nghìn")]
    [DataRow(5_001_000_001L, "năm tỷ không trăm linh một triệu không nghìn không trăm linh một")]
    [DataRow(5_000_000_001L, "năm tỷ không triệu không nghìn không trăm linh một")]
    public void Vietnamese_ZeroClass_AboveTrieu(long value, string expected)
        => Assert.AreEqual(expected, Get("VN").Convert(value));

    /// <summary>Every Vietnamese culture and the composites reuse the cardinal.</summary>
    [TestMethod]
    public void Vietnamese_ZeroClass_CulturesAndComposites()
    {
        foreach (string culture in new[] { "VN", "VI", "VI-VN" })
            Assert.AreEqual("năm mươi triệu không nghìn một trăm linh hai", Get(culture).Convert(50_000_102));
        var vn = Get("VN");
        Assert.AreEqual("âm năm mươi triệu không nghìn một trăm linh hai", vn.Convert(-50_000_102));
        Assert.AreEqual("năm mươi triệu không nghìn một trăm linh hai phẩy năm", vn.Convert(50_000_102.5m));
    }

    /// <summary>The ordinal is thứ + cardinal on the values whose cardinal is now formed (NTS-28 rule).</summary>
    [TestMethod]
    [DataRow(50_000_102L, "thứ năm mươi triệu không nghìn một trăm linh hai")]
    [DataRow(1_000_005L, "thứ một triệu không nghìn không trăm linh năm")]
    [DataRow(3_000_013L, "thứ ba triệu không nghìn không trăm mười ba")]
    [DataRow(5_000_001_000L, "thứ năm tỷ không triệu không trăm linh một nghìn")]
    [DataRow(50_001_000L, "thứ năm mươi triệu không trăm linh một nghìn")]
    [DataRow(1_000_000L, "thứ một triệu")]
    public void Vietnamese_Ordinal_ZeroClassOpened(long value, string expected)
        => Assert.AreEqual(expected, Get("VN").ConvertOrdinal(value));

    /// <summary>Consecutive zero classes are not attested: their ordinal stays closed.</summary>
    [TestMethod]
    [DataRow(5_000_000_001L)]
    [DataRow(100_000_000_100L)]
    public void Vietnamese_Ordinal_ConsecutiveZeroClassesClosed(long value)
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("VN").ConvertOrdinal(value));

    // ===== ES currency ===============================================================================

    /// <summary>The numeral governing a masculine currency noun is apocopated (DPD "uno" §2.2).</summary>
    [TestMethod]
    [DataRow("1", "un euro")]
    [DataRow("21", "veintiún euros")]
    [DataRow("31", "treinta y un euros")]
    [DataRow("101", "ciento un euros")]
    [DataRow("2", "dos euros")]
    [DataRow("1.01", "un euro con un céntimo")]
    [DataRow("21.21", "veintiún euros con veintiún céntimos")]
    [DataRow("2.31", "dos euros con treinta y un céntimos")]
    [DataRow("21000001", "veintiún millones un euros")]
    [DataRow("1000000", "un millón euros")]
    public void Spanish_Currency_Apocope(string amount, string expected)
        => Assert.AreEqual(expected, Get("ES").ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Euro));

    /// <summary>A feminine currency, declared by the caller, takes <c>una</c>/<c>veintiuna</c>.</summary>
    [TestMethod]
    public void Spanish_Currency_FeminineByDefinition()
    {
        var peseta = new CurrencyDefinition
        {
            UnitSingular = "peseta", UnitPlural = "pesetas", SubunitSingular = "céntimo", SubunitPlural = "céntimos",
            Connector = "con", UnitForcedVariants = ForcedVariantSet.Create(("gender", "femenino")),
        };
        var es = Get("ES");
        Assert.AreEqual("una peseta", es.ConvertCurrency(1m, peseta));
        Assert.AreEqual("veintiuna pesetas", es.ConvertCurrency(21m, peseta));
        Assert.AreEqual("treinta y una pesetas con un céntimo", es.ConvertCurrency(31.01m, peseta));
    }

    /// <summary>
    /// Precedence: the caller's variants &lt; the language's currency context &lt; the currency's own constraints.
    /// A caller variant <c>form=standalone</c> does not undo the context; a currency declaring it does.
    /// </summary>
    [TestMethod]
    public void Spanish_Currency_Precedence()
    {
        var es = Get("ES");
        Assert.AreEqual("un euro", es.ConvertCurrency(1m, Euro, "form=standalone"));
        Assert.AreEqual("una euro", es.ConvertCurrency(1m, Euro, "gender=femenino"));
        var standalone = new CurrencyDefinition
        {
            UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "céntimo", SubunitPlural = "céntimos",
            Connector = "con", UnitForcedVariants = ForcedVariantSet.Create(("form", "standalone")),
        };
        Assert.AreEqual("uno euro con un céntimo", es.ConvertCurrency(1.01m, standalone));
        var feminineSubunit = new CurrencyDefinition
        {
            UnitSingular = "dólar", UnitPlural = "dólares", SubunitSingular = "moneda", SubunitPlural = "monedas",
            Connector = "con", SubunitForcedVariants = ForcedVariantSet.Create(("gender", "femenino")),
        };
        Assert.AreEqual("veintiún dólares con veintiuna monedas", es.ConvertCurrency(21.21m, feminineSubunit));
        Assert.IsFalse(es.CurrencyForcedVariants.IsEmpty);
    }

    /// <summary>The other languages declare no currency context: their currency numerals are unchanged.</summary>
    [TestMethod]
    public void Currency_OtherLanguagesUnchanged()
    {
        Assert.AreEqual("uno euro", Get("IT").ConvertCurrency(1m, Euro));
        Assert.AreEqual("one euro", Get("EN").ConvertCurrency(1m, Euro));
        foreach (string culture in new[] { "EN", "FR", "IT", "PT", "DE", "VN", "EU" })
            Assert.IsTrue(Get(culture).CurrencyForcedVariants.IsEmpty, culture);
    }

    /// <summary>The standalone cardinals, the time units and the clock are unchanged.</summary>
    [TestMethod]
    public void Spanish_Currency_OtherSurfacesUnchanged()
    {
        var es = Get("ES");
        Assert.AreEqual("uno", es.Convert(1));
        Assert.AreEqual("veintiuno", es.Convert(21));
        Assert.AreEqual("treinta y uno", es.Convert(31));
        Assert.AreEqual("un millón", es.Convert(1_000_000));
        Assert.AreEqual("veintiún minutos", es.Convert(TimeSpan.FromMinutes(21)));
    }

    // ===== NTS-38 EU =================================================================================

    /// <summary>Euskaltzaindia rule 203 §5, table and text.</summary>
    [TestMethod]
    [DataRow(1_000L, "milagarren")]
    [DataRow(1_001L, "mila eta batgarren")]
    [DataRow(1_002L, "mila eta bigarren")]
    [DataRow(10_000L, "hamar milagarren")]
    [DataRow(100_000L, "ehun milagarren")]
    [DataRow(1_000_000L, "milioigarren")]
    [DataRow(2_000_000L, "bi milioigarren")]
    [DataRow(1_000_001L, "milioi bat eta batgarren")]
    public void Basque_Ordinal_Rule203Attested(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").ConvertOrdinal(value));

    /// <summary>The rule-203 general rule (-garren on the cardinal) on the other millions.</summary>
    [TestMethod]
    [DataRow(1_000_002L, "milioi bat eta bigarren")]
    [DataRow(2_000_001L, "bi milioi eta batgarren")]
    [DataRow(10_000_000L, "hamar milioigarren")]
    [DataRow(100_000_000L, "ehun milioigarren")]
    [DataRow(1_100_000L, "milioi bat eta ehun milagarren")]
    public void Basque_Ordinal_Millions(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").ConvertOrdinal(value));

    /// <summary>From 10^9 the cardinal is "mila milioi" (rules 7, 203); its ordinal follows the same general rule.</summary>
    [TestMethod]
    [DataRow(1_000_000_000L, "mila milioigarren")]
    [DataRow(1_000_000_001L, "mila milioi eta batgarren")]
    [DataRow(2_000_000_000L, "bi mila milioigarren")]
    [DataRow(1_001_000_000L, "mila eta bat milioigarren")]
    [DataRow(999_999_999_999L, "bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzi milioi bederatziehun eta laurogeita hemeretzi mila bederatziehun eta laurogeita hemeretzigarren")]
    public void Basque_Ordinal_FromMilliard(long value, string expected)
        => Assert.AreEqual(expected, Get("EU").ConvertOrdinal(value));

    /// <summary>Every Basque culture agrees; beyond the cardinal domain the ordinal still fails closed.</summary>
    [TestMethod]
    public void Basque_Ordinal_CulturesAndBound()
    {
        Assert.AreEqual("milioigarren", Get("eu-ES").ConvertOrdinal(1_000_000));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("EU").ConvertOrdinal(1_000_000_000_000));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("EU").ConvertOrdinal(0));
        Assert.AreEqual("milioi bat", Get("EU").Convert(1_000_000));
    }

    // ===== NTS-38 ES =================================================================================

    /// <summary>DPD "ordinales" §1 and §2d.</summary>
    [TestMethod]
    [DataRow(1_000_000L, "millonésimo")]
    [DataRow(2_000_000L, "dosmillonésimo")]
    [DataRow(500_000_000L, "quinientosmillonésimo")]
    [DataRow(1_000_000_000L, "milmillonésimo")]
    public void Spanish_Ordinal_Attested(long value, string expected)
        => Assert.AreEqual(expected, Get("ES").ConvertOrdinal(value));

    /// <summary>The lower orders follow the head (DPD §2d "posponiendo los ordinales correspondientes a los órdenes inferiores").</summary>
    [TestMethod]
    [DataRow(1_000_000_001L, "milmillonésimo primero")]
    [DataRow(1_000_000_021L, "milmillonésimo vigesimoprimero")]
    [DataRow(1_000_002_000L, "milmillonésimo dosmilésimo")]
    [DataRow(1_002_000_000L, "ERR")]
    public void Spanish_Ordinal_ComposedFromMilliard(long value, string expected)
    {
        if (expected == "ERR")
            Assert.ThrowsExactly<NotSupportedException>(() => Get("ES").ConvertOrdinal(value));
        else
            Assert.AreEqual(expected, Get("ES").ConvertOrdinal(value));
    }

    /// <summary>
    /// The feminine: a simple ordinal varies, and a complex ordinal written in two words varies on both components
    /// (DPD §3: <c>vigésima primera</c>).
    /// </summary>
    [TestMethod]
    public void Spanish_Ordinal_Feminine()
    {
        var es = Get("ES");
        Assert.AreEqual("milmillonésima", es.ConvertOrdinal(1_000_000_000, "gender=femenino"));
        Assert.AreEqual("milmillonésima primera", es.ConvertOrdinal(1_000_000_001, "gender=femenino"));
        Assert.AreEqual("milmillonésima dosmilésima", es.ConvertOrdinal(1_000_002_000, "gender=femenino"));
        // A feminine hundreds multiplier (quinientas) stays closed, as below 10^9 (NTS-28).
        Assert.ThrowsExactly<NotSupportedException>(() => es.ConvertOrdinal(1_000_500_000, "gender=femenino"));
    }

    /// <summary>
    /// A multiplier of millón written in several words (dos mil, diez mil, veintiún mil, mil uno) is not shown soldered
    /// by the DPD: these round ordinals stay closed, like ciento cincuenta millones.
    /// </summary>
    [TestMethod]
    [DataRow(2_000_000_000L)]
    [DataRow(10_000_000_000L)]
    [DataRow(21_000_000_000L)]
    [DataRow(1_001_000_000L)]
    [DataRow(150_000_000L)]
    [DataRow(21_000_000L)]
    [DataRow(2_000_000_001L)]
    public void Spanish_Ordinal_SeveralWordMultipliersClosed(long value)
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("ES").ConvertOrdinal(value));

    /// <summary>Beyond the cardinal domain (no billón) the ordinal fails closed.</summary>
    [TestMethod]
    public void Spanish_Ordinal_Bound()
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("ES").ConvertOrdinal(1_000_000_000_000));
}
