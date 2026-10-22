using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-33: an ordinal is derived from the derivational base of its last constituent, not from the agreed standalone
/// cardinal. French writes the cardinal <c>deux cents</c> but the ordinal <c>deux centième</c> (OQLF, "Écriture des
/// fractions": <c>centième, deux centième, trois centième</c>; "Pluriel de vingt, de cent et de mille": <c>vingt</c> and
/// <c>cent</c> stay invariable with an ordinal value), and the ordinal of a round million is formed on the singular noun
/// (<c>deux millionième</c>, CCDMD "la deux-millionième fois", Larousse <c>millionième</c> adj. num. ord.). The fraction
/// denominators are separate strings and keep their plural (<c>trois centièmes</c>, <c>deux millionièmes</c>).
/// <para>
/// Covers the French configurations (FR, FR-ca, FR-be, FR-ch), the fraction invariants and the generic
/// <see cref="DigitType.OrdinalBaseString"/> primitive on a synthetic language.
/// </para>
/// </summary>
[TestClass]
public class NumberToStringNts33Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The language key.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    // ── French: the five historical NTS-33 cases ────────────────────────────────────────────────

    /// <summary>The five values recorded by NTS-33 lose the plural of the agreed cardinal.</summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(200L, "deux centième")]
    [DataRow(300_000_000L, "trois cent millionième")]
    [DataRow(2_000_000L, "deux millionième")]
    [DataRow(10_000_000_000L, "dix milliardième")]
    [DataRow(2_000_000_000_000L, "deux billionième")]
    public void French_HistoricalCases(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number));

    // ── French: hundreds ────────────────────────────────────────────────────────────────────────

    /// <summary>An exact hundred is derived from its invariable base (OQLF: deux centième, trois centième).</summary>
    /// <param name="language">The language key.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("FR", 100L, "centième")]
    [DataRow("FR", 200L, "deux centième")]
    [DataRow("FR", 300L, "trois centième")]
    [DataRow("FR", 500L, "cinq centième")]
    [DataRow("FR", 900L, "neuf centième")]
    [DataRow("FR-ca", 200L, "deux centième")]
    [DataRow("FR-be", 200L, "deux centième")]
    [DataRow("FR-be", 900L, "neuf centième")]
    [DataRow("FR-ch", 200L, "deux centième")]
    [DataRow("FR-ch", 900L, "neuf centième")]
    public void French_ExactHundreds(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).ConvertOrdinal(number));

    /// <summary>
    /// A hundred followed by a lower part already composes through <c>buildString</c> (<c>deux cent *</c>): the base is
    /// not applied a second time, and the final word still takes the suffix.
    /// </summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(201L, "deux cent unième")]
    [DataRow(220L, "deux cent vingtième")]
    [DataRow(250L, "deux cent cinquantième")]
    [DataRow(299L, "deux cent quatre-vingt dix neuvième")]
    [DataRow(301L, "trois cent unième")]
    [DataRow(999L, "neuf cent quatre-vingt dix neuvième")]
    [DataRow(1_200L, "mille deux centième")]
    [DataRow(2_000_200L, "deux millions deux centième")]
    public void French_HundredNeighbours(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number));

    // ── French: round scales ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A round value from the million is formed by <c>OrdinalScale</c> on the singular scale noun, the multiplier one
    /// dropped (<c>millionième</c>, Larousse) and a larger multiplier rendered in its derivational base. The thousands
    /// keep the cardinal path, which already gives the singular <c>mille</c>.
    /// </summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(1_000L, "millième")]
    [DataRow(2_000L, "deux millième")]
    [DataRow(1_000_000L, "millionième")]
    [DataRow(2_000_000L, "deux millionième")]
    [DataRow(3_000_000L, "trois millionième")]
    [DataRow(17_000_000L, "dix sept millionième")]
    [DataRow(200_000_000L, "deux cent millionième")]
    [DataRow(300_000_000L, "trois cent millionième")]
    [DataRow(1_000_000_000L, "milliardième")]
    [DataRow(2_000_000_000L, "deux milliardième")]
    [DataRow(6_000_000_000L, "six milliardième")]
    [DataRow(10_000_000_000L, "dix milliardième")]
    [DataRow(200_000_000_000L, "deux cent milliardième")]
    [DataRow(1_000_000_000_000L, "billionième")]
    [DataRow(2_000_000_000_000L, "deux billionième")]
    [DataRow(300_000_000_000_000L, "trois cent billionième")]
    [DataRow(1_000_000_000_000_000L, "billiardième")]
    [DataRow(2_000_000_000_000_000L, "deux billiardième")]
    [DataRow(1_000_000_000_000_000_000L, "trillionième")]
    [DataRow(2_000_000_000_000_000_000L, "deux trillionième")]
    [DataRow(9_000_000_000_000_000_000L, "neuf trillionième")]
    public void French_RoundScales(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number));

    /// <summary>
    /// The scale nouns are masculine: under the feminine variant the multiplier of a round million keeps <c>un</c>, as in
    /// the cardinal <c>vingt et un millions</c>; the feminine of the ordinal one is unchanged.
    /// </summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected feminine ordinal.</param>
    [TestMethod]
    [DataRow(1L, "première")]
    [DataRow(1_000_000L, "millionième")]
    [DataRow(2_000_000L, "deux millionième")]
    [DataRow(21_000_000L, "vingt et un millionième")]
    [DataRow(201_000_000L, "deux cent un millionième")]
    [DataRow(21_000_000_000L, "vingt et un milliardième")]
    public void French_RoundScales_Feminine(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number, "gender=feminin"));

    /// <summary>The regional configurations inherit the scale ordinals and declare the hundreds base in their own tables.</summary>
    /// <param name="language">The language key.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow("FR-ca", 2_000_000L, "deux millionième")]
    [DataRow("FR-be", 1_000_000L, "millionième")]
    [DataRow("FR-be", 300_000_000L, "trois cent millionième")]
    [DataRow("FR-be", 2_000_000_000L, "deux milliardième")]
    [DataRow("FR-ch", 300_000_000L, "trois cent millionième")]
    [DataRow("FR-ch", 80_000_000L, "huitante millionième")]
    public void FrenchRegional_RoundScales(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).ConvertOrdinal(number));

    /// <summary>
    /// Belgian <c>quatre-vingts</c> is invariable with an ordinal value (OQLF: "la page trois cent quatre-vingt"), at the
    /// end of a cardinal-based ordinal as in the multiplier of a round scale.
    /// </summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(80L, "quatre-vingtième")]
    [DataRow(180L, "cent quatre-vingtième")]
    [DataRow(280L, "deux cent quatre-vingtième")]
    [DataRow(1_080L, "mille quatre-vingtième")]
    [DataRow(80_000_000L, "quatre-vingt millionième")]
    [DataRow(81L, "quatre-vingt unième")]
    public void Belgian_QuatreVingts(long number, string expected)
        => Assert.AreEqual(expected, Get("FR-be").ConvertOrdinal(number));

    // ── French: non-round values (characterization, unchanged) ─────────────────────────────────

    /// <summary>
    /// In a non-round ordinal only the last constituent is derived; the leading groups are cardinal and keep their
    /// agreement (CCDMD: million "prend un s dès qu'il y en a deux ou plus, peu importe qu'il termine ou non le
    /// nombre"; OQLF: "Cinq cents millions", million being a noun). These outputs are byte-identical to master.
    /// </summary>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(2_000_001L, "deux millions unième")]
    [DataRow(200_000_001L, "deux cents millions unième")]
    [DataRow(300_000_001L, "trois cents millions unième")]
    [DataRow(2_000_000_001L, "deux milliards unième")]
    [DataRow(2_000_000_000_001L, "deux billions unième")]
    [DataRow(1_001_000L, "un million millième")]
    public void French_NonRound_KeepLeadingCardinalGroups(long number, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertOrdinal(number));

    // ── French: cardinals and fractions are untouched ──────────────────────────────────────────

    /// <summary>The cardinals keep their agreed forms.</summary>
    /// <param name="language">The language key.</param>
    /// <param name="number">The cardinal value.</param>
    /// <param name="expected">The expected cardinal.</param>
    [TestMethod]
    [DataRow("FR", 200L, "deux cents")]
    [DataRow("FR", 300_000_000L, "trois cents millions")]
    [DataRow("FR", 2_000_000L, "deux millions")]
    [DataRow("FR", 10_000_000_000L, "dix milliards")]
    [DataRow("FR", 2_000_000_000_000L, "deux billions")]
    [DataRow("FR-be", 80L, "quatre-vingts")]
    [DataRow("FR-be", 180L, "cent quatre-vingts")]
    public void French_CardinalsUnchanged(string language, long number, string expected)
        => Assert.AreEqual(expected, Get(language).Convert(new BigInteger(number)));

    /// <summary>
    /// The fraction denominators are nouns already derived and agree with the numerator (OQLF: "les trois centièmes",
    /// CCDMD: "deux millionièmes", "six milliardièmes"); the ordinal base never reaches them.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <param name="expected">The expected fraction.</param>
    [TestMethod]
    [DataRow(1L, 100L, "un centième")]
    [DataRow(2L, 100L, "deux centièmes")]
    [DataRow(3L, 100L, "trois centièmes")]
    [DataRow(2L, 1_000L, "deux millièmes")]
    [DataRow(2L, 1_000_000L, "deux millionièmes")]
    [DataRow(6L, 1_000_000_000L, "six milliardièmes")]
    [DataRow(2L, 1_000_000_000L, "deux milliardièmes")]
    public void French_FractionsKeepTheirPlural(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("FR").ConvertFraction(numerator, denominator));

    /// <summary>The decimal reading on a named denominator is unchanged.</summary>
    [TestMethod]
    public void French_DecimalFractionsUnchanged()
    {
        var math = new DecimalFormatOptions { ReadingMode = DecimalReadingMode.MathematicalFraction };

        Assert.AreEqual("zéro virgule deux centièmes", Get("FR").Convert(0.02m));
        Assert.AreEqual("zéro virgule deux millionièmes", Get("FR").Convert(0.000002m));
        Assert.AreEqual("vingt et un et cinq cents millièmes", Get("FR").Convert(21.5m, 3, math));
    }

    /// <summary>The French configuration declares the base on the agreed hundreds only.</summary>
    [TestMethod]
    public void French_ConfigurationDeclaresTheHundredsBase()
    {
        var hundreds = Get("FR").Groups[3];

        Assert.IsNull(hundreds[1].OrdinalBaseString);
        Assert.AreEqual("deux cent", hundreds[2].OrdinalBaseString);
        Assert.AreEqual("neuf cent", hundreds[9].OrdinalBaseString);
        Assert.IsTrue(Get("FR").Groups[1].Values.All(d => d.OrdinalBaseString is null));
        Assert.AreEqual("quatre-vingt", Get("FR-be").Groups[2][8].OrdinalBaseString);
        // The generated scale nouns force the masculine on their multiplier (million is a masculine noun).
        Assert.AreEqual(2, Get("FR").GeneratedScaleForms.Count);
        Assert.AreEqual(1, Get("FR").OrdinalScaleRules.Count);
    }

    // ── Generic primitive (synthetic language) ──────────────────────────────────────────────────

    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"NTS33-{Guid.NewGuid():N}";

    /// <summary>
    /// A synthetic language whose digit 3 has the agreed standalone form <c>fooS</c> and the composition form
    /// <c>foo *</c>, with an optional derivational base, the scales thousand and million, and a <c>X</c> suffix.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="baseAttribute">The <c>ordinalBaseString</c> attribute text of digit 3, or empty.</param>
    /// <param name="scaleRules">The OrdinalScale elements.</param>
    /// <param name="innerAttribute">The <c>innerString</c> attribute text of digit 3, or empty.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string baseAttribute, string scaleRules = "", string innerAttribute = "") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999999">
            <Culture>{culture}</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" /><Digit digit="1" string="one" /><Digit digit="2" string="two" />
                <Digit digit="3" string="fooS" {baseAttribute} {innerAttribute} />
                <Digit digit="4" string="four" /><Digit digit="5" string="five" /><Digit digit="6" string="six" />
                <Digit digit="7" string="seven" /><Digit digit="8" string="eight" /><Digit digit="9" string="nine" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" /><Digit digit="1" string="ten" buildString="ten *" />
                <Digit digit="2" string="twenty" buildString="twenty *" /><Digit digit="3" string="thirty" buildString="thirty *" />
                <Digit digit="4" string="forty" buildString="forty *" /><Digit digit="5" string="fifty" buildString="fifty *" />
                <Digit digit="6" string="sixty" buildString="sixty *" /><Digit digit="7" string="seventy" buildString="seventy *" />
                <Digit digit="8" string="eighty" buildString="eighty *" /><Digit digit="9" string="ninety" buildString="ninety *" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" /><Digit digit="1" string="hundred" buildString="hundred *" />
                <Digit digit="2" string="two hundred" buildString="two hundred *" /><Digit digit="3" string="three hundred" buildString="three hundred *" />
                <Digit digit="4" string="four hundred" buildString="four hundred *" /><Digit digit="5" string="five hundred" buildString="five hundred *" />
                <Digit digit="6" string="six hundred" buildString="six hundred *" /><Digit digit="7" string="seven hundred" buildString="seven hundred *" />
                <Digit digit="8" string="eight hundred" buildString="eight hundred *" /><Digit digit="9" string="nine hundred" buildString="nine hundred *" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="million" /></StaticNames></NumberScale>
            <Ordinals suffix="X">{scaleRules}</Ordinals>
          </Language>
        </Numbers>
        """;

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="baseAttribute">The <c>ordinalBaseString</c> attribute text of digit 3, or empty.</param>
    /// <param name="scaleRules">The OrdinalScale elements.</param>
    /// <param name="innerAttribute">The <c>innerString</c> attribute text of digit 3, or empty.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string baseAttribute, string scaleRules = "", string innerAttribute = "")
    {
        string culture = NewCulture();
        return NumberToStringConverter.ReadConfiguration(Document(culture, baseAttribute, scaleRules, innerAttribute))[culture];
    }

    /// <summary>The cardinal keeps the agreed form; the ordinal is derived from the base.</summary>
    [TestMethod]
    public void OrdinalBase_CardinalUnchanged_OrdinalDerivedFromBase()
    {
        var converter = Build("""ordinalBaseString="foo" """);

        Assert.AreEqual("fooS", converter.Convert(new BigInteger(3)));
        Assert.AreEqual("fooX", converter.ConvertOrdinal(3));
        Assert.AreEqual("twenty fooS", converter.Convert(new BigInteger(23)));
        Assert.AreEqual("twenty fooX", converter.ConvertOrdinal(23));
    }

    /// <summary>Without the attribute the ordinal keeps the historical form built on the agreed cardinal.</summary>
    [TestMethod]
    public void OrdinalBase_Absent_IsHistoricalBehaviour()
    {
        var converter = Build("");

        Assert.AreEqual("fooSX", converter.ConvertOrdinal(3));
        Assert.AreEqual("fooS million", converter.Convert(new BigInteger(3_000_000)));
        Assert.AreEqual("fooS millionX", converter.ConvertOrdinal(3_000_000));
        Assert.IsNull(converter.Groups[1][3].OrdinalBaseString);
    }

    /// <summary>The base only reaches the last constituent: a leading group keeps its cardinal form.</summary>
    [TestMethod]
    public void OrdinalBase_LeadingGroupsKeepTheCardinalForm()
    {
        var converter = Build("""ordinalBaseString="foo" """);

        Assert.AreEqual("fooS thousand fooX", converter.ConvertOrdinal(3_003));
        Assert.AreEqual("fooS thousand oneX", converter.ConvertOrdinal(3_001));
        // A round value without OrdinalScale ends with the scale noun: the multiplier is a leading constituent.
        Assert.AreEqual("fooS millionX", converter.ConvertOrdinal(3_000_000));
    }

    /// <summary>The multiplier of an OrdinalScale value is rendered in its derivational base, never as an ordinal.</summary>
    [TestMethod]
    public void OrdinalBase_UsedByTheOrdinalScaleMultiplier()
    {
        var converter = Build("""ordinalBaseString="foo" """, """<OrdinalScale scales="2" multiplierSeparator=" " />""");

        Assert.AreEqual("foo millionX", converter.ConvertOrdinal(3_000_000));
        Assert.AreEqual("twenty foo millionX", converter.ConvertOrdinal(23_000_000));
        Assert.AreEqual("millionX", converter.ConvertOrdinal(1_000_000));
        Assert.AreEqual("fooS million", converter.Convert(new BigInteger(3_000_000)));
        // A non-round value keeps the cardinal-based path.
        Assert.AreEqual("fooS million fooX", converter.ConvertOrdinal(3_000_003));

        var historical = Build("", """<OrdinalScale scales="2" multiplierSeparator=" " />""");
        Assert.AreEqual("fooS millionX", historical.ConvertOrdinal(3_000_000));
    }

    /// <summary>In the ordinal base the attribute wins over innerString; the cardinal keeps innerString.</summary>
    [TestMethod]
    public void OrdinalBase_TakesPrecedenceOverInnerString()
    {
        var converter = Build("""ordinalBaseString="foo" """, innerAttribute: """innerString="fooI" """);

        Assert.AreEqual("twenty fooI", converter.Convert(new BigInteger(23)));
        Assert.AreEqual("twenty fooX", converter.ConvertOrdinal(23));
        Assert.AreEqual("fooX", converter.ConvertOrdinal(3));
    }

    /// <summary>Fractions, decimals and currencies never use the base.</summary>
    [TestMethod]
    public void OrdinalBase_NotUsedOutsideOrdinals()
    {
        var converter = Build("""ordinalBaseString="foo" """);

        Assert.AreEqual("fooS", converter.Convert(3m));
        StringAssert.Contains(converter.ConvertFraction(3, 7), "fooS");
        var currency = new CurrencyDefinition { UnitSingular = "u", UnitPlural = "us", SubunitSingular = "s", SubunitPlural = "ss" };
        StringAssert.StartsWith(converter.ConvertCurrency(3m, currency), "fooS");
    }

    /// <summary>The value is snapshotted: the exposed digits and the options clone are copies.</summary>
    [TestMethod]
    public void OrdinalBase_IsSnapshottedAndCloned()
    {
        var converter = Build("""ordinalBaseString="foo" """);

        DigitType exposed = converter.Groups[1][3];
        Assert.AreEqual("foo", exposed.OrdinalBaseString);
        exposed.OrdinalBaseString = "mutated";
        Assert.AreEqual("fooX", converter.ConvertOrdinal(3));

        var options = new NumberToStringConverterOptions(Build("""ordinalBaseString="foo" """));
        Assert.AreEqual("foo", options.Groups![1].Digits.Single(d => d.Digit == 3).OrdinalBaseString);
        var clone = new NumberToStringConverter(options);
        options.Groups[1].Digits.Single(d => d.Digit == 3).OrdinalBaseString = "changed";
        Assert.AreEqual("fooX", clone.ConvertOrdinal(3));
        Assert.AreEqual("fooS", clone.Convert(new BigInteger(3)));
    }

    /// <summary>The schema accepts the attribute and rejects an empty value.</summary>
    [TestMethod]
    public void OrdinalBase_Schema()
    {
        NumberToStringConverter.ValidateConfigurationSchemaForTesting(Document(NewCulture(), """ordinalBaseString="foo" """));

        Assert.ThrowsExactly<XmlSchemaValidationException>(
            () => NumberToStringConverter.ReadConfiguration(Document(NewCulture(), """ordinalBaseString="" """)));
    }

    /// <summary>A blank value configured programmatically is rejected at construction.</summary>
    [TestMethod]
    public void OrdinalBase_BlankValueIsRejected()
    {
        var options = new NumberToStringConverterOptions(Build(""));
        options.Groups![1].Digits.Single(d => d.Digit == 3).OrdinalBaseString = " ";

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(options));
        StringAssert.Contains(exception.Message, "ordinalBaseString");
    }

    /// <summary>A derived language that redefines its Groups replaces the digits, base included, like every digit attribute.</summary>
    [TestMethod]
    public void OrdinalBase_FollowsGroupsInheritance()
    {
        string parent = NewCulture();
        string inheriting = NewCulture();
        string document = Document(parent, """ordinalBaseString="foo" """)
            .Replace("</Numbers>", $"""<Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language></Numbers>""");
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("fooX", converters[inheriting].ConvertOrdinal(3));
        // FR-be and FR-ch redeclare their Groups and therefore their own bases.
        Assert.AreEqual("deux centième", Get("FR-ch").ConvertOrdinal(200));
    }
}
