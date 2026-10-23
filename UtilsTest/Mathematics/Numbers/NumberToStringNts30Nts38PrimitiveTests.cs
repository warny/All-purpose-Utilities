using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30 and NTS-38, the generic engine behaviour on synthetic languages that follow no real grammar:
/// <list type="bullet">
/// <item><description><c>internalZeroGroupString</c> (<see cref="NumberToStringConverterOptions.InternalZeroGroupString"/>).</description></item>
/// <item><description><c>&lt;Currency forceVariants&gt;</c> (<see cref="NumberToStringConverterOptions.CurrencyForcedVariants"/>).</description></item>
/// <item><description>Round <c>OrdinalScale</c> values under <c>pairedScalesFrom</c>: the multiplier spans the pair.</description></item>
/// </list>
/// </summary>
[TestClass]
public class NumberToStringNts30Nts38PrimitiveTests
{
    /// <summary>Unit words of the synthetic languages.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic languages.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS30-38-{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a synthetic configuration: groups of three digits, static scales thousand, million(s), billion (unless
    /// paired), a gender dimension a/b and a form dimension plain/bound whose "bound" form writes "one" as "an".
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <param name="extraLanguages">Further Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string attributes = "", string scaleAttributes = "", string extra = "",
        string extraLanguages = "")
    {
        bool paired = scaleAttributes.Contains("pairedScalesFrom", StringComparison.Ordinal);
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string billion = paired ? "" : """<Scale value="3" string="billion" />""";
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point"
                        fractionSeparator="over" maxNumber="999999999999" {attributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" {scaleAttributes}>
                  <StaticNames>
                    <Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="milli(on|ons)" />{billion}
                  </StaticNames>
                </NumberScale>
                <Variants>
                  <Dimension name="gender" values="a,b" />
                  <Dimension name="form" values="plain,bound" />
                  <Variant type="gender" variant="b"><Replacement oldValue="one" newValue="oneB" scope="LastWord" /></Variant>
                  <Variant type="gender" variant="a"><Variant type="form" variant="bound"><Replacement oldValue="one" newValue="an" scope="LastWord" /></Variant></Variant>
                </Variants>
                {extra}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds a synthetic converter.</summary>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string attributes = "", string scaleAttributes = "", string extra = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, attributes, scaleAttributes, extra))[culture];
    }

    /// <summary>A currency with neutral words.</summary>
    private static readonly CurrencyDefinition Coin = new()
    {
        UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
    };

    // ===== internalZeroGroupString ===================================================================

    /// <summary>Without the option, zero groups are skipped exactly as before.</summary>
    [TestMethod]
    public void ZeroGroup_Absent_Historical()
    {
        var converter = Build();
        Assert.IsNull(converter.InternalZeroGroupString);
        Assert.AreEqual("one million one", converter.Convert(1_000_001));
        Assert.AreEqual("one billion one", converter.Convert(1_000_000_001));
    }

    /// <summary>
    /// The option renders the zero group, followed by its scale noun, only between two non-zero groups; consecutive
    /// internal zero groups are each rendered. The noun takes the form the language selects for a count of zero (here
    /// the singular of milli(on|ons)).
    /// </summary>
    [TestMethod]
    [DataRow(1_000_001L, "one million nil thousand one")]
    [DataRow(1_000_000L, "one million")]
    [DataRow(1_001_000L, "one million one thousand")]
    [DataRow(1_000L, "one thousand")]
    [DataRow(1L, "one")]
    [DataRow(1_000_000_000L, "one billion")]
    [DataRow(1_000_001_000L, "one billion nil million one thousand")]
    [DataRow(1_000_000_001L, "one billion nil million nil thousand one")]
    [DataRow(1_001_000_001L, "one billion one million nil thousand one")]
    [DataRow(-1_000_001L, "minus one million nil thousand one")]
    public void ZeroGroup_OnlyInternalGroups(long value, string expected)
        => Assert.AreEqual(expected, Build("""internalZeroGroupString="nil" """).Convert(value));

    /// <summary>The scale replacements and the group-with-scale triggers see the zero group like any other group.</summary>
    [TestMethod]
    public void ZeroGroup_GoesThroughTheScalePasses()
    {
        var converter = Build("""internalZeroGroupString="nil" """, extra: """
            <Replacements><Replacement oldValue="nil thousand" newValue="nought-thousand" onScale="1" /></Replacements>
            """);
        Assert.AreEqual("one million nought-thousand one", converter.Convert(1_000_001));
        Assert.AreEqual("two thousand", converter.Convert(2_000));
    }

    /// <summary>A group connector applies at the junctions as for a small group (its value is zero).</summary>
    [TestMethod]
    public void ZeroGroup_WithGroupConnector()
        => Assert.AreEqual("one million and nil thousand and one",
            Build("""internalZeroGroupString="nil" groupConnector="and" """).Convert(1_000_001));

    /// <summary>The final-group connector still precedes the last non-zero group.</summary>
    [TestMethod]
    public void ZeroGroup_WithFinalGroupConnector()
        => Assert.AreEqual("one million nil thousand and one",
            Build("""internalZeroGroupString="nil" finalGroupConnector="and" """).Convert(1_000_001));

    /// <summary>
    /// Under paired scales the multiplier of million spans two groups and is a phrase of its own: its inner zero
    /// thousand is not an internal group of the whole number (one thousand millions), while the zero thousand below
    /// the million is (one thousand millions nil thousand one).
    /// </summary>
    [TestMethod]
    public void ZeroGroup_PairedScales()
    {
        var converter = Build("""internalZeroGroupString="nil" """, """pairedScalesFrom="2" """);
        Assert.AreEqual("one thousand millions", converter.Convert(1_000_000_000));
        Assert.AreEqual("one thousand millions nil thousand one", converter.Convert(1_000_000_001));
        Assert.AreEqual("one thousand one millions", converter.Convert(1_001_000_000));
        Assert.AreEqual("one thousand one millions nil thousand one", converter.Convert(1_001_000_001));
    }

    /// <summary>Whitespace means absent; the variants of the caller do not rewrite the literal.</summary>
    [TestMethod]
    public void ZeroGroup_WhitespaceAndVariants()
    {
        Assert.IsNull(Build("""internalZeroGroupString=" " """).InternalZeroGroupString);
        var converter = Build("""internalZeroGroupString="one" """);
        Assert.AreEqual("one million one thousand oneB", converter.Convert(1_000_001, "gender=b"));
    }

    /// <summary>baseOn: an absent attribute inherits, an explicit empty value removes.</summary>
    [TestMethod]
    public void ZeroGroup_BaseOn()
    {
        string parent = NewName(), child = NewName(), cleared = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent, """internalZeroGroupString="nil" """,
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
                <Language baseOn="{parent}" internalZeroGroupString=""><Culture>{cleared}</Culture></Language>
                """));
        Assert.AreEqual("one million nil thousand one", converters[child].Convert(1_000_001));
        Assert.IsNull(converters[cleared].InternalZeroGroupString);
        Assert.AreEqual("one million one", converters[cleared].Convert(1_000_001));
    }

    /// <summary>Programmatic options and the clone carry the value.</summary>
    [TestMethod]
    public void ZeroGroup_OptionsAndClone()
    {
        var source = Build();
        var options = new NumberToStringConverterOptions(source) { InternalZeroGroupString = "nil" };
        var converter = new NumberToStringConverter(options);
        Assert.AreEqual("one million nil thousand one", converter.Convert(1_000_001));
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual("nil", clone.InternalZeroGroupString);
        Assert.AreEqual("one million nil thousand one", clone.Convert(1_000_001));
        Assert.AreEqual("one million one", source.Convert(1_000_001));
    }

    /// <summary>The ordinal is derived from the same cardinal.</summary>
    [TestMethod]
    public void ZeroGroup_Ordinal()
    {
        var converter = Build("""internalZeroGroupString="nil" """, extra: """<Ordinals prefix="no. " />""");
        Assert.AreEqual("no. one million nil thousand one", converter.ConvertOrdinal(1_000_001));
    }

    /// <summary>No built-in language other than the Vietnamese one opts in.</summary>
    [TestMethod]
    public void ZeroGroup_OnlyVietnameseOptsIn()
    {
        foreach (string culture in new[] { "EN", "FR", "DE", "ES", "EU", "IT", "PT", "RU", "ZH", "JA", "HI", "WO", "EE" })
            Assert.IsNull(NumberToStringConverter.GetConverter(culture).InternalZeroGroupString, culture);
        Assert.AreEqual("không", NumberToStringConverter.GetConverter("VN").InternalZeroGroupString);
    }

    // ===== Currency forceVariants ====================================================================

    /// <summary>Without a Currency element the currency numeral is the caller's cardinal.</summary>
    [TestMethod]
    public void Currency_Absent_Historical()
    {
        var converter = Build();
        Assert.IsTrue(converter.CurrencyForcedVariants.IsEmpty);
        Assert.AreEqual("one coin and one bit", converter.ConvertCurrency(1.01m, Coin));
    }

    /// <summary>The context is forced on the unit and the subunit, whatever the words of the currency.</summary>
    [TestMethod]
    public void Currency_ContextOnUnitAndSubunit()
    {
        var converter = Build(extra: """<Currency forceVariants="form=bound" />""");
        Assert.AreEqual("an coin and an bit", converter.ConvertCurrency(1.01m, Coin));
        Assert.AreEqual("twenty an coins", converter.ConvertCurrency(21m, Coin));
        Assert.AreEqual("one", converter.Convert(1));
    }

    /// <summary>Precedence: caller variants &lt; language context &lt; currency definition.</summary>
    [TestMethod]
    public void Currency_Precedence()
    {
        var converter = Build(extra: """<Currency forceVariants="form=bound" />""");
        // The caller's form is overridden by the context; its gender is kept.
        Assert.AreEqual("an coin", converter.ConvertCurrency(1m, Coin, "form=plain"));
        Assert.AreEqual("oneB coin", converter.ConvertCurrency(1m, Coin, "gender=b"));
        // The currency overrides the context, unit and subunit independently.
        var plainUnit = new CurrencyDefinition
        {
            UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
            UnitForcedVariants = ForcedVariantSet.Create(("form", "plain")),
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "b")),
        };
        Assert.AreEqual("one coin and oneB bit", converter.ConvertCurrency(1.01m, plainUnit));
    }

    /// <summary>An unknown dimension or value is a configuration error naming the Currency constituent.</summary>
    [TestMethod]
    public void Currency_UnknownVariant_Rejected()
    {
        var dimension = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build(extra: """<Currency forceVariants="case=x" />"""));
        Assert.AreEqual("UNTS006", dimension.ErrorCode);
        StringAssert.Contains(dimension.Message, "Currency");
        var value = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build(extra: """<Currency forceVariants="form=z" />"""));
        Assert.AreEqual("UNTS006", value.ErrorCode);
    }

    /// <summary>The schema rejects an unknown attribute of the Currency element.</summary>
    [TestMethod]
    public void Currency_Schema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build(extra: """<Currency gender="a" />"""));

    /// <summary>baseOn: an absent element inherits, a declared one replaces.</summary>
    [TestMethod]
    public void Currency_BaseOn()
    {
        string parent = NewName(), child = NewName(), replaced = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent, extra: """<Currency forceVariants="form=bound" />""",
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
                <Language baseOn="{parent}"><Culture>{replaced}</Culture><Currency /></Language>
                """));
        Assert.AreEqual("an coin", converters[child].ConvertCurrency(1m, Coin));
        Assert.AreEqual("one coin", converters[replaced].ConvertCurrency(1m, Coin));
    }

    /// <summary>Programmatic options and the clone carry the context.</summary>
    [TestMethod]
    public void Currency_OptionsAndClone()
    {
        var source = Build();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            CurrencyForcedVariants = ForcedVariantSet.Create(("form", "bound")),
        });
        Assert.AreEqual("an coin", converter.ConvertCurrency(1m, Coin));
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual("an coin", clone.ConvertCurrency(1m, Coin));
        Assert.AreEqual("one coin", source.ConvertCurrency(1m, Coin));
    }

    // ===== OrdinalScale under paired scales ==========================================================

    /// <summary>
    /// A round value of the unnamed upper half of a pair is a round value of the named scale below it, whose multiplier
    /// spans the pair: 10^9 is a thousand millions.
    /// </summary>
    [TestMethod]
    public void OrdinalScale_PairedUpperHalf()
    {
        var converter = Build(scaleAttributes: """pairedScalesFrom="2" """, extra: """
            <Ordinals suffix="th"><OrdinalScale scales="2" multiplierSeparator=" " /></Ordinals>
            """);
        Assert.AreEqual("millionth", converter.ConvertOrdinal(1_000_000));
        Assert.AreEqual("two millionth", converter.ConvertOrdinal(2_000_000));
        Assert.AreEqual("one thousand millionth", converter.ConvertOrdinal(1_000_000_000));
        Assert.AreEqual("two thousand five hundred millionth", converter.ConvertOrdinal(2_500_000_000));
        // Not round at the million: the cardinal path.
        Assert.AreEqual("one thousand millions oneth", converter.ConvertOrdinal(1_000_000_001));
    }
}
