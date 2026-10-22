using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-49: the French configurations (FR, FR-be, FR-ch) write their numerals after the 1990 spelling rectifications
/// (Rectifications de l'orthographe, Journal officiel, 6 December 1990, rule 1, as given by RENOUVO and
/// orthographe-recommandee.info: "Les numéraux composés sont systématiquement reliés par des traits d'union", with
/// <c>vingt-et-un</c>, <c>deux-cents</c>, <c>un-million-cent</c>, <c>trente-et-unième</c>; CCDMD list:
/// <c>deux-millions-trois-cent-mille</c>, <c>la deux-millionième fois</c>). Every element of the numeral is joined by a
/// hyphen, <c>et</c>, <c>cent</c>, <c>mille</c> and the scale nouns included (some summaries state that the nouns
/// <c>million</c> and <c>milliard</c> are not concerned; the project follows the hyphenated examples); the agreement rules of
/// NTS-48 are unchanged. A fraction keeps a space between numerator and denominator (<c>vingt-cinq millièmes</c>, 25/1000,
/// no longer ambiguous with <c>vingt cinq-millièmes</c>, 20/5000), and the words around the numeral (<c>virgule</c>,
/// <c>sur</c>, <c>moins</c>, units, currencies) keep their spaces. Also fixes FR 81 (<c>quatre-vingt-un</c>, no <c>et</c>)
/// and 71 (<c>soixante-et-onze</c>).
/// </summary>
[TestClass]
public class NumberToStringNts49Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The language key.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>The cardinals of the three French configurations.</summary>
    /// <param name="language">The language key.</param>
    /// <param name="number">The cardinal value.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow("FR", 17L, "dix-sept")]
    [DataRow("FR", 21L, "vingt-et-un")]
    [DataRow("FR", 22L, "vingt-deux")]
    [DataRow("FR", 70L, "soixante-dix")]
    [DataRow("FR", 71L, "soixante-et-onze")]
    [DataRow("FR", 77L, "soixante-dix-sept")]
    [DataRow("FR", 80L, "quatre-vingts")]
    [DataRow("FR", 81L, "quatre-vingt-un")]
    [DataRow("FR", 82L, "quatre-vingt-deux")]
    [DataRow("FR", 90L, "quatre-vingt-dix")]
    [DataRow("FR", 91L, "quatre-vingt-onze")]
    [DataRow("FR", 97L, "quatre-vingt-dix-sept")]
    [DataRow("FR", 101L, "cent-un")]
    [DataRow("FR", 121L, "cent-vingt-et-un")]
    [DataRow("FR", 181L, "cent-quatre-vingt-un")]
    [DataRow("FR", 200L, "deux-cents")]
    [DataRow("FR", 1_000L, "mille")]
    [DataRow("FR", 1_001L, "mille-un")]
    [DataRow("FR", 2_000L, "deux-mille")]
    [DataRow("FR", 21_000L, "vingt-et-un-mille")]
    [DataRow("FR", 81_000L, "quatre-vingt-un-mille")]
    [DataRow("FR", 200_000L, "deux-cent-mille")]
    [DataRow("FR", 1_000_000L, "un-million")]
    [DataRow("FR", 2_000_000L, "deux-millions")]
    [DataRow("FR", 2_000_001L, "deux-millions-un")]
    [DataRow("FR", 2_300_000L, "deux-millions-trois-cent-mille")]
    [DataRow("FR", 200_000_000L, "deux-cents-millions")]
    [DataRow("FR", 2_000_000_000L, "deux-milliards")]
    [DataRow("FR", -22L, "moins vingt-deux")]
    [DataRow("FR-be", 71L, "septante-et-un")]
    [DataRow("FR-be", 77L, "septante-sept")]
    [DataRow("FR-be", 80L, "quatre-vingts")]
    [DataRow("FR-be", 81L, "quatre-vingt-un")]
    [DataRow("FR-be", 99L, "nonante-neuf")]
    [DataRow("FR-be", 80_000L, "quatre-vingt-mille")]
    [DataRow("FR-be", 200_000L, "deux-cent-mille")]
    [DataRow("FR-ch", 81L, "huitante-et-un")]
    [DataRow("FR-ch", 88L, "huitante-huit")]
    [DataRow("FR-ch", 70_000L, "septante-mille")]
    [DataRow("FR-ch", 2_000_000L, "deux-millions")]
    public void French_Cardinals(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(new BigInteger(number)));

    /// <summary>The feminine still agrees the last element, a hyphen being a word boundary.</summary>
    [TestMethod]
    public void French_Feminine()
    {
        Assert.AreEqual("vingt-et-une", Get("FR").Convert(new BigInteger(21), "gender=feminin"));
        Assert.AreEqual("deux-cent-mille-une", Get("FR").Convert(new BigInteger(200_001), "gender=feminin"));
        Assert.AreEqual("vingt-et-un-millions", Get("FR").Convert(new BigInteger(21_000_000), "gender=feminin"));
    }

    /// <summary>The ordinals derive the last element of the hyphenated numeral.</summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(1L, "premier")]
    [DataRow(21L, "vingt-et-unième")]
    [DataRow(22L, "vingt-deuxième")]
    [DataRow(25L, "vingt-cinquième")]
    [DataRow(81L, "quatre-vingt-unième")]
    [DataRow(99L, "quatre-vingt-dix-neuvième")]
    [DataRow(200L, "deux-centième")]
    [DataRow(1_000_000L, "millionième")]
    [DataRow(2_000_000L, "deux-millionième")]
    [DataRow(300_000_000L, "trois-cent-millionième")]
    [DataRow(2_000_001L, "deux-millions-unième")]
    [DataRow(1_001_000L, "un-million-millième")]
    public void French_Ordinals(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number));

    /// <summary>Fractions keep the space between numerator and denominator; the composites keep their own words.</summary>
    [TestMethod]
    public void French_FractionsDecimalsAndComposites()
    {
        var math = new DecimalFormatOptions { ReadingMode = DecimalReadingMode.MathematicalFraction };
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "centime", SubunitPlural = "centimes", Connector = "et" };

        Assert.AreEqual("vingt-cinq millièmes", Get("FR").ConvertFraction(25, 1_000));
        Assert.AreEqual("deux centièmes", Get("FR").ConvertFraction(2, 100));
        Assert.AreEqual("deux sur deux-cents", Get("FR").ConvertFraction(2, 200));
        Assert.AreEqual("vingt-et-un virgule cinq dixièmes", Get("FR").Convert(21.5m));
        Assert.AreEqual("vingt-et-un et cinq-cents millièmes", Get("FR").Convert(21.5m, 3, math));
        Assert.AreEqual("vingt-et-un euros et vingt-deux centimes", Get("FR").ConvertCurrency(21.22m, euro));
        Assert.AreEqual("mille-neuf-cent-quatre-vingts", Get("FR").ConvertYear(1980));
    }
}
