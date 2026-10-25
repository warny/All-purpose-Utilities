using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-35, Polish cardinals above 999 999 on the long scale (SJP PWN: <c>bilion</c> "w Polsce […] milion milionów",
/// <c>biliard</c> "tysiąc bilionów", <c>trylion</c> "milion bilionów", <c>tryliard</c> "tysiąc trylionów", <c>kwadrylion</c>
/// "w Polsce […] bilion bilionów"; WSJP PAN <c>milion</c>, <c>miliard</c>, <c>bilion</c>, <c>biliard</c>, <c>trylion</c>):
/// <c>milion</c>, <c>miliard</c>, <c>bilion</c>, <c>biliard</c>, <c>trylion</c> … generated from the Conway components in
/// Polish spelling (<c>kwadrylion</c>, <c>kwintylion</c>, <c>sekstylion</c>, <c>septylion</c>, <c>oktylion</c>,
/// <c>nonilion</c>, <c>decylion</c>, <c>undecylion</c>, <c>duodecylion</c>, attested in SJP PWN and the Wielki słownik
/// ortograficzny PWN), each family inflected after its multiplier by the CLDR rule of
/// <c>PolishPluralLexicalFormSelector</c> (WSO PWN: <c>bilion -na; -ny, -nów</c>, <c>biliard -rda; -rdy, -rdów</c>):
/// <c>jeden milion</c>, <c>dwa miliony</c>, <c>pięć milionów</c>, <c>dwadzieścia dwa miliony</c>. The ordinals keep their
/// validated domain (1–999 999). Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-35 section.
/// </summary>
[TestClass]
public class NumberToStringNts35Tests
{
    /// <summary>Gets the built-in Polish converter.</summary>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get() => NumberToStringConverter.GetConverter("PL");

    /// <summary>Gets m × 10^e.</summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="exponent">The power of ten.</param>
    /// <returns>The value.</returns>
    private static BigInteger Power(long multiplier, int exponent) => multiplier * BigInteger.Pow(10, exponent);

    /// <summary><c>milion</c> after every kind of multiplier: one, few (2–4 but 12–14), many.</summary>
    [TestMethod]
    [DataRow(1L, "jeden milion")]
    [DataRow(2L, "dwa miliony")]
    [DataRow(4L, "cztery miliony")]
    [DataRow(5L, "pięć milionów")]
    [DataRow(12L, "dwanaście milionów")]
    [DataRow(14L, "czternaście milionów")]
    [DataRow(21L, "dwadzieścia jeden milionów")]
    [DataRow(22L, "dwadzieścia dwa miliony")]
    [DataRow(25L, "dwadzieścia pięć milionów")]
    [DataRow(101L, "sto jeden milionów")]
    [DataRow(102L, "sto dwa miliony")]
    [DataRow(112L, "sto dwanaście milionów")]
    [DataRow(122L, "sto dwadzieścia dwa miliony")]
    [DataRow(999L, "dziewięćset dziewięćdziesiąt dziewięć milionów")]
    public void Millions(long multiplier, string expected) => Assert.AreEqual(expected, Get().Convert(Power(multiplier, 6)));

    /// <summary><c>miliard</c>, the second suffix family.</summary>
    [TestMethod]
    [DataRow(1L, "jeden miliard")]
    [DataRow(2L, "dwa miliardy")]
    [DataRow(5L, "pięć miliardów")]
    [DataRow(12L, "dwanaście miliardów")]
    [DataRow(22L, "dwadzieścia dwa miliardy")]
    [DataRow(102L, "sto dwa miliardy")]
    public void Milliards(long multiplier, string expected) => Assert.AreEqual(expected, Get().Convert(Power(multiplier, 9)));

    /// <summary>The attested long-scale names and their inflection.</summary>
    [TestMethod]
    [DataRow(1L, 12, "jeden bilion")]
    [DataRow(2L, 12, "dwa biliony")]
    [DataRow(5L, 12, "pięć bilionów")]
    [DataRow(1L, 15, "jeden biliard")]
    [DataRow(2L, 15, "dwa biliardy")]
    [DataRow(5L, 15, "pięć biliardów")]
    [DataRow(1L, 18, "jeden trylion")]
    [DataRow(22L, 18, "dwadzieścia dwa tryliony")]
    [DataRow(1L, 21, "jeden tryliard")]
    [DataRow(1L, 24, "jeden kwadrylion")]
    [DataRow(1L, 27, "jeden kwadryliard")]
    [DataRow(1L, 30, "jeden kwintylion")]
    [DataRow(1L, 36, "jeden sekstylion")]
    [DataRow(1L, 42, "jeden septylion")]
    [DataRow(1L, 48, "jeden oktylion")]
    [DataRow(1L, 54, "jeden nonilion")]
    [DataRow(1L, 60, "jeden decylion")]
    [DataRow(1L, 66, "jeden undecylion")]
    [DataRow(1L, 72, "jeden duodecylion")]
    [DataRow(5L, 72, "pięć duodecylionów")]
    public void AttestedLargeScales(long multiplier, int exponent, string expected)
        => Assert.AreEqual(expected, Get().Convert(Power(multiplier, exponent)));

    /// <summary>
    /// Generated names beyond the attested ones, in the Polish spelling of the Conway components (Latin i after t, r and
    /// c written y as in <c>kwintylion</c>, <c>trylion</c>, <c>decylion</c>; qu written kw, v written w, c read [k]
    /// written k, x written ks): the first unattested family (10^78), the tens, the hundreds and a grouped name.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 78, "jeden tredecylion")]
    [DataRow(1L, 84, "jeden kwatuordecylion")]
    [DataRow(1L, 96, "jeden sedecylion")]
    [DataRow(1L, 120, "jeden wigintylion")]
    [DataRow(1L, 123, "jeden wigintyliard")]
    [DataRow(1L, 156, "jeden seswigintylion")]
    [DataRow(1L, 180, "jeden trygintylion")]
    [DataRow(2L, 180, "dwa trygintyliony")]
    [DataRow(1L, 600, "jeden centylion")]
    [DataRow(5L, 603, "pięć centyliardów")]
    [DataRow(1L, 6000, "jeden milinilion")]
    public void GeneratedLargeScales(long multiplier, int exponent, string expected)
        => Assert.AreEqual(expected, Get().Convert(Power(multiplier, exponent)));

    /// <summary>The groups compose; the thousands keep their own forms.</summary>
    [TestMethod]
    [DataRow(1_001_000L, "jeden milion tysiąc")]
    [DataRow(2_001_001L, "dwa miliony tysiąc jeden")]
    [DataRow(2_002_000L, "dwa miliony dwa tysiące")]
    [DataRow(5_005_000L, "pięć milionów pięć tysięcy")]
    [DataRow(22_345_678L, "dwadzieścia dwa miliony trzysta czterdzieści pięć tysięcy sześćset siedemdziesiąt osiem")]
    [DataRow(1_000_001_000L, "jeden miliard tysiąc")]
    [DataRow(1_001_000_001L, "jeden miliard jeden milion jeden")]
    [DataRow(999_999_999_999L, "dziewięćset dziewięćdziesiąt dziewięć miliardów dziewięćset dziewięćdziesiąt dziewięć milionów dziewięćset dziewięćdziesiąt dziewięć tysięcy dziewięćset dziewięćdziesiąt dziewięć")]
    public void Compositions(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>Negative values take <c>minus</c>.</summary>
    [TestMethod]
    public void Negatives()
    {
        Assert.AreEqual("minus jeden milion", Get().Convert(-1_000_000));
        Assert.AreEqual("minus dwa biliony", Get().Convert(-Power(2, 12)));
    }

    /// <summary>The former domain is unchanged.</summary>
    [TestMethod]
    [DataRow(1_000L, "tysiąc")]
    [DataRow(2_000L, "dwa tysiące")]
    [DataRow(5_000L, "pięć tysięcy")]
    [DataRow(22_000L, "dwadzieścia dwa tysiące")]
    [DataRow(999_999L, "dziewięćset dziewięćdziesiąt dziewięć tysięcy dziewięćset dziewięćdziesiąt dziewięć")]
    public void BelowTheMillion_Unchanged(long number, string expected) => Assert.AreEqual(expected, Get().Convert(number));

    /// <summary>The cardinal domain is open: no maximum.</summary>
    [TestMethod]
    public void Domain_IsUnbounded() => Assert.IsNull(Get().MaxNumber);

    /// <summary>The ordinals keep their validated domain: the million and above fail closed.</summary>
    [TestMethod]
    [DataRow(1_000_000L)]
    [DataRow(1_000_001L)]
    [DataRow(2_000_000L)]
    [DataRow(1_000_000_000L)]
    public void Ordinals_FromTheMillion_FailClosed(long number)
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => Get().ConvertOrdinal(number));
        StringAssert.Contains(exception.Message, "validated ordinal domain");
    }

    /// <summary>The largest validated ordinal is unchanged.</summary>
    [TestMethod]
    public void Ordinal_999999_Unchanged()
        => Assert.AreEqual("dziewięćset dziewięćdziesiąt dziewięć tysięcy dziewięćset dziewięćdziesiąty dziewiąty", Get().ConvertOrdinal(999_999));
}
