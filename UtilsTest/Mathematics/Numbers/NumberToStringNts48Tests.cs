using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-48: French agreement of <c>vingt</c> and <c>cent</c>. OQLF, "Pluriel de vingt, de cent et de mille": they take an
/// <c>s</c> "quand ils sont multipliés et qu'ils ne sont pas immédiatement suivis d'un autre déterminant numéral"
/// (<c>quatre-vingts</c>, <c>deux cents</c>), stay invariable before another numeral, <c>mille</c> included (<c>trois cent
/// mille</c>), and agree before the nouns <c>million</c> and <c>milliard</c> ("Cinq cents millions … Million est un nom").
/// Configuration only: the multiplier of <c>mille</c> has its own <c>&lt;Groups onScale="1"&gt;</c> table in FR, FR-be and
/// FR-ch (each regional configuration redeclares it, since the inherited one carries the FR tens).
/// </summary>
[TestClass]
public class NumberToStringNts48Tests
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
    // FR: vingt and cent agree when they end the number or precede a scale noun.
    [DataRow("FR", 80L, "quatre-vingts")]
    [DataRow("FR", 180L, "cent quatre-vingts")]
    [DataRow("FR", 280L, "deux cent quatre-vingts")]
    [DataRow("FR", 200L, "deux cents")]
    [DataRow("FR", 80_000_000L, "quatre-vingts millions")]
    [DataRow("FR", 200_000_000L, "deux cents millions")]
    [DataRow("FR", 300_000_000L, "trois cents millions")]
    [DataRow("FR", 200_000_000_000L, "deux cents milliards")]
    [DataRow("FR", 200_000_001L, "deux cents millions un")]
    // FR: invariable before mille.
    [DataRow("FR", 80_000L, "quatre-vingt mille")]
    [DataRow("FR", 180_000L, "cent quatre-vingt mille")]
    [DataRow("FR", 280_000L, "deux cent quatre-vingt mille")]
    [DataRow("FR", 200_000L, "deux cent mille")]
    [DataRow("FR", 300_000L, "trois cent mille")]
    [DataRow("FR", 900_000L, "neuf cent mille")]
    [DataRow("FR", 200_001L, "deux cent mille un")]
    [DataRow("FR", 200_100L, "deux cent mille cent")]
    [DataRow("FR", 200_200L, "deux cent mille deux cents")]
    [DataRow("FR", 200_200_000L, "deux cents millions deux cent mille")]
    [DataRow("FR", 201_000L, "deux cent un mille")]
    [DataRow("FR", 1_000L, "mille")]
    [DataRow("FR", 2_000L, "deux mille")]
    [DataRow("FR", 21_000L, "vingt et un mille")]
    // FR-be: quatre-vingts already agreed alone; septante and nonante kept before mille.
    [DataRow("FR-be", 80L, "quatre-vingts")]
    [DataRow("FR-be", 80_000L, "quatre-vingt mille")]
    [DataRow("FR-be", 180_000L, "cent quatre-vingt mille")]
    [DataRow("FR-be", 70_000L, "septante mille")]
    [DataRow("FR-be", 90_000L, "nonante mille")]
    [DataRow("FR-be", 71_000L, "septante et un mille")]
    [DataRow("FR-be", 200_000L, "deux cent mille")]
    [DataRow("FR-be", 80_000_000L, "quatre-vingts millions")]
    [DataRow("FR-be", 200_000_000L, "deux cents millions")]
    // FR-ch: huitante is invariable; septante and nonante kept before mille.
    [DataRow("FR-ch", 80L, "huitante")]
    [DataRow("FR-ch", 80_000L, "huitante mille")]
    [DataRow("FR-ch", 70_000L, "septante mille")]
    [DataRow("FR-ch", 90_000L, "nonante mille")]
    [DataRow("FR-ch", 200_000L, "deux cent mille")]
    [DataRow("FR-ch", 200_000_000L, "deux cents millions")]
    public void French_VingtAndCentAgreement(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(new BigInteger(number)));

    /// <summary>
    /// The ordinals built on these cardinals lose the plural: invariable <c>vingt</c>/<c>cent</c> with an ordinal value
    /// (OQLF), the last constituent derived from its base (NTS-33), the leading groups following the cardinal rule.
    /// </summary>
    /// <param name="language">The language key.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("FR", 80L, "quatre-vingtième")]
    [DataRow("FR", 180L, "cent quatre-vingtième")]
    [DataRow("FR", 280L, "deux cent quatre-vingtième")]
    [DataRow("FR", 80_000L, "quatre-vingt millième")]
    [DataRow("FR", 200_000L, "deux cent millième")]
    [DataRow("FR", 300_000L, "trois cent millième")]
    [DataRow("FR", 200_001L, "deux cent mille unième")]
    [DataRow("FR", 200_200L, "deux cent mille deux centième")]
    [DataRow("FR", 80_000_000L, "quatre-vingt millionième")]
    [DataRow("FR", 200_000_000L, "deux cent millionième")]
    [DataRow("FR", 200_000_001L, "deux cents millions unième")]
    [DataRow("FR-be", 80_000L, "quatre-vingt millième")]
    [DataRow("FR-be", 200_000L, "deux cent millième")]
    [DataRow("FR-ch", 200_000L, "deux cent millième")]
    [DataRow("FR-ch", 80_000L, "huitante millième")]
    public void French_OrdinalsOnTheCorrectedCardinals(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).ConvertOrdinal(number));

    /// <summary>The feminine keeps agreeing the last word only: un → une, never the multiplier of mille or million.</summary>
    [TestMethod]
    public void French_FeminineUnchanged()
    {
        Assert.AreEqual("deux cent mille une", Get("FR").Convert(new BigInteger(200_001), "gender=feminin"));
        Assert.AreEqual("quatre-vingts", Get("FR").Convert(new BigInteger(80), "gender=feminin"));
        Assert.AreEqual("vingt et un millions", Get("FR").Convert(new BigInteger(21_000_000), "gender=feminin"));
    }

    /// <summary>
    /// The numerators agree like the cardinals (OQLF, "Écriture des fractions": quatre-vingts dix-millièmes (80/10 000),
    /// cinq cents millièmes (500/1000)); the denominators are untouched.
    /// </summary>
    [TestMethod]
    public void French_FractionsAndDecimals()
    {
        Assert.AreEqual("quatre-vingts virgule cinq dixièmes", Get("FR").Convert(80.5m));
        Assert.AreEqual("quatre-vingts dix-millièmes", Get("FR").ConvertFraction(80, 10_000));
        Assert.AreEqual("deux centièmes", Get("FR").ConvertFraction(2, 100));
        Assert.AreEqual("deux cent mille sur trois", Get("FR").ConvertFraction(200_000, 3));
    }

    /// <summary>
    /// The scoped table covers the multiplier of mille only, and each regional configuration redeclares it with its own
    /// tens (the inherited FR table would write quatre-vingt-dix instead of nonante).
    /// </summary>
    [TestMethod]
    public void French_ScopedTableCoversTheThousandsOnly()
    {
        foreach (string language in new[] { "FR", "FR-be", "FR-ch" })
        {
            var scoped = Get(language).ScaleScopedGroups;
            Assert.AreEqual(1, scoped.Count, language);
            Assert.AreEqual("1", scoped[0].OnScale, language);
        }
        Assert.AreEqual("deux cent", Get("FR").ScaleScopedGroups[0].Groups[3].Digits.Find(d => d.Digit == 2)!.StringValue);
        Assert.AreEqual("nonante", Get("FR-be").ScaleScopedGroups[0].Groups[2].Digits.Find(d => d.Digit == 9)!.StringValue);
        Assert.AreEqual("huitante", Get("FR-ch").ScaleScopedGroups[0].Groups[2].Digits.Find(d => d.Digit == 8)!.StringValue);
    }
}
