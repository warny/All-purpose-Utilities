using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-51, the generic currency-phrase primitives on synthetic languages that follow no real grammar:
/// <list type="bullet">
/// <item><description>
/// <c>&lt;Currency terminalScaleConnector terminalScales&gt;</c>
/// (<see cref="NumberToStringConverterOptions.CurrencyTerminalScaleConnector"/>): a connector between the numeral and
/// the currency noun when the numeral ends, structurally, on a configured scale noun.
/// </description></item>
/// <item><description>
/// <c>&lt;Currency&gt;&lt;Replacement exceptBeforeNoun&gt;</c> (<see cref="NumberToStringConverterOptions.CurrencyReplacements"/>):
/// replacements of the currency numeral only, optionally conditioned on the beginning of the noun.
/// </description></item>
/// </list>
/// </summary>
[TestClass]
public class NumberToStringNts51PrimitiveTests
{
    /// <summary>Unit words of the synthetic languages.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic languages.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS51-{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a synthetic configuration: groups of three digits, static scales thousand, million(s) and billion (unless
    /// paired), and a form dimension plain/bound whose "bound" form writes "one" as "an".
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="scaleAttributes">Extra attributes of the NumberScale element.</param>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <param name="extraLanguages">Further Language elements.</param>
    /// <param name="scaleNames">The static scale names from scale 1, or <see langword="null"/> for the default ones.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string attributes = "", string scaleAttributes = "", string extra = "",
        string extraLanguages = "", string[]? scaleNames = null)
    {
        bool paired = scaleAttributes.Contains("pairedScalesFrom", StringComparison.Ordinal);
        scaleNames ??= paired ? ["thousand", "milli(on|ons)"] : ["thousand", "milli(on|ons)", "billion"];
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string scales = string.Concat(scaleNames.Select((s, i) => $"""<Scale value="{i + 1}" string="{s}" />"""));
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
                  <StaticNames><Scale value="0" string="" />{scales}</StaticNames>
                </NumberScale>
                <Variants>
                  <Dimension name="form" values="plain,bound" />
                  <Variant type="form" variant="bound"><Replacement oldValue="one" newValue="an" scope="LastWord" /></Variant>
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
    /// <param name="scaleNames">The static scale names from scale 1, or <see langword="null"/> for the default ones.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string attributes = "", string scaleAttributes = "", string extra = "",
        string[]? scaleNames = null)
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, attributes, scaleAttributes, extra, scaleNames: scaleNames))[culture];
    }

    /// <summary>The Currency element of the terminal-scale connector on scales 2 and above.</summary>
    private const string OfFromMillion = """<Currency terminalScaleConnector="OF" terminalScales="2.." />""";

    /// <summary>A currency with neutral words.</summary>
    private static readonly CurrencyDefinition Coin = new()
    {
        UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
    };

    /// <summary>Builds a currency whose unit noun is <paramref name="noun"/>.</summary>
    /// <param name="noun">The unit noun, singular and plural.</param>
    /// <returns>The currency.</returns>
    private static CurrencyDefinition Named(string noun) => new()
    {
        UnitSingular = noun, UnitPlural = noun, SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
    };

    // ===== Terminal scale: structural helper =========================================================

    /// <summary>The terminal scale is the scale of the lowest non-zero group, not the highest scale.</summary>
    [TestMethod]
    [DataRow(0L, null)]
    [DataRow(1L, null)]
    [DataRow(1_000L, 1)]
    [DataRow(1_001L, null)]
    [DataRow(1_000_000L, 2)]
    [DataRow(1_001_000L, 1)]
    [DataRow(1_000_001L, null)]
    [DataRow(2_500_000L, 1)]
    [DataRow(7_000_000_000L, 3)]
    [DataRow(7_000_000_000_000_000L, 5)]
    public void TerminalScale_English(long value, int? expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("EN").GetTerminalScaleIndex(value));

    /// <summary>Above <see cref="long.MaxValue"/> the generated scales are reached the same way.</summary>
    [TestMethod]
    public void TerminalScale_BeyondLong()
    {
        var english = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(7, english.GetTerminalScaleIndex(BigInteger.Pow(10, 21)));
        Assert.AreEqual(1, english.GetTerminalScaleIndex(BigInteger.Pow(10, 21) + 5_000));
        Assert.IsNull(english.GetTerminalScaleIndex(BigInteger.Pow(10, 21) + 5));
    }

    /// <summary>A paired multiplier is one group at its named scale: 10^9 (mil millones) ends on scale 2, never on 3.</summary>
    [TestMethod]
    [DataRow(1_000_000_000L, 2)]
    [DataRow(2_500_000_000L, 2)]
    [DataRow(2_500_000L, 1)]
    [DataRow(1_000_000_001L, null)]
    [DataRow(1_000_000L, 2)]
    public void TerminalScale_Paired(long value, int? expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("ES").GetTerminalScaleIndex(value));

    /// <summary>The group widths of a custom group pattern are followed (Hindi 3,2: लाख is scale 2, at 10^5).</summary>
    [TestMethod]
    [DataRow(100_000L, 2)]
    [DataRow(10_000_000L, 3)]
    [DataRow(1_000_000L, 2)]
    [DataRow(101_000L, 1)]
    public void TerminalScale_GroupPattern(long value, int? expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("HI").GetTerminalScaleIndex(value));

    // ===== Terminal scale connector ==================================================================

    /// <summary>Without the attributes the currency phrase is historical.</summary>
    [TestMethod]
    public void Connector_Absent_Historical()
    {
        var converter = Build();
        Assert.IsNull(converter.CurrencyTerminalScaleConnector);
        Assert.IsNull(converter.CurrencyTerminalScales);
        Assert.AreEqual(0, converter.CurrencyReplacements.Count);
        Assert.AreEqual("two millions coins", converter.ConvertCurrency(2_000_000m, Coin));
    }

    /// <summary>The connector follows a numeral ending on a configured scale noun only.</summary>
    [TestMethod]
    [DataRow("1", "one coin")]
    [DataRow("1000", "one thousand coins")]
    [DataRow("1000000", "one million OF coins")]
    [DataRow("2000000", "two millions OF coins")]
    [DataRow("1001000", "one million one thousand coins")]
    [DataRow("1000001", "one million one coins")]
    [DataRow("1000000000", "one billion OF coins")]
    [DataRow("2000000000.01", "two billion OF coins and one bit")]
    [DataRow("-2000000", "minus two millions OF coins")]
    [DataRow("999999.999", "one million OF coins")]
    [DataRow("0", "zero coins")]
    [DataRow("0.5", "zero coins and fifty bits")]
    public void Connector_TerminalScale(string amount, string expected)
        => Assert.AreEqual(expected, Build(extra: OfFromMillion)
            .ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Coin));

    /// <summary>The covered scales are the configured ones only.</summary>
    [TestMethod]
    public void Connector_ScaleRange()
    {
        var converter = Build(extra: """<Currency terminalScaleConnector="OF" terminalScales="2" />""");
        Assert.AreEqual("one million OF coins", converter.ConvertCurrency(1_000_000m, Coin));
        Assert.AreEqual("one billion coins", converter.ConvertCurrency(1_000_000_000m, Coin));
        Assert.AreEqual("one thousand coins", converter.ConvertCurrency(1_000m, Coin));
        Assert.AreEqual("2", converter.CurrencyTerminalScales);
    }

    /// <summary>Under pairedScalesFrom the multiplier of the pair ends on the named scale.</summary>
    [TestMethod]
    [DataRow("1000000000", "one thousand millions OF coins")]
    [DataRow("2500000000", "two thousand five hundred millions OF coins")]
    [DataRow("2500000001", "two thousand five hundred millions one coins")]
    [DataRow("2500000", "two millions five hundred thousand coins")]
    public void Connector_Paired(string amount, string expected)
        => Assert.AreEqual(expected, Build(scaleAttributes: """pairedScalesFrom="2" """, extra: OfFromMillion)
            .ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Coin));

    /// <summary>The subunit amount is decided on its own value: a large subunit count can end on a scale.</summary>
    [TestMethod]
    public void Connector_Subunit()
    {
        var micro = new CurrencyDefinition
        {
            UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
            SubunitDigits = 7,
        };
        var converter = Build(extra: OfFromMillion);
        Assert.AreEqual("one million OF coins and two millions OF bits", converter.ConvertCurrency(1_000_000.2m, micro));
        Assert.AreEqual("one coin and two millions one bits", converter.ConvertCurrency(1.2000001m, micro));
        Assert.AreEqual("one coin", Build(extra: OfFromMillion).ConvertCurrency(1m, new CurrencyDefinition
        {
            UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "bit", SubunitPlural = "bits", Connector = "and",
            SubunitDigits = 0,
        }));
    }

    /// <summary>A whole-number exception is a literal: its structure is unknown, so it takes no connector.</summary>
    [TestMethod]
    public void Connector_Exception()
    {
        var converter = Build(extra: OfFromMillion + """<Exceptions><Number value="1000000" string="megaone" /></Exceptions>""");
        Assert.AreEqual("megaone coins", converter.ConvertCurrency(1_000_000m, Coin));
        Assert.AreEqual("two millions OF coins", converter.ConvertCurrency(2_000_000m, Coin));
    }

    /// <summary>A multiplier placed after the scale noun ends the numeral: no connector.</summary>
    [TestMethod]
    public void Connector_MultiplierAfterScale()
    {
        var converter = Build("""multiplierPosition="afterScale" """, extra: OfFromMillion);
        Assert.AreEqual("millions two coins", converter.ConvertCurrency(2_000_000m, Coin));
        Assert.IsNull(converter.GetTerminalScaleIndex(2_000_000));
    }

    /// <summary>The connector is independent of the forced variants of the amount, which keep their precedence.</summary>
    [TestMethod]
    public void Connector_WithForcedVariants()
    {
        var converter = Build(extra: """<Currency forceVariants="form=bound" terminalScaleConnector="OF" terminalScales="2.." />""");
        Assert.AreEqual("one million OF coins", converter.ConvertCurrency(1_000_000m, Coin));
        Assert.AreEqual("two millions an coins", converter.ConvertCurrency(2_000_001m, Coin));
        Assert.AreEqual("an coin", converter.ConvertCurrency(1m, Coin, "form=plain"));
        Assert.AreEqual("one million", converter.Convert(1_000_000));
    }

    /// <summary>Invalid configurations are rejected.</summary>
    [TestMethod]
    public void Connector_Invalid()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Build(extra: """<Currency terminalScaleConnector="OF" />"""));
        Assert.ThrowsExactly<ArgumentException>(() => Build(extra: """<Currency terminalScales="2.." />"""));
        Assert.ThrowsExactly<ArgumentException>(() => Build(extra: """<Currency terminalScaleConnector="OF" terminalScales="0.." />"""));
        Assert.ThrowsExactly<ArgumentException>(() => Build(extra: """<Currency terminalScaleConnector="OF" terminalScales="..2" />"""));
        Assert.ThrowsExactly<ArgumentException>(() => Build(extra: """<Currency terminalScaleConnector="OF" terminalScales="x" />"""));
        var source = Build();
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            CurrencyTerminalScaleConnector = " ",
            CurrencyTerminalScales = "2..",
        }));
    }

    // ===== Currency replacements =====================================================================

    /// <summary>The replacement changes the currency numeral only, unit and subunit, before the noun.</summary>
    [TestMethod]
    public void Replacement_CurrencyOnly()
    {
        var converter = Build(extra: """<Currency><Replacement oldValue="one" newValue="a" scope="Standalone" /></Currency>""");
        Assert.AreEqual("a coin", converter.ConvertCurrency(1m, Coin));
        Assert.AreEqual("a coin and a bit", converter.ConvertCurrency(1.01m, Coin));
        Assert.AreEqual("twenty one coins", converter.ConvertCurrency(21m, Coin));
        Assert.AreEqual("one", converter.Convert(1));
        Assert.AreEqual("minus a coin", converter.ConvertCurrency(-1m, Coin));
    }

    /// <summary>The scope keeps its meaning on the whole numeral.</summary>
    [TestMethod]
    public void Replacement_LastWord()
    {
        var converter = Build(extra: """<Currency><Replacement oldValue="one" newValue="a" scope="LastWord" /></Currency>""");
        Assert.AreEqual("twenty a coins", converter.ConvertCurrency(21m, Coin));
        Assert.AreEqual("a coin", converter.ConvertCurrency(1m, Coin));
        Assert.AreEqual("twenty one", converter.Convert(21));
    }

    /// <summary>The noun condition blocks the rule before the listed beginnings, ignoring case.</summary>
    [TestMethod]
    [DataRow("coin", "a coin")]
    [DataRow("xenon", "one xenon")]
    [DataRow("Xenon", "one Xenon")]
    [DataRow("stone", "one stone")]
    [DataRow("sun", "a sun")]
    public void Replacement_ExceptBeforeNoun(string noun, string expected)
        => Assert.AreEqual(expected, Build(extra: """
            <Currency><Replacement oldValue="one" newValue="a" scope="Standalone" exceptBeforeNoun="x, st" /></Currency>
            """).ConvertCurrency(1m, Named(noun)));

    /// <summary>The noun condition is evaluated for the unit and the subunit separately.</summary>
    [TestMethod]
    public void Replacement_ExceptBeforeNoun_Subunit()
    {
        var converter = Build(extra: """<Currency><Replacement oldValue="one" newValue="a" exceptBeforeNoun="x" /></Currency>""");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "coin", UnitPlural = "coins", SubunitSingular = "xbit", SubunitPlural = "xbits", Connector = "and",
        };
        Assert.AreEqual("a coin and one xbit", converter.ConvertCurrency(1.01m, currency));
    }

    /// <summary>The replacements run before the connector, in order, after the variants of the amount.</summary>
    [TestMethod]
    public void Replacement_Order()
    {
        var converter = Build(extra: """
            <Currency forceVariants="form=bound" terminalScaleConnector="OF" terminalScales="2..">
              <Replacement oldValue="twenty one millions" newValue="a score millions" />
              <Replacement oldValue="a score millions" newValue="the score millions" />
            </Currency>
            """);
        Assert.AreEqual("the score millions OF coins", converter.ConvertCurrency(21_000_000m, Coin));
        Assert.AreEqual("an coin", converter.ConvertCurrency(1m, Coin));
    }

    /// <summary>The schema accepts only oldValue, newValue, scope and exceptBeforeNoun on a currency replacement.</summary>
    [TestMethod]
    public void Replacement_Schema()
    {
        Assert.ThrowsExactly<XmlSchemaValidationException>(() =>
            Build(extra: """<Currency><Replacement oldValue="one" newValue="a" onScale="1" /></Currency>"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() =>
            Build(extra: """<Currency><Replacement oldValue="one" newValue="a" onValue="1" /></Currency>"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() =>
            Build(extra: """<Currency><Replacement oldValue="one" /></Currency>"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() =>
            Build(extra: """<Currency><Replacement oldValue="one" newValue="a" scope="Everywhere" /></Currency>"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build(extra: """
            <Currency><Replacement oldValue="one" newValue="a"><Variant type="form" variant="bound" /></Replacement></Currency>
            """));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() =>
            Build(extra: """<Currency terminalConnector="OF" />"""));
    }

    /// <summary>The programmatic options reject what the schema rejects.</summary>
    [TestMethod]
    public void Replacement_Options_Invalid()
    {
        var source = Build();
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            CurrencyReplacements = [new CurrencyReplacementRule(new NumberToStringConverter.ReplacementRule("one", "a", ReplacementScope.Standalone, 1))],
        }));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            CurrencyReplacements = [null!],
        }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CurrencyReplacementRule(new NumberToStringConverter.ReplacementRule("one", "a", ReplacementScope.Standalone), ["x", " "]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurrencyReplacementRule(null!));
    }

    // ===== baseOn, clone, immutability ===============================================================

    /// <summary>baseOn: an absent element inherits everything; a declared one replaces the parent's as a whole.</summary>
    [TestMethod]
    public void Currency_BaseOn_ReplacedAsAWhole()
    {
        string parent = NewName(), child = NewName(), replaced = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent, extra: """
                <Currency terminalScaleConnector="OF" terminalScales="2.."><Replacement oldValue="one" newValue="a" /></Currency>
                """,
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
                <Language baseOn="{parent}"><Culture>{replaced}</Culture><Currency forceVariants="form=bound" /></Language>
                """));
        Assert.AreEqual("a coin", converters[child].ConvertCurrency(1m, Coin));
        Assert.AreEqual("two millions OF coins", converters[child].ConvertCurrency(2_000_000m, Coin));
        Assert.AreEqual("an coin", converters[replaced].ConvertCurrency(1m, Coin));
        Assert.AreEqual("two millions coins", converters[replaced].ConvertCurrency(2_000_000m, Coin));
        Assert.IsNull(converters[replaced].CurrencyTerminalScaleConnector);
        Assert.AreEqual(0, converters[replaced].CurrencyReplacements.Count);
    }

    /// <summary>The options and the clone carry both primitives.</summary>
    [TestMethod]
    public void Currency_OptionsAndClone()
    {
        var source = Build();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            CurrencyTerminalScaleConnector = "OF",
            CurrencyTerminalScales = "2..",
            CurrencyReplacements = [new CurrencyReplacementRule(new NumberToStringConverter.ReplacementRule("one", "a", ReplacementScope.Standalone), ["x"])],
        });
        Assert.AreEqual("a coin", converter.ConvertCurrency(1m, Coin));
        Assert.AreEqual("two millions OF coins", converter.ConvertCurrency(2_000_000m, Coin));
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual("a coin", clone.ConvertCurrency(1m, Coin));
        Assert.AreEqual("one xenon", clone.ConvertCurrency(1m, Named("xenon")));
        Assert.AreEqual("two millions OF coins", clone.ConvertCurrency(2_000_000m, Coin));
        Assert.AreEqual("OF", clone.CurrencyTerminalScaleConnector);
        Assert.AreEqual("2..", clone.CurrencyTerminalScales);
        Assert.AreEqual("one coin", source.ConvertCurrency(1m, Coin));
    }

    /// <summary>Mutating the options or the rule source after construction does not change the converter.</summary>
    [TestMethod]
    public void Currency_Snapshot()
    {
        var prefixes = new List<string> { "x" };
        var rules = new List<CurrencyReplacementRule>
        {
            new(new NumberToStringConverter.ReplacementRule("one", "a", ReplacementScope.Standalone), prefixes),
        };
        var options = new NumberToStringConverterOptions(Build())
        {
            CurrencyTerminalScaleConnector = "OF",
            CurrencyTerminalScales = "2..",
            CurrencyReplacements = rules,
        };
        var converter = new NumberToStringConverter(options);
        rules.Clear();
        prefixes.Add("c");
        options.CurrencyTerminalScaleConnector = "BY";
        options.CurrencyTerminalScales = "1..";
        Assert.AreEqual("a coin", converter.ConvertCurrency(1m, Coin));
        Assert.AreEqual("one thousand coins", converter.ConvertCurrency(1_000m, Coin));
        Assert.AreEqual("two millions OF coins", converter.ConvertCurrency(2_000_000m, Coin));
        Assert.AreEqual(1, converter.CurrencyReplacements.Count);
        Assert.AreEqual(1, converter.CurrencyReplacements[0].ExceptBeforeNoun.Count);
    }
}
