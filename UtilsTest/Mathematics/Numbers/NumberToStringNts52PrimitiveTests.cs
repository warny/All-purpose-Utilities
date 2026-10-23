using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-52, the generic currency noun connector on synthetic languages that follow no real grammar:
/// <c>&lt;Currency&gt;&lt;NounConnector default&gt;&lt;Substitution …&gt;</c>
/// (<see cref="NumberToStringConverterOptions.CurrencyNounConnector"/>). A default, then contextual substitutions on the
/// terminal scale of the numeral, its count (optionally modulo) and the beginning of the noun; the most specific
/// substitution wins and, at equal specificity, the first declared.
/// </summary>
[TestClass]
public class NumberToStringNts52PrimitiveTests
{
    /// <summary>Unit words of the synthetic languages.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic languages.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS52-{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a synthetic configuration: groups of three digits, static scales thousand, million(s) and billion, and a
    /// form dimension plain/bound whose "bound" form writes "one" as "an".
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <param name="extraLanguages">Further Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string extra = "", string extraLanguages = "")
    {
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point"
                        fractionSeparator="over" maxNumber="999999999999">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="milli(on|ons)" /><Scale value="3" string="billion" /></StaticNames>
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
    /// <param name="extra">Elements appended to the Language.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string extra = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, extra))[culture];
    }

    /// <summary>Builds a synthetic converter whose Currency element holds <paramref name="nounConnector"/>.</summary>
    /// <param name="nounConnector">The NounConnector element.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter WithConnector(string nounConnector) => Build($"<Currency>{nounConnector}</Currency>");

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

    /// <summary>Converts a currency amount written in the invariant culture.</summary>
    /// <param name="converter">The converter.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency, <see cref="Coin"/> by default.</param>
    /// <returns>The text.</returns>
    private static string Amount(NumberToStringConverter converter, string amount, CurrencyDefinition? currency = null)
        => converter.ConvertCurrency(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), currency ?? Coin);

    /// <summary>The terminal-scale connector with its elided form before a noun beginning with "e".</summary>
    private const string Elision = """
        <NounConnector default="">
          <Substitution terminalScales="2.." value="OF" />
          <Substitution terminalScales="2.." beforeNoun="e" value="O'" nounSeparator="" />
        </NounConnector>
        """;

    // ===== Default ===================================================================================

    /// <summary>Without a NounConnector the phrase is historical.</summary>
    [TestMethod]
    public void Absent_Historical()
    {
        var converter = Build();
        Assert.IsNull(converter.CurrencyNounConnector);
        Assert.AreEqual("two millions coins", Amount(converter, "2000000"));
        Assert.AreEqual("one coin and twenty bits", Amount(converter, "1.20"));
    }

    /// <summary>An empty default without substitution produces the historical text, never two spaces.</summary>
    [TestMethod]
    public void EmptyDefault_Historical()
    {
        var converter = WithConnector("""<NounConnector default="" />""");
        Assert.IsNotNull(converter.CurrencyNounConnector);
        Assert.AreEqual("", converter.CurrencyNounConnector!.Default);
        Assert.AreEqual(0, converter.CurrencyNounConnector.Substitutions.Count);
        Assert.AreEqual("one coin", Amount(converter, "1"));
        Assert.AreEqual("two millions coins and twenty bits", Amount(converter, "2000000.20"));
        Assert.AreEqual("one coin", Amount(WithConnector("<NounConnector />"), "1"));
    }

    /// <summary>A non-empty default applies to every amount, unit and subunit, with the standard or a configured separator.</summary>
    [TestMethod]
    public void NonEmptyDefault()
    {
        var converter = WithConnector("""<NounConnector default="OF" />""");
        Assert.AreEqual("one OF coin", Amount(converter, "1"));
        Assert.AreEqual("two millions OF coins and twenty OF bits", Amount(converter, "2000000.20"));
        Assert.AreEqual("zero OF coins", Amount(converter, "0"));
        var bound = WithConnector("""<NounConnector default="OF-" nounSeparator="" />""");
        Assert.AreEqual("one OF-coin", Amount(bound, "1"));
    }

    /// <summary>An empty substitution cancels a non-empty default where it matches: numeral, separator, noun.</summary>
    [TestMethod]
    public void EmptySubstitution_CancelsDefault()
    {
        var converter = WithConnector("""
            <NounConnector default="OF" nounSeparator="-"><Substitution numberRange="1" value="" nounSeparator="+" /></NounConnector>
            """);
        Assert.AreEqual("one coin", Amount(converter, "1"));
        Assert.AreEqual("two OF-coins", Amount(converter, "2"));
    }

    // ===== Constraints ===============================================================================

    /// <summary>terminalScales: the scale the numeral ends on, decided on the group structure.</summary>
    [TestMethod]
    [DataRow("1", "one coin")]
    [DataRow("1000", "one thousand coins")]
    [DataRow("1000000", "one million OF coins")]
    [DataRow("2000000", "two millions OF coins")]
    [DataRow("1001000", "one million one thousand coins")]
    [DataRow("1000001", "one million one coins")]
    [DataRow("2500000", "two millions five hundred thousand coins")]
    [DataRow("1000000000", "one billion OF coins")]
    [DataRow("-2000000", "minus two millions OF coins")]
    public void TerminalScales(string amount, string expected)
        => Assert.AreEqual(expected, Amount(WithConnector("""<NounConnector><Substitution terminalScales="2.." value="OF" /></NounConnector>"""), amount));

    /// <summary>numberRange: the count of the amount, whatever its structure.</summary>
    [TestMethod]
    [DataRow("0", "zero coins")]
    [DataRow("1", "one coin")]
    [DataRow("2", "two coins")]
    [DataRow("19", "ten nine coins")]
    [DataRow("20", "twenty OF coins")]
    [DataRow("21", "twenty one OF coins")]
    [DataRow("100", "one hundred OF coins")]
    [DataRow("101", "one hundred one OF coins")]
    [DataRow("1000", "one thousand OF coins")]
    [DataRow("1000000", "one million OF coins")]
    public void NumberRange(string amount, string expected)
        => Assert.AreEqual(expected, Amount(WithConnector("""<NounConnector><Substitution numberRange="20.." value="OF" /></NounConnector>"""), amount));

    /// <summary>
    /// numberModulo: the range is tested on the count modulo the value. A rule on the last two digits 1..19, declared
    /// first, wins over the general rule of equal specificity.
    /// </summary>
    [TestMethod]
    [DataRow("1", "one coin")]
    [DataRow("19", "ten nine coins")]
    [DataRow("20", "twenty OF coins")]
    [DataRow("99", "ninety nine OF coins")]
    [DataRow("100", "one hundred OF coins")]
    [DataRow("101", "one hundred one coins")]
    [DataRow("119", "one hundred ten nine coins")]
    [DataRow("120", "one hundred twenty OF coins")]
    [DataRow("1000", "one thousand OF coins")]
    [DataRow("1001", "one thousand one coins")]
    [DataRow("2000308", "two millions three hundred eight coins")]
    [DataRow("5200", "five thousand two hundred OF coins")]
    [DataRow("1000000", "one million OF coins")]
    public void NumberModulo(string amount, string expected)
        => Assert.AreEqual(expected, Amount(WithConnector("""
            <NounConnector>
              <Substitution numberRange="1..19" numberModulo="100" value="" />
              <Substitution numberRange="20.." value="OF" />
            </NounConnector>
            """), amount));

    /// <summary>The unit and the subunit are decided on their own counts.</summary>
    [TestMethod]
    public void UnitAndSubunit_Separately()
    {
        var converter = WithConnector("""<NounConnector><Substitution numberRange="20.." value="OF" /></NounConnector>""");
        Assert.AreEqual("one coin and twenty OF bits", Amount(converter, "1.20"));
        Assert.AreEqual("twenty OF coins and one bit", Amount(converter, "20.01"));
    }

    /// <summary>beforeNoun: listed beginnings of the noun only, ignoring case; no linguistic category is inferred.</summary>
    [TestMethod]
    public void BeforeNoun()
    {
        var converter = WithConnector("""<NounConnector><Substitution beforeNoun="e, ox" value="OF" /></NounConnector>""");
        Assert.AreEqual("two OF euros", Amount(converter, "2", Named("euros")));
        Assert.AreEqual("two OF Euros", Amount(converter, "2", Named("Euros")));
        Assert.AreEqual("two OF oxen", Amount(converter, "2", Named("oxen")));
        Assert.AreEqual("two orange", Amount(converter, "2", Named("orange")));
        Assert.AreEqual("two coins", Amount(converter, "2"));
    }

    // ===== Resolution ================================================================================

    /// <summary>The substitution with more constraints wins; the elided connector takes no separator.</summary>
    [TestMethod]
    public void MostSpecificWins()
    {
        var converter = WithConnector(Elision);
        Assert.AreEqual("one million O'euro", Amount(converter, "1000000", Named("euro")));
        Assert.AreEqual("one million OF coins", Amount(converter, "1000000"));
        Assert.AreEqual("one thousand euro", Amount(converter, "1000", Named("euro")));
    }

    /// <summary>The order of declaration does not matter across specificities.</summary>
    [TestMethod]
    public void MostSpecificWins_WhateverTheOrder()
    {
        var converter = WithConnector("""
            <NounConnector>
              <Substitution terminalScales="2.." beforeNoun="e" value="O'" nounSeparator="" />
              <Substitution terminalScales="2.." value="OF" />
            </NounConnector>
            """);
        Assert.AreEqual("one million O'euro", Amount(converter, "1000000", Named("euro")));
        Assert.AreEqual("one million OF coins", Amount(converter, "1000000"));
    }

    /// <summary>At equal specificity the first declared wins; it is never an ambiguity error.</summary>
    [TestMethod]
    public void EqualSpecificity_FirstDeclaredWins()
    {
        var first = WithConnector("""
            <NounConnector>
              <Substitution terminalScales="2.." beforeNoun="e" value="FIRST" />
              <Substitution terminalScales="2.." beforeNoun="e" value="SECOND" />
            </NounConnector>
            """);
        Assert.AreEqual("one million FIRST euro", Amount(first, "1000000", Named("euro")));
        var mixed = WithConnector("""
            <NounConnector>
              <Substitution numberRange="1000000" value="BYVALUE" />
              <Substitution terminalScales="2.." value="BYSCALE" />
            </NounConnector>
            """);
        Assert.AreEqual("one million BYVALUE coins", Amount(mixed, "1000000"));
        Assert.AreEqual("two millions BYSCALE coins", Amount(mixed, "2000000"));
    }

    /// <summary>With no matching substitution the default applies.</summary>
    [TestMethod]
    public void NoMatch_Default()
    {
        var converter = WithConnector("""
            <NounConnector default="BY"><Substitution terminalScales="2.." value="OF" /></NounConnector>
            """);
        Assert.AreEqual("one million OF coins", Amount(converter, "1000000"));
        Assert.AreEqual("one thousand BY coins", Amount(converter, "1000"));
    }

    /// <summary>A rule combining numberRange and terminalScales is more specific than a rule with one predicate.</summary>
    [TestMethod]
    public void TwoPredicates_MoreSpecific()
    {
        var converter = WithConnector("""
            <NounConnector>
              <Substitution terminalScales="2.." value="OF" />
              <Substitution numberRange="20.." value="BY" />
              <Substitution terminalScales="2.." numberRange="2000000.." value="BOTH" />
            </NounConnector>
            """);
        Assert.AreEqual("one million OF coins", Amount(converter, "1000000"));
        Assert.AreEqual("two millions BOTH coins", Amount(converter, "2000000"));
        Assert.AreEqual("twenty BY coins", Amount(converter, "20"));
    }

    /// <summary>The three constraints together form the most specific rule.</summary>
    [TestMethod]
    public void ThreePredicates()
    {
        var converter = WithConnector("""
            <NounConnector>
              <Substitution terminalScales="2.." beforeNoun="e" value="TWO" />
              <Substitution terminalScales="2.." numberRange="1000000" beforeNoun="e" value="THREE" />
            </NounConnector>
            """);
        Assert.AreEqual("one million THREE euro", Amount(converter, "1000000", Named("euro")));
        Assert.AreEqual("two millions TWO euro", Amount(converter, "2000000", Named("euro")));
        Assert.AreEqual(3, converter.CurrencyNounConnector!.Substitutions[1].Specificity);
    }

    /// <summary>The connector is independent of the variants of the numeral and of the currency replacements.</summary>
    [TestMethod]
    public void IndependentOfVariantsAndReplacements()
    {
        var converter = Build("""
            <Currency forceVariants="form=bound">
              <NounConnector><Substitution terminalScales="2.." beforeNoun="e" value="O'" nounSeparator="" /></NounConnector>
              <Replacement oldValue="two" newValue="deux" scope="StartsWith" />
            </Currency>
            """);
        Assert.AreEqual("one million O'euro", Amount(converter, "1000000", Named("euro")));
        Assert.AreEqual("deux millions O'euro", Amount(converter, "2000000", Named("euro")));
        Assert.AreEqual("an euro", Amount(converter, "1", Named("euro")));
    }

    // ===== Legacy shorthand ==========================================================================

    /// <summary>The legacy shorthand is the equivalent single substitution, and is not reported as a NounConnector.</summary>
    [TestMethod]
    public void Legacy_EquivalentSubstitution()
    {
        var legacy = Build("""<Currency terminalScaleConnector="OF" terminalScales="2.." />""");
        var connector = WithConnector("""<NounConnector><Substitution terminalScales="2.." value="OF" /></NounConnector>""");
        Assert.IsNull(legacy.CurrencyNounConnector);
        Assert.AreEqual("OF", legacy.CurrencyTerminalScaleConnector);
        Assert.IsNull(connector.CurrencyTerminalScaleConnector);
        foreach (string amount in new[] { "1", "1000", "1000000", "2000000", "1001000", "1000001", "1000000000", "2000000.20", "0" })
            Assert.AreEqual(Amount(legacy, amount), Amount(connector, amount), amount);
    }

    /// <summary>The two models are never mixed: setting both is rejected, in XML and through the options.</summary>
    [TestMethod]
    public void Legacy_CombinedWithNounConnector_Rejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Build("""
            <Currency terminalScaleConnector="OF" terminalScales="2.."><NounConnector default="" /></Currency>
            """));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(Build())
        {
            CurrencyTerminalScaleConnector = "OF",
            CurrencyTerminalScales = "2..",
            CurrencyNounConnector = new CurrencyNounConnector(),
        }));
    }

    // ===== Validation ================================================================================

    /// <summary>The schema rejects malformed substitutions.</summary>
    [TestMethod]
    [DataRow("""<Substitution terminalScales="2.." />""")]
    [DataRow("""<Substitution terminalScales="0.." value="OF" />""")]
    [DataRow("""<Substitution terminalScales="..2" value="OF" />""")]
    [DataRow("""<Substitution terminalScales="x" value="OF" />""")]
    [DataRow("""<Substitution terminalScales="" value="OF" />""")]
    [DataRow("""<Substitution numberRange="x" value="OF" />""")]
    [DataRow("""<Substitution numberRange="-1" value="OF" />""")]
    [DataRow("""<Substitution numberRange="" value="OF" />""")]
    [DataRow("""<Substitution numberRange="1..19" numberModulo="1" value="OF" />""")]
    [DataRow("""<Substitution numberRange="1..19" numberModulo="x" value="OF" />""")]
    [DataRow("""<Substitution beforeNoun="" value="OF" />""")]
    [DataRow("""<Substitution beforeNoun="a,,e" value="OF" />""")]
    [DataRow("""<Substitution beforeNoun="a, " value="OF" />""")]
    [DataRow("""<Substitution terminalScales="2.." value="OF" unknown="1" />""")]
    [DataRow("""<Substitution terminalScales="2.." value="OF"><Variant type="form" variant="bound" /></Substitution>""")]
    [DataRow("""<Other />""")]
    public void Schema_RejectsMalformed(string substitution)
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => WithConnector($"<NounConnector>{substitution}</NounConnector>"));

    /// <summary>The schema rejects unknown attributes of NounConnector and a NounConnector after the replacements.</summary>
    [TestMethod]
    public void Schema_RejectsMalformedConnector()
    {
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => WithConnector("""<NounConnector value="OF" />"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""
            <Currency><Replacement oldValue="one" newValue="a" /><NounConnector /></Currency>
            """));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""
            <Currency><NounConnector /><NounConnector /></Currency>
            """));
    }

    /// <summary>The loader rejects what the schema cannot express.</summary>
    [TestMethod]
    [DataRow("""<Substitution value="OF" />""")]
    [DataRow("""<Substitution value="OF" nounSeparator="" />""")]
    [DataRow("""<Substitution terminalScales="2.." value=" " />""")]
    [DataRow("""<Substitution numberRange="200.." numberModulo="100" value="OF" />""")]
    public void Loader_RejectsInvalid(string substitution)
        => Assert.ThrowsExactly<ArgumentException>(() => WithConnector($"<NounConnector>{substitution}</NounConnector>"));

    /// <summary>A white-space default is rejected.</summary>
    [TestMethod]
    public void Loader_RejectsWhiteSpaceDefault()
        => Assert.ThrowsExactly<ArgumentException>(() => WithConnector("""<NounConnector default=" " />"""));

    /// <summary>The public types validate their arguments.</summary>
    [TestMethod]
    public void Api_Validation()
    {
        var scales = NumberToStringConverter.ParseRangeExpression("2..");
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurrencyNounConnectorRule(null!, terminalScales: scales));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule(" ", terminalScales: scales));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF"));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF", terminalScales: NumberToStringConverter.ParseRangeExpression("0..")));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF", numberRange: NumberToStringConverter.ParseRangeExpression("..5")));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF", numberModulo: 100));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF", numberRange: NumberToStringConverter.ParseRangeExpression("1"), numberModulo: 1));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnectorRule("OF", beforeNoun: ["a", ""]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurrencyNounConnector(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnector(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyNounConnector(substitutions: [null!]));

        var rule = new CurrencyNounConnectorRule("d'", "", scales, NumberToStringConverter.ParseRangeExpression("1..19"), 100, ["a", "e"]);
        Assert.AreEqual("d'", rule.Value);
        Assert.AreEqual("", rule.NounSeparator);
        Assert.AreEqual(100L, rule.NumberModulo);
        Assert.AreEqual(3, rule.Specificity);
        Assert.AreEqual(1, new CurrencyNounConnectorRule("", beforeNoun: ["a"]).Specificity);
        CollectionAssert.AreEqual(new[] { "a", "e" }, rule.BeforeNoun.ToArray());
    }

    // ===== baseOn, clone, immutability ===============================================================

    /// <summary>baseOn: an absent Currency inherits the connector; a declared one replaces the whole element.</summary>
    [TestMethod]
    public void BaseOn_ReplacedAsAWhole()
    {
        string parent = NewName(), child = NewName(), replaced = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent,
            extra: $"<Currency>{Elision}<Replacement oldValue=\"one\" newValue=\"a\" /></Currency>",
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
                <Language baseOn="{parent}"><Culture>{replaced}</Culture><Currency forceVariants="form=bound" /></Language>
                """));
        Assert.AreEqual("a euro", Amount(converters[child], "1", Named("euro")));
        Assert.AreEqual("one million O'euro", Amount(converters[child], "1000000", Named("euro")));
        Assert.AreEqual("two millions OF coins", Amount(converters[child], "2000000"));
        Assert.AreEqual(2, converters[child].CurrencyNounConnector!.Substitutions.Count);
        Assert.AreEqual("an euro", Amount(converters[replaced], "1", Named("euro")));
        Assert.AreEqual("one million euro", Amount(converters[replaced], "1000000", Named("euro")));
        Assert.IsNull(converters[replaced].CurrencyNounConnector);
        Assert.AreEqual(0, converters[replaced].CurrencyReplacements.Count);
    }

    /// <summary>Options → converter → options → converter keeps the connector and the exact order of its rules.</summary>
    [TestMethod]
    public void OptionsAndClone_KeepOrder()
    {
        var scales = NumberToStringConverter.ParseRangeExpression("2..");
        var connector = new CurrencyNounConnector("", null,
        [
            new CurrencyNounConnectorRule("FIRST", terminalScales: scales, beforeNoun: ["e"]),
            new CurrencyNounConnectorRule("SECOND", terminalScales: scales, beforeNoun: ["e"]),
            new CurrencyNounConnectorRule("OF", terminalScales: scales),
        ]);
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(Build()) { CurrencyNounConnector = connector });
        Assert.AreSame(connector, converter.CurrencyNounConnector);
        Assert.AreEqual("one million FIRST euro", Amount(converter, "1000000", Named("euro")));
        var options = new NumberToStringConverterOptions(converter);
        Assert.AreSame(connector, options.CurrencyNounConnector);
        Assert.IsNull(options.CurrencyTerminalScaleConnector);
        var clone = new NumberToStringConverter(options);
        Assert.AreEqual("one million FIRST euro", Amount(clone, "1000000", Named("euro")));
        Assert.AreEqual("one million OF coins", Amount(clone, "1000000"));
        CollectionAssert.AreEqual(new[] { "FIRST", "SECOND", "OF" }, clone.CurrencyNounConnector!.Substitutions.Select(s => s.Value).ToArray());
    }

    /// <summary>A legacy-configured converter clones without mixing the two models.</summary>
    [TestMethod]
    public void Clone_Legacy()
    {
        var legacy = Build("""<Currency terminalScaleConnector="OF" terminalScales="2.." />""");
        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(legacy));
        Assert.AreEqual("two millions OF coins", Amount(clone, "2000000"));
        Assert.IsNull(clone.CurrencyNounConnector);
        Assert.AreEqual("OF", clone.CurrencyTerminalScaleConnector);
    }

    /// <summary>Mutating the source sequences after construction changes neither the rules nor the converter.</summary>
    [TestMethod]
    public void Snapshot()
    {
        var prefixes = new List<string> { "e" };
        var scales = NumberToStringConverter.ParseRangeExpression("2..");
        var rules = new List<CurrencyNounConnectorRule>
        {
            new("O'", "", scales, beforeNoun: prefixes),
            new("OF", terminalScales: scales),
        };
        var connector = new CurrencyNounConnector("", null, rules);
        var options = new NumberToStringConverterOptions(Build()) { CurrencyNounConnector = connector };
        var converter = new NumberToStringConverter(options);
        rules.Reverse();
        rules.Add(new CurrencyNounConnectorRule("BY", numberRange: NumberToStringConverter.ParseRangeExpression("0..")));
        prefixes.Add("c");
        options.CurrencyNounConnector = null;
        Assert.AreEqual(2, connector.Substitutions.Count);
        Assert.AreEqual("O'", connector.Substitutions[0].Value);
        Assert.AreEqual(1, connector.Substitutions[0].BeforeNoun.Count);
        Assert.AreEqual("one million O'euro", Amount(converter, "1000000", Named("euro")));
        Assert.AreEqual("one million OF coins", Amount(converter, "1000000"));
        Assert.AreEqual("one coin", Amount(converter, "1"));
    }
}
